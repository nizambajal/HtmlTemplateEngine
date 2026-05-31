using TemplateEngine.Exceptions;

namespace TemplateEngine.Lexer;

/// <summary>
/// Tokenizes a template string into a sequence of <see cref="Token"/> objects.
/// The lexer operates in two modes: text mode (scanning raw HTML) and expression mode
/// (scanning inside {{ }} delimiters). It is deliberately stateless per-call so it
/// can be re-used across threads safely by constructing a new instance per template.
/// </summary>
public sealed class TemplateLexer
{
    private readonly string _source;
    private int _pos;
    private int _line;
    private int _col;
    private readonly List<Token> _tokens = new();

    public TemplateLexer(string source)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _pos = 0;
        _line = 1;
        _col = 1;
    }

    /// <summary>
    /// Tokenizes the entire template and returns the token list.
    /// </summary>
    //public IReadOnlyList<Token> Tokenize()
    //{
    //    while (_pos < _source.Length)
    //    {
    //        if (Match("{{{"))
    //        {
    //            Emit(TokenType.RawExpressionOpen, "{{{");
    //            ScanExpression(raw: true);
    //        }
    //        else if (Match("{{"))
    //        {
    //            Emit(TokenType.ExpressionOpen, "{{");
    //            ScanExpression(raw: false);
    //        }
    //        else
    //        {
    //            ScanText();
    //        }
    //    }

    //    _tokens.Add(new Token(TokenType.EndOfFile, string.Empty, _line, _col));
    //    return _tokens;
    //}

    public IReadOnlyList<Token> Tokenize()
    {
        while (_pos < _source.Length)
        {
            int line = _line;
            int col = _col;

            if (Match("{{{"))
            {
                Emit(
                    TokenType.RawExpressionOpen,
                    "{{{",
                    line,
                    col);

                ScanExpression(raw: true);
            }
            else if (Match("{{"))
            {
                Emit(
                    TokenType.ExpressionOpen,
                    "{{",
                    line,
                    col);

                ScanExpression(raw: false);
            }
            else
            {
                ScanText();
            }
        }

        _tokens.Add(
            new Token(
                TokenType.EndOfFile,
                string.Empty,
                _line,
                _col));

        return _tokens;
    }

    // ── Text mode ──────────────────────────────────────────────────────────────

    private void ScanText()
    {
        var start = _pos;
        var startLine = _line;
        var startCol = _col;

        while (_pos < _source.Length && !Peek("{{") && !Peek("{{{"))
        {
            Advance();
        }

        if (_pos > start)
        {
            _tokens.Add(new Token(TokenType.Text, _source[start.._pos], startLine, startCol));
        }
    }

    // ── Expression mode ────────────────────────────────────────────────────────

    //private void ScanExpression(bool raw)
    //{
    //    SkipWhitespace();

    //    // Check for closing directive: {{/if}} {{/foreach}}
    //    if (Current() == '/')
    //    {
    //        Advance();
    //        SkipWhitespace();
    //        var keyword = ReadIdentifierRaw();
    //        SkipWhitespace();
    //        Expect(raw ? "}}}" : "}}");
    //        var closeType = keyword switch
    //        {
    //            "if" => TokenType.EndIf,
    //            "foreach" => TokenType.EndForeach,
    //            _ => TokenType.Identifier
    //        };
    //        Emit(closeType, "/" + keyword);
    //        if (raw) Emit(TokenType.RawExpressionClose, "}}}");
    //        else Emit(TokenType.ExpressionClose, "}}");
    //        return;
    //    }

    //    // Check for opening directive: {{#if ...}} {{#foreach ...}}
    //    if (Current() == '#')
    //    {
    //        Advance();
    //        SkipWhitespace();
    //        var directive = ReadIdentifierRaw();
    //        ScanDirectiveContent(directive, raw);
    //        return;
    //    }

    //    // Regular expression: could be {{else}} {{else if ...}}
    //    if (PeekKeyword("else"))
    //    {
    //        ScanElse(raw);
    //        return;
    //    }

    //    // Normal expression possibly with format specifier
    //    ScanValueExpression(raw);
    //}

    private void ScanExpression(bool raw)
    {
        SkipWhitespace();

        int line = _line;
        int col = _col;

        // Closing directive: {{/if}} {{/foreach}}
        if (Current() == '/')
        {
            Advance();

            SkipWhitespace();

            var keyword = ReadIdentifierRaw();

            SkipWhitespace();

            Expect(raw ? "}}}" : "}}");

            var closeType = keyword switch
            {
                "if" => TokenType.EndIf,
                "foreach" => TokenType.EndForeach,
                _ => TokenType.Identifier
            };

            Emit(
                closeType,
                "/" + keyword,
                line,
                col);

            Emit(
                raw
                    ? TokenType.RawExpressionClose
                    : TokenType.ExpressionClose,
                raw ? "}}}" : "}}",
                _line,
                _col);

            return;
        }

        // Opening directive: {{#if}} {{#foreach}}
        if (Current() == '#')
        {
            Advance();

            SkipWhitespace();

            var directive = ReadIdentifierRaw();

            ScanDirectiveContent(
                directive,
                raw);

            return;
        }

        // {{else}} or {{else if}}
        if (PeekKeyword("else"))
        {
            ScanElse(raw);
            return;
        }

        // Normal expression
        ScanValueExpression(raw);
    }

    //private void ScanDirectiveContent(string directive, bool raw)
    //{
    //    SkipWhitespace();
    //    switch (directive)
    //    {
    //        case "if":
    //            Emit(TokenType.If, "if");
    //            ScanExpressionUntilClose(raw);
    //            break;
    //        case "foreach":
    //            Emit(TokenType.Foreach, "foreach");
    //            ScanExpressionUntilClose(raw);
    //            break;
    //        default:
    //            // Custom block helper
    //            Emit(TokenType.Identifier, directive);
    //            ScanExpressionUntilClose(raw);
    //            break;
    //    }
    //}

    //private void ScanElse(bool raw)
    //{
    //    // Consume "else"
    //    ConsumeKeyword("else");
    //    SkipWhitespace();

    //    if (PeekKeyword("if"))
    //    {
    //        ConsumeKeyword("if");
    //        Emit(TokenType.ElseIf, "else if");
    //        SkipWhitespace();
    //        ScanExpressionUntilClose(raw);
    //    }
    //    else
    //    {
    //        Emit(TokenType.Else, "else");
    //        SkipWhitespace();
    //        Expect(raw ? "}}}" : "}}");
    //        Emit(raw ? TokenType.RawExpressionClose : TokenType.ExpressionClose, raw ? "}}}" : "}}");
    //    }
    //}

    //private void ScanValueExpression(bool raw)
    //{
    //    // Scan tokens until we hit '}}'
    //    while (_pos < _source.Length)
    //    {
    //        SkipWhitespace();

    //        if (Peek("}}}", offset: 0) || Peek("}}", offset: 0))
    //            break;

    //        var tok = ReadExpressionToken();
    //        if (tok != null) _tokens.Add(tok);
    //    }

    //    Expect(raw ? "}}}" : "}}");
    //    Emit(raw ? TokenType.RawExpressionClose : TokenType.ExpressionClose, raw ? "}}}" : "}}");
    //}

    //private void ScanExpressionUntilClose(bool raw)
    //{
    //    while (_pos < _source.Length)
    //    {
    //        SkipWhitespace();
    //        if (Peek("}}}", offset: 0) || Peek("}}", offset: 0))
    //            break;

    //        var tok = ReadExpressionToken();
    //        if (tok != null) _tokens.Add(tok);
    //    }

    //    Expect(raw ? "}}}" : "}}");
    //    Emit(raw ? TokenType.RawExpressionClose : TokenType.ExpressionClose, raw ? "}}}" : "}}");
    //}

    private void ScanDirectiveContent(string directive, bool raw)
    {
        SkipWhitespace();

        int line = _line;
        int col = _col;

        switch (directive)
        {
            case "if":
                Emit(
                    TokenType.If,
                    "if",
                    line,
                    col);

                ScanExpressionUntilClose(raw);
                break;

            case "foreach":
                Emit(
                    TokenType.Foreach,
                    "foreach",
                    line,
                    col);

                ScanExpressionUntilClose(raw);
                break;

            default:
                // Custom block helper
                Emit(
                    TokenType.Identifier,
                    directive,
                    line,
                    col);

                ScanExpressionUntilClose(raw);
                break;
        }
    }

    private void ScanElse(bool raw)
    {
        int line = _line;
        int col = _col;

        ConsumeKeyword("else");

        SkipWhitespace();

        if (PeekKeyword("if"))
        {
            ConsumeKeyword("if");

            Emit(
                TokenType.ElseIf,
                "else if",
                line,
                col);

            SkipWhitespace();

            ScanExpressionUntilClose(raw);
        }
        else
        {
            Emit(
                TokenType.Else,
                "else",
                line,
                col);

            SkipWhitespace();

            Expect(raw ? "}}}" : "}}");

            Emit(
                raw
                    ? TokenType.RawExpressionClose
                    : TokenType.ExpressionClose,
                raw ? "}}}" : "}}",
                _line,
                _col);
        }
    }

    private void ScanValueExpression(bool raw)
    {
        while (_pos < _source.Length)
        {
            SkipWhitespace();

            if (Peek("}}}") || Peek("}}"))
                break;

            var tok = ReadExpressionToken();

            if (tok != null)
                _tokens.Add(tok);
        }

        int line = _line;
        int col = _col;

        Expect(raw ? "}}}" : "}}");

        Emit(
            raw
                ? TokenType.RawExpressionClose
                : TokenType.ExpressionClose,
            raw ? "}}}" : "}}",
            line,
            col);
    }

    private void ScanExpressionUntilClose(bool raw)
    {
        while (_pos < _source.Length)
        {
            SkipWhitespace();

            if (Peek("}}}") || Peek("}}"))
                break;

            var tok = ReadExpressionToken();

            if (tok != null)
                _tokens.Add(tok);
        }

        int line = _line;
        int col = _col;

        Expect(raw ? "}}}" : "}}");

        Emit(
            raw
                ? TokenType.RawExpressionClose
                : TokenType.ExpressionClose,
            raw ? "}}}" : "}}",
            line,
            col);
    }

    //private Token? ReadExpressionToken()
    //{
    //    int startLine = _line, startCol = _col;
    //    char c = Current();

    //    // Parent context navigation ../
    //    if (c == '.' && _pos + 1 < _source.Length && _source[_pos + 1] == '.')
    //    {
    //        var sb = new System.Text.StringBuilder();
    //        while (_pos < _source.Length && _source[_pos] == '.' )
    //        {
    //            if (_pos + 1 < _source.Length && _source[_pos + 1] == '/')
    //            {
    //                sb.Append("../");
    //                _pos += 2; _col += 2;
    //            }
    //            else break;
    //        }
    //        // Now read the actual identifier
    //        var path = ReadIdentifierPath();
    //        return new Token(TokenType.Identifier, sb.ToString() + path, startLine, startCol);
    //    }

    //    // Format specifier (colon)
    //    if (c == ':')
    //    {
    //        Advance();
    //        // Everything until }} is the format string
    //        var fmtStart = _pos;
    //        while (_pos < _source.Length && !Peek("}}", offset: 0) && !Peek("}}}", offset: 0))
    //            Advance();
    //        return new Token(TokenType.Format, _source[fmtStart.._pos], startLine, startCol);
    //    }

    //    // Null-safe operator ?.
    //    if (c == '?' && _pos + 1 < _source.Length && _source[_pos + 1] == '.')
    //    {
    //        _pos += 2; _col += 2;
    //        return new Token(TokenType.NullCoalesce, "?.", startLine, startCol);
    //    }

    //    // String literals
    //    if (c == '"' || c == '\'') return ReadString(c, startLine, startCol);

    //    // Numbers
    //    if (char.IsDigit(c) || (c == '-' && _pos + 1 < _source.Length && char.IsDigit(_source[_pos + 1])))
    //        return ReadNumber(startLine, startCol);

    //    // Identifiers and keywords
    //    if (char.IsLetter(c) || c == '_' || c == '@')
    //        return ReadIdentifierToken(startLine, startCol);

    //    // Two-char operators
    //    if (_pos + 1 < _source.Length)
    //    {
    //        var two = _source.Substring(_pos, 2);
    //        Token? twoChar = two switch
    //        {
    //            "==" => Tok(TokenType.Equal, "==", startLine, startCol),
    //            "!=" => Tok(TokenType.NotEqual, "!=", startLine, startCol),
    //            ">=" => Tok(TokenType.GreaterThanOrEqual, ">=", startLine, startCol),
    //            "<=" => Tok(TokenType.LessThanOrEqual, "<=", startLine, startCol),
    //            "&&" => Tok(TokenType.And, "&&", startLine, startCol),
    //            "||" => Tok(TokenType.Or, "||", startLine, startCol),
    //            _ => null
    //        };
    //        if (twoChar != null) { _pos += 2; _col += 2; return twoChar; }
    //    }

    //    // Single-char operators
    //    Token? single = c switch
    //    {
    //        '+' => Tok(TokenType.Plus, "+", startLine, startCol),
    //        '-' => Tok(TokenType.Minus, "-", startLine, startCol),
    //        '*' => Tok(TokenType.Star, "*", startLine, startCol),
    //        '/' => Tok(TokenType.Slash, "/", startLine, startCol),
    //        '%' => Tok(TokenType.Percent, "%", startLine, startCol),
    //        '>' => Tok(TokenType.GreaterThan, ">", startLine, startCol),
    //        '<' => Tok(TokenType.LessThan, "<", startLine, startCol),
    //        '!' => Tok(TokenType.Not, "!", startLine, startCol),
    //        '.' => Tok(TokenType.Dot, ".", startLine, startCol),
    //        ',' => Tok(TokenType.Comma, ",", startLine, startCol),
    //        '(' => Tok(TokenType.OpenParen, "(", startLine, startCol),
    //        ')' => Tok(TokenType.CloseParen, ")", startLine, startCol),
    //        _ => null
    //    };

    //    if (single != null) { Advance(); return single; }

    //    throw new TemplateParseException($"Unexpected character '{c}'", startLine, startCol, c.ToString());
    //}

    private Token? ReadExpressionToken()
    {
        int startLine = _line;
        int startCol = _col;

        char c = Current();

        // Parent navigation
        if (Peek("../"))
        {
            var sb = new System.Text.StringBuilder(16);

            while (Peek("../"))
            {
                sb.Append("../");
                _pos += 3;
                _col += 3;
            }

            var path = ReadIdentifierPath();

            return new Token(
                TokenType.Identifier,
                sb + path,
                startLine,
                startCol);
        }

        // Format
        if (c == ':')
        {
            Advance();

            int fmtStart = _pos;

            while (_pos < _source.Length &&
                   !Peek("}}") &&
                   !Peek("}}}"))
            {
                Advance();
            }

            return new Token(
                TokenType.Format,
                _source[fmtStart.._pos],
                startLine,
                startCol);
        }

        // Null-safe
        if (Peek("?."))
        {
            _pos += 2;
            _col += 2;

            return new Token(
                TokenType.NullCoalesce,
                "?.",
                startLine,
                startCol);
        }

        // String
        if (c == '"' || c == '\'')
            return ReadString(
                c,
                startLine,
                startCol);

        // Number
        if (char.IsDigit(c) ||
           (c == '-' &&
            _pos + 1 < _source.Length &&
            char.IsDigit(_source[_pos + 1])))
        {
            return ReadNumber(
                startLine,
                startCol);
        }

        // Identifier
        if (char.IsLetter(c) ||
            c == '_' ||
            c == '@')
        {
            return ReadIdentifierToken(
                startLine,
                startCol);
        }

        // Two-character operators (no Substring allocation)

        if (_pos + 1 < _source.Length)
        {
            char c2 = _source[_pos + 1];

            if (c == '=' && c2 == '=')
            {
                _pos += 2;
                _col += 2;

                return Tok(
                    TokenType.Equal,
                    "==",
                    startLine,
                    startCol);
            }

            if (c == '!' && c2 == '=')
            {
                _pos += 2;
                _col += 2;

                return Tok(
                    TokenType.NotEqual,
                    "!=",
                    startLine,
                    startCol);
            }

            if (c == '>' && c2 == '=')
            {
                _pos += 2;
                _col += 2;

                return Tok(
                    TokenType.GreaterThanOrEqual,
                    ">=",
                    startLine,
                    startCol);
            }

            if (c == '<' && c2 == '=')
            {
                _pos += 2;
                _col += 2;

                return Tok(
                    TokenType.LessThanOrEqual,
                    "<=",
                    startLine,
                    startCol);
            }

            if (c == '&' && c2 == '&')
            {
                _pos += 2;
                _col += 2;

                return Tok(
                    TokenType.And,
                    "&&",
                    startLine,
                    startCol);
            }

            if (c == '|' && c2 == '|')
            {
                _pos += 2;
                _col += 2;

                return Tok(
                    TokenType.Or,
                    "||",
                    startLine,
                    startCol);
            }
        }

        Token? single = c switch
        {
            '+' => Tok(TokenType.Plus, "+", startLine, startCol),
            '-' => Tok(TokenType.Minus, "-", startLine, startCol),
            '*' => Tok(TokenType.Star, "*", startLine, startCol),
            '/' => Tok(TokenType.Slash, "/", startLine, startCol),
            '%' => Tok(TokenType.Percent, "%", startLine, startCol),
            '>' => Tok(TokenType.GreaterThan, ">", startLine, startCol),
            '<' => Tok(TokenType.LessThan, "<", startLine, startCol),
            '!' => Tok(TokenType.Not, "!", startLine, startCol),
            '.' => Tok(TokenType.Dot, ".", startLine, startCol),
            ',' => Tok(TokenType.Comma, ",", startLine, startCol),
            '(' => Tok(TokenType.OpenParen, "(", startLine, startCol),
            ')' => Tok(TokenType.CloseParen, ")", startLine, startCol),
            _ => null
        };

        if (single != null)
        {
            Advance();
            return single;
        }

        throw new TemplateParseException(
            $"Unexpected character '{c}'",
            startLine,
            startCol,
            c.ToString());
    }

    private Token Tok(TokenType t, string v, int line, int col) => new(t, v, line, col);

    //private Token ReadString(char quote, int startLine, int startCol)
    //{
    //    Advance(); // consume opening quote
    //    var sb = new System.Text.StringBuilder();
    //    while (_pos < _source.Length && Current() != quote)
    //    {
    //        if (Current() == '\\' && _pos + 1 < _source.Length)
    //        {
    //            Advance();
    //            sb.Append(Current() switch { 'n' => '\n', 't' => '\t', 'r' => '\r', _ => Current() });
    //        }
    //        else sb.Append(Current());
    //        Advance();
    //    }
    //    Advance(); // consume closing quote
    //    return new Token(TokenType.String, sb.ToString(), startLine, startCol);
    //}

    private Token ReadString(
    char quote,
    int startLine,
    int startCol)
    {
        Advance();

        var sb = new System.Text.StringBuilder(32);

        while (_pos < _source.Length &&
               Current() != quote)
        {
            if (Current() == '\\' &&
                _pos + 1 < _source.Length)
            {
                Advance();

                sb.Append(Current() switch
                {
                    'n' => '\n',
                    't' => '\t',
                    'r' => '\r',
                    _ => Current()
                });
            }
            else
            {
                sb.Append(Current());
            }

            Advance();
        }

        if (_pos >= _source.Length)
        {
            throw new TemplateParseException(
                "Unterminated string",
                startLine,
                startCol,
                null);
        }

        Advance();

        return new Token(
            TokenType.String,
            sb.ToString(),
            startLine,
            startCol);
    }

    private Token ReadNumber(int startLine, int startCol)
    {
        var start = _pos;
        if (Current() == '-') Advance();
        while (_pos < _source.Length && char.IsDigit(Current())) Advance();
        if (_pos < _source.Length && Current() == '.')
        {
            Advance();
            while (_pos < _source.Length && char.IsDigit(Current())) Advance();
        }
        return new Token(TokenType.Number, _source[start.._pos], startLine, startCol);
    }

    private Token ReadIdentifierToken(int startLine, int startCol)
    {
        var path = ReadIdentifierPath();
        return (path) switch
        {
            "true" => new Token(TokenType.Boolean, "true", startLine, startCol),
            "false" => new Token(TokenType.Boolean, "false", startLine, startCol),
            "null" => new Token(TokenType.Null, "null", startLine, startCol),
            "as" => new Token(TokenType.As, "as", startLine, startCol),
            _ => new Token(TokenType.Identifier, path, startLine, startCol)
        };
    }

    private string ReadIdentifierPath()
    {
        var sb = new System.Text.StringBuilder();
        // Support @index, @first, @last, @count
        if (_pos < _source.Length && Current() == '@')
        {
            sb.Append('@');
            Advance();
        }
        while (_pos < _source.Length && (char.IsLetterOrDigit(Current()) || Current() == '_'))
        {
            sb.Append(Current());
            Advance();
        }
        // Support dotted paths: Customer.Name and null-safe: Customer?.Name
        while (_pos < _source.Length)
        {
            if (Current() == '?' && _pos + 1 < _source.Length && _source[_pos + 1] == '.')
            {
                sb.Append("?.");
                _pos += 2; _col += 2;
                while (_pos < _source.Length && (char.IsLetterOrDigit(Current()) || Current() == '_'))
                { sb.Append(Current()); Advance(); }
            }
            else if (Current() == '.')
            {
                // Lookahead: is next char a letter? (not ..) 
                if (_pos + 1 < _source.Length && (char.IsLetterOrDigit(_source[_pos + 1]) || _source[_pos + 1] == '_'))
                {
                    sb.Append('.');
                    Advance();
                    while (_pos < _source.Length && (char.IsLetterOrDigit(Current()) || Current() == '_'))
                    { sb.Append(Current()); Advance(); }
                }
                else break;
            }
            else break;
        }
        return sb.ToString();
    }

    private string ReadIdentifierRaw()
    {
        SkipWhitespace();
        var sb = new System.Text.StringBuilder();
        while (_pos < _source.Length && (char.IsLetterOrDigit(Current()) || Current() == '_'))
        { sb.Append(Current()); Advance(); }
        return sb.ToString();
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private char Current() => _pos < _source.Length ? _source[_pos] : '\0';

    private void Advance()
    {
        if (_pos < _source.Length)
        {
            if (_source[_pos] == '\n') { _line++; _col = 1; }
            else _col++;
            _pos++;
        }
    }

    private bool Match(string s)
    {
        if (_source.AsSpan(_pos).StartsWith(s.AsSpan(), StringComparison.Ordinal))
        {
            foreach (var _ in s) Advance();
            return true;
        }
        return false;
    }

    private bool Peek(string s, int offset = 0) =>
        _pos + offset + s.Length <= _source.Length &&
        _source.AsSpan(_pos + offset).StartsWith(s.AsSpan(), StringComparison.Ordinal);

    private void Expect(string s)
    {
        if (!Match(s))
            throw new TemplateParseException($"Expected '{s}'", _line, _col, s);
    }

    private bool PeekKeyword(string kw)
    {
        int i = _pos;
        foreach (var c in kw) { if (i >= _source.Length || _source[i] != c) return false; i++; }
        return i >= _source.Length || !char.IsLetterOrDigit(_source[i]);
    }

    private void ConsumeKeyword(string kw)
    {
        foreach (var _ in kw) Advance();
    }

    private void SkipWhitespace()
    {
        while (_pos < _source.Length && char.IsWhiteSpace(Current())) Advance();
    }

    //private void Emit(TokenType type, string value)
    //    => _tokens.Add(new Token(type, value, _line, _col));

    // Replace Emit()

    private void Emit(
        TokenType type,
        string value,
        int line,
        int col)
    {
        _tokens.Add(
            new Token(
                type,
                value,
                line,
                col));
    }
}
