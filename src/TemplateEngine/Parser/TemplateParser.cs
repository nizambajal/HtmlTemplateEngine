using TemplateEngine.Ast;
using TemplateEngine.Exceptions;
using TemplateEngine.Lexer;

namespace TemplateEngine.Parser;

/// <summary>
/// Consumes the token stream produced by <see cref="TemplateLexer"/> and builds an
/// <see cref="DocumentNode"/> AST. Uses a recursive-descent approach for expressions
/// and a stack-based approach for block nesting.
/// </summary>
public sealed class TemplateParser
{
    private readonly IReadOnlyList<Token> _tokens;
    private int _pos;

    public TemplateParser(IReadOnlyList<Token> tokens)
    {
        _tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
        _pos = 0;
    }

    // ── Public entry point ────────────────────────────────────────────────────

    /// <summary>Parses the token stream and returns the document AST.</summary>
    public DocumentNode Parse()
    {
        var doc = new DocumentNode { Line = 1, Column = 1 };
        ParseChildren(doc.Children, endCondition: t => t.Type == TokenType.EndOfFile);
        return doc;
    }

    // ── Child parsing ─────────────────────────────────────────────────────────

    private void ParseChildren(List<AstNode> children, Func<Token, bool> endCondition)
    {
        while (!endCondition(Current()))
        {
            if (Current().Type == TokenType.EndOfFile) break;

            var node = ParseNext();
            if (node != null) children.Add(node);
        }
    }

    private AstNode? ParseNext()
    {
        var tok = Current();

        return tok.Type switch
        {
            TokenType.Text => ParseText(),
            TokenType.ExpressionOpen => ParseExpression(raw: false),
            TokenType.RawExpressionOpen => ParseExpression(raw: true),
            TokenType.If => ParseIf(),
            TokenType.Foreach => ParseForeach(),
            // These are consumed by their parent and should not appear here
            TokenType.Else or TokenType.ElseIf or TokenType.EndIf or TokenType.EndForeach =>
                throw new TemplateSyntaxException($"Unexpected '{tok.Value}'", tok.Line, tok.Column, tok.Value),
            _ => throw new TemplateSyntaxException($"Unexpected token {tok.Type} '{tok.Value}'", tok.Line, tok.Column, tok.Value)
        };
    }

    // ── Text ──────────────────────────────────────────────────────────────────

    private TextNode ParseText()
    {
        var tok = Consume(TokenType.Text);
        return new TextNode { Content = tok.Value, Line = tok.Line, Column = tok.Column };
    }

    // ── Expressions ───────────────────────────────────────────────────────────

    private ExpressionNode ParseExpression(bool raw)
    {
        // Consume {{ or {{{
        var open = Consume(raw ? TokenType.RawExpressionOpen : TokenType.ExpressionOpen);

        // Build expression AST
        var expr = ParseExpressionAst();

        // Optional format specifier
        string? format = null;
        if (Current().Type == TokenType.Format)
        {
            format = Current().Value;
            Advance();
        }

        // Consume }} or }}}
        Consume(raw ? TokenType.RawExpressionClose : TokenType.ExpressionClose);

        return new ExpressionNode
        {
            Expression = expr,
            Format = format,
            IsRaw = raw,
            Line = open.Line,
            Column = open.Column
        };
    }

    // ── If blocks ─────────────────────────────────────────────────────────────

