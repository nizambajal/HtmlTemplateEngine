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
        while (true)
        {
            if (Current().Type == TokenType.EndOfFile) break;
            // Directives are wrapped in {{ }}, so peek through ExpressionOpen
            var lookahead = Current().Type == TokenType.ExpressionOpen ? PeekAt(1) : Current();
            if (endCondition(lookahead)) break;

            var node = ParseNext();
            if (node != null) children.Add(node);
        }
    }

    private AstNode? ParseNext()
    {
        var tok = Current();

        if (tok.Type == TokenType.Text) return ParseText();

        // {{ ... }} — peek at the token after {{ to decide if it is a directive or value expression
        if (tok.Type == TokenType.ExpressionOpen)
        {
            var inner = PeekAt(1);
            return inner.Type switch
            {
                TokenType.If => ParseIfBlock(),
                TokenType.Foreach => ParseForeachBlock(),
                TokenType.EndIf or TokenType.EndForeach or TokenType.Else or TokenType.ElseIf =>
                    throw new TemplateSyntaxException($"Unexpected '{{inner.Value}}'", inner.Line, inner.Column, inner.Value),
                _ => ParseExpression(raw: false)
            };
        }

        if (tok.Type == TokenType.RawExpressionOpen) return ParseExpression(raw: true);

        throw new TemplateSyntaxException($"Unexpected token {tok.Type} '{tok.Value}'", tok.Line, tok.Column, tok.Value);
    }

    /// <summary>Peeks at the token at position _pos+offset without consuming.</summary>
    private Token PeekAt(int offset)
    {
        var idx = _pos + offset;
        return idx < _tokens.Count ? _tokens[idx] : _tokens[^1];
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

    private IfNode ParseIfBlock()
    {
        Consume(TokenType.ExpressionOpen);
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
        while (Current().Type == TokenType.ExpressionOpen && PeekAt(1).Type == TokenType.ElseIf)
        {
            Consume(TokenType.ExpressionOpen);
            var elseIfTok = Consume(TokenType.ElseIf);
            var elseIfCond = ParseExpressionAst();
            Consume(TokenType.ExpressionClose);
            var branch = new ElseIfBranch { Condition = elseIfCond };
            ParseChildren(branch.Body, t => t.Type is TokenType.Else or TokenType.ElseIf or TokenType.EndIf or TokenType.EndOfFile);
            node.ElseIfBranches.Add(branch);
        }

        // Optional else
        if (Current().Type == TokenType.ExpressionOpen && PeekAt(1).Type == TokenType.Else)
        {
            Consume(TokenType.ExpressionOpen);
            Consume(TokenType.Else);
            Consume(TokenType.ExpressionClose);
            node.ElseBody = new List<AstNode>();
            ParseChildren(node.ElseBody, t => t.Type is TokenType.EndIf or TokenType.EndOfFile);
        }

        Consume(TokenType.ExpressionOpen);
        Consume(TokenType.EndIf);
        Consume(TokenType.ExpressionClose);
        return node;
    }

    // ── Foreach blocks ────────────────────────────────────────────────────────

    private ForeachNode ParseForeachBlock()
    {
        Consume(TokenType.ExpressionOpen);
        var forTok = Consume(TokenType.Foreach);

        // Read collection path — the lexer may emit "order.Items" as one dotted
        // Identifier token, or (if re-lexed) as separate Identifier + Dot + Identifier
        // tokens. ConsumeCollectionPath handles both cases.
        var collectionPath = ConsumeCollectionPath();

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
            Collection = collectionPath,
            Alias = alias,
            Line = forTok.Line,
            Column = forTok.Column
        };

        ParseChildren(node.Body, t => t.Type is TokenType.EndForeach or TokenType.EndOfFile);

        Consume(TokenType.ExpressionOpen);
        Consume(TokenType.EndForeach);
        Consume(TokenType.ExpressionClose);
        return node;
    }

    /// <summary>
    /// Reads a (possibly dotted) collection path from the token stream.
    /// Handles both single-token "order.Items" and multi-token "order · . · Items" forms.
    /// Stops before <c>as</c>, <c>}}</c>, or EOF.
    /// </summary>
    private string ConsumeCollectionPath()
    {
        var tok = Current();
        if (tok.Type != TokenType.Identifier)
            throw new TemplateSyntaxException(
                $"Expected collection name in foreach, got '{tok.Value}'",
                tok.Line, tok.Column, tok.Value);

        // The lexer already combines dotted paths into one Identifier token
        // (e.g. "category.Products" is emitted as a single token).
        // But defensively stitch together Identifier (Dot Identifier)* sequences too.
        var path = new System.Text.StringBuilder(Advance().Value);

        while (Current().Type == TokenType.Dot)
        {
            Advance(); // consume dot
            if (Current().Type != TokenType.Identifier)
                throw new TemplateSyntaxException(
                    "Expected identifier after '.' in collection path",
                    Current().Line, Current().Column, Current().Value);
            path.Append('.').Append(Advance().Value);
        }

        return path.ToString();
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
                var helperExpr = new HelperCallExpr { HelperName = tok.Value, Line = tok.Line, Column = tok.Column };
                helperExpr.Arguments.AddRange(args);
                return helperExpr;
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