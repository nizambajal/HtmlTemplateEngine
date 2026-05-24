namespace TemplateEngine.Lexer;

/// <summary>
/// Enumerates all token types produced by the lexer.
/// </summary>
public enum TokenType
{
    // Structural
    Text,               // Raw HTML/text content
    ExpressionOpen,     // {{
    ExpressionClose,    // }}
    RawExpressionOpen,  // {{{
    RawExpressionClose, // }}}

    // Identifiers and literals
    Identifier,         // Name, Customer.Name, @index
    String,             // "hello" or 'hello'
    Number,             // 42, 3.14
    Boolean,            // true, false
    Null,               // null

    // Operators
    Dot,                // .
    NullCoalesce,       // ?.
    ParentContext,       // ../
    Plus,               // +
    Minus,              // -
    Star,               // *
    Slash,              // /
    Percent,            // %
    Equal,              // ==
    NotEqual,           // !=
    GreaterThan,        // >
    LessThan,           // <
    GreaterThanOrEqual, // >=
    LessThanOrEqual,    // <=
    And,                // &&
    Or,                 // ||
    Not,                // !
    Colon,              // : (for formatters)
    Comma,              // ,
    OpenParen,          // (
    CloseParen,         // )

    // Directives
    Hash,               // #
    Slash2,             // / (closing directive)
    If,                 // if
    Else,               // else
    ElseIf,             // else if
    Foreach,            // foreach
    As,                 // as
    EndIf,              // /if
    EndForeach,         // /foreach

    // Meta
    Format,             // everything after : in {{expr:format}}
    EndOfFile
}

/// <summary>
/// Represents a single lexical token produced by the lexer.
/// </summary>
public sealed class Token
{
    /// <summary>The kind of token.</summary>
    public TokenType Type { get; }

    /// <summary>The raw text value of this token.</summary>
    public string Value { get; }

    /// <summary>Line number in source (1-based).</summary>
    public int Line { get; }

    /// <summary>Column number in source (1-based).</summary>
    public int Column { get; }

    public Token(TokenType type, string value, int line, int column)
    {
        Type = type;
        Value = value;
        Line = line;
        Column = column;
    }

    public override string ToString() => $"[{Type}] '{Value}' @ {Line}:{Column}";
}