    private IfNode ParseIf()
    {
        var ifTok = Consume(TokenType.If);
        var cond = ParseExpressionAst();
        Consume(TokenType.ExpressionClose);

        var node = new IfNode
        {
            Condition = cond,
            Line = ifTok.Line,
            Column = ifTok.Column
        };

        // Parse body until else/else-if/endif
        ParseChildren(node.ThenBody, t => t.Type is TokenType.Else or TokenType.ElseIf or TokenType.EndIf or TokenType.EndOfFile);

        // Handle else-if chains
        while (Current().Type == TokenType.ElseIf)
        {
            var elseIfTok = Consume(TokenType.ElseIf);
            var elseIfCond = ParseExpressionAst();
            Consume(TokenType.ExpressionClose);
            var branch = new ElseIfBranch { Condition = elseIfCond };
            ParseChildren(branch.Body, t => t.Type is TokenType.Else or TokenType.ElseIf or TokenType.EndIf or TokenType.EndOfFile);
            node.ElseIfBranches.Add(branch);
        }

        // Optional else
        if (Current().Type == TokenType.Else)
        {
            Consume(TokenType.Else);
            Consume(TokenType.ExpressionClose);
            node.ElseBody = new List<AstNode>();
            ParseChildren(node.ElseBody, t => t.Type is TokenType.EndIf or TokenType.EndOfFile);
        }

        Consume(TokenType.EndIf);
        Consume(TokenType.ExpressionClose);
        return node;
    }

    // ── Foreach blocks ────────────────────────────────────────────────────────

    private ForeachNode ParseForeach()
    {
        var forTok = Consume(TokenType.Foreach);

        // Read collection identifier
        var collectionTok = Consume(TokenType.Identifier);

        // Optional "as alias"
        string? alias = null;
        if (Current().Type == TokenType.As)
        {
            Consume(TokenType.As);
            alias = Consume(TokenType.Identifier).Value;
        }

        Consume(TokenType.ExpressionClose);

        var node = new ForeachNode
        {
            Collection = collectionTok.Value,
            Alias = alias,
            Line = forTok.Line,
            Column = forTok.Column
        };

        ParseChildren(node.Body, t => t.Type is TokenType.EndForeach or TokenType.EndOfFile);

        Consume(TokenType.EndForeach);
        Consume(TokenType.ExpressionClose);
        return node;
    }

    // ── Expression AST (recursive descent) ───────────────────────────────────

    /// <summary>Parses a full expression with operator precedence.</summary>
    private ExpressionAst ParseExpressionAst() => ParseOr();

    private ExpressionAst ParseOr()
    {
        var left = ParseAnd();
        while (Current().Type == TokenType.Or)
        {
            var op = Advance();
            var right = ParseAnd();
            left = new BinaryExpr { Operator = "||", Left = left, Right = right, Line = op.Line, Column = op.Column };
        }
        return left;
    }

    private ExpressionAst ParseAnd()
    {
        var left = ParseEquality();
        while (Current().Type == TokenType.And)
        {
            var op = Advance();
            var right = ParseEquality();
            left = new BinaryExpr { Operator = "&&", Left = left, Right = right, Line = op.Line, Column = op.Column };
        }
        return left;
    }

    private ExpressionAst ParseEquality()
    {
        var left = ParseComparison();
        while (Current().Type is TokenType.Equal or TokenType.NotEqual)
        {
            var op = Advance();
            var right = ParseComparison();
            left = new BinaryExpr { Operator = op.Value, Left = left, Right = right, Line = op.Line, Column = op.Column };
        }
        return left;
    }

    private ExpressionAst ParseComparison()
    {
        var left = ParseAdditive();
        while (Current().Type is TokenType.GreaterThan or TokenType.LessThan
                                or TokenType.GreaterThanOrEqual or TokenType.LessThanOrEqual)
        {
            var op = Advance();
            var right = ParseAdditive();
            left = new BinaryExpr { Operator = op.Value, Left = left, Right = right, Line = op.Line, Column = op.Column };
        }
        return left;
    }

    private ExpressionAst ParseAdditive()
    {
        var left = ParseMultiplicative();
        while (Current().Type is TokenType.Plus or TokenType.Minus)
        {
            var op = Advance();
            var right = ParseMultiplicative();
            left = new BinaryExpr { Operator = op.Value, Left = left, Right = right, Line = op.Line, Column = op.Column };
        }
        return left;
    }

    private ExpressionAst ParseMultiplicative()
    {
        var left = ParseUnary();
        while (Current().Type is TokenType.Star or TokenType.Slash or TokenType.Percent)
        {
            var op = Advance();
            var right = ParseUnary();
            left = new BinaryExpr { Operator = op.Value, Left = left, Right = right, Line = op.Line, Column = op.Column };
        }
        return left;
    }

    private ExpressionAst ParseUnary()
    {
        if (Current().Type == TokenType.Not)
        {
            var op = Advance();
            return new UnaryExpr { Operator = "!", Operand = ParseUnary(), Line = op.Line, Column = op.Column };
        }
        if (Current().Type == TokenType.Minus)
        {
            var op = Advance();
            return new UnaryExpr { Operator = "-", Operand = ParseUnary(), Line = op.Line, Column = op.Column };
        }
        return ParsePrimary();
    }

    private ExpressionAst ParsePrimary()
    {
        var tok = Current();

        // Grouped expression
        if (tok.Type == TokenType.OpenParen)
        {
            Advance();
            var inner = ParseExpressionAst();
            Consume(TokenType.CloseParen);
            return inner;
        }

        // Literals
        if (tok.Type == TokenType.String)
        { Advance(); return new StringLiteralExpr { Value = tok.Value, Line = tok.Line, Column = tok.Column }; }

        if (tok.Type == TokenType.Number)
        {
            Advance();
            return new NumberLiteralExpr { Value = double.Parse(tok.Value, System.Globalization.CultureInfo.InvariantCulture), Line = tok.Line, Column = tok.Column };
        }

        if (tok.Type == TokenType.Boolean)
        { Advance(); return new BoolLiteralExpr { Value = tok.Value == "true", Line = tok.Line, Column = tok.Column }; }

        if (tok.Type == TokenType.Null)
        { Advance(); return new NullLiteralExpr { Line = tok.Line, Column = tok.Column }; }

        // Loop metadata: @index, @first, @last, @count
        if (tok.Type == TokenType.Identifier && tok.Value.StartsWith('@'))
        { Advance(); return new LoopMetaExpr { MetaName = tok.Value[1..], Line = tok.Line, Column = tok.Column }; }

        // Identifier — could be a helper call or property path
        if (tok.Type == TokenType.Identifier)
        {
            Advance();

            // Check for helper call: Identifier(...)
            if (Current().Type == TokenType.OpenParen)
            {
                Advance(); // consume (
                var args = new List<ExpressionAst>();
                while (Current().Type != TokenType.CloseParen && Current().Type != TokenType.EndOfFile)
                {
                    args.Add(ParseExpressionAst());
                    if (Current().Type == TokenType.Comma) Advance();
                }
                Consume(TokenType.CloseParen);
                return new HelperCallExpr { HelperName = tok.Value, Line = tok.Line, Column = tok.Column, Arguments = { } }
                    .WithArgs(args);
            }

            return new PropertyExpr { Path = tok.Value, Line = tok.Line, Column = tok.Column };
        }

        throw new TemplateSyntaxException($"Unexpected token '{tok.Value}' in expression", tok.Line, tok.Column, tok.Value);
    }

    // ── Token helpers ─────────────────────────────────────────────────────────

    private Token Current() => _pos < _tokens.Count ? _tokens[_pos] : _tokens[^1];

    private Token Advance()
    {
        var t = Current();
        if (_pos < _tokens.Count - 1) _pos++;
        return t;
    }

    private Token Consume(TokenType expected)
    {
        var tok = Current();
        if (tok.Type != expected)
            throw new TemplateSyntaxException(
                $"Expected {expected} but found {tok.Type} '{tok.Value}'",
                tok.Line, tok.Column, tok.Value);
        return Advance();
    }
}

internal static class HelperCallExprExtensions
{
    public static HelperCallExpr WithArgs(this HelperCallExpr expr, List<ExpressionAst> args)
    {
        expr.Arguments.AddRange(args);
        return expr;
    }
}
