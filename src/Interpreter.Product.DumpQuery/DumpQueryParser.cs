using System.Globalization;
using System.Text;

namespace Interpreter.Product.DumpQuery;

internal enum DumpQueryLiteralKind
{
    Null,
    Int32,
    String,
}

internal sealed record DumpQueryLiteral(DumpQueryLiteralKind Kind, int Int32Value, string? StringValue);

internal sealed record ParsedDumpQuery(
    string RootName,
    string FieldName,
    DumpQueryLiteral? CoalesceLiteral);

internal sealed record DumpQueryParseResult(
    ParsedDumpQuery? Query,
    string? DiagnosticCode,
    string? DiagnosticMessage)
{
    internal bool IsSuccess => Query is not null;
}

internal static class DumpQueryParser
{
    internal const int MaximumExpressionLength = 512;
    internal const int MaximumIdentifierLength = 64;
    internal const int MaximumStringLiteralLength = 256;

    internal static DumpQueryParseResult Parse(string? expression, string? expectedRootName)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return Failure("QUERY_EXPRESSION_REQUIRED", "A dump-query expression is required.");
        }

        if (expression.Length > MaximumExpressionLength)
        {
            return Failure("QUERY_EXPRESSION_TOO_LONG", "The dump-query expression exceeds the deterministic length limit.");
        }

        if (!IsValidIdentifier(expectedRootName, out var rootIssue))
        {
            return rootIssue == IdentifierIssue.TooLong
                ? Failure("QUERY_ROOT_NAME_TOO_LONG", "The configured root name exceeds the deterministic identifier limit.")
                : Failure("QUERY_ROOT_NAME_INVALID", "The configured root name is not a supported identifier.");
        }

        var reader = new Reader(expression);
        reader.SkipWhiteSpace();
        if (!reader.TryReadIdentifier(out var rootName, out var identifierIssue))
        {
            return IdentifierFailure(identifierIssue);
        }

        if (!string.Equals(rootName, expectedRootName, StringComparison.Ordinal))
        {
            return Failure("QUERY_ROOT_MISMATCH", "The expression does not reference the configured root name exactly.");
        }

        reader.SkipWhiteSpace();
        if (reader.TryRead("?."))
        {
            return Failure(
                "QUERY_SYNTAX_UNSUPPORTED",
                "The expression contains syntax outside the supported dump-query grammar.");
        }

        if (!reader.TryRead("."))
        {
            return Failure("QUERY_MEMBER_ACCESS_REQUIRED", "The supported grammar requires one instance-field access.");
        }

        reader.SkipWhiteSpace();
        if (!reader.TryReadIdentifier(out var fieldName, out identifierIssue))
        {
            return IdentifierFailure(identifierIssue);
        }

        reader.SkipWhiteSpace();
        DumpQueryLiteral? literal = null;
        if (reader.TryRead("??"))
        {
            reader.SkipWhiteSpace();
            var literalResult = reader.TryReadLiteral();
            if (!literalResult.IsSuccess)
            {
                return Failure(literalResult.DiagnosticCode!, literalResult.DiagnosticMessage!);
            }

            literal = literalResult.Literal;
            reader.SkipWhiteSpace();
        }

        if (!reader.IsAtEnd)
        {
            return Failure("QUERY_SYNTAX_UNSUPPORTED", "The expression contains syntax outside the supported dump-query grammar.");
        }

        return new DumpQueryParseResult(
            new ParsedDumpQuery(rootName!, fieldName!, literal),
            null,
            null);
    }

    private static DumpQueryParseResult IdentifierFailure(IdentifierIssue issue) => issue switch
    {
        IdentifierIssue.TooLong => Failure(
            "QUERY_IDENTIFIER_TOO_LONG",
            "An expression identifier exceeds the deterministic identifier limit."),
        _ => Failure(
            "QUERY_IDENTIFIER_INVALID",
            "The expression contains a missing or unsupported identifier."),
    };

    private static DumpQueryParseResult Failure(string code, string message) => new(null, code, message);

    private static bool IsValidIdentifier(string? value, out IdentifierIssue issue)
    {
        if (string.IsNullOrEmpty(value) || !IsIdentifierStart(value[0]))
        {
            issue = IdentifierIssue.Invalid;
            return false;
        }

        if (value.Length > MaximumIdentifierLength)
        {
            issue = IdentifierIssue.TooLong;
            return false;
        }

        for (var index = 1; index < value.Length; index++)
        {
            if (!IsIdentifierPart(value[index]))
            {
                issue = IdentifierIssue.Invalid;
                return false;
            }
        }

        issue = IdentifierIssue.None;
        return true;
    }

    private static bool IsIdentifierStart(char value) =>
        value is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or '_';

    private static bool IsIdentifierPart(char value) => IsIdentifierStart(value) || value is >= '0' and <= '9';

    private enum IdentifierIssue
    {
        None,
        Invalid,
        TooLong,
    }

    private ref struct Reader
    {
        private readonly ReadOnlySpan<char> _text;
        private int _position;

        internal Reader(string text)
        {
            _text = text.AsSpan();
            _position = 0;
        }

        internal bool IsAtEnd => _position == _text.Length;

        internal void SkipWhiteSpace()
        {
            while (_position < _text.Length && char.IsWhiteSpace(_text[_position]))
            {
                _position++;
            }
        }

        internal bool TryRead(string token)
        {
            if (!_text[_position..].StartsWith(token, StringComparison.Ordinal))
            {
                return false;
            }

            _position += token.Length;
            return true;
        }

        internal bool TryReadIdentifier(out string? value, out IdentifierIssue issue)
        {
            value = null;
            if (_position >= _text.Length || !IsIdentifierStart(_text[_position]))
            {
                issue = IdentifierIssue.Invalid;
                return false;
            }

            var start = _position++;
            while (_position < _text.Length && IsIdentifierPart(_text[_position]))
            {
                _position++;
            }

            var length = _position - start;
            if (length > MaximumIdentifierLength)
            {
                issue = IdentifierIssue.TooLong;
                return false;
            }

            value = _text.Slice(start, length).ToString();
            issue = IdentifierIssue.None;
            return true;
        }

        internal LiteralReadResult TryReadLiteral()
        {
            if (_position >= _text.Length)
            {
                return LiteralReadResult.Failure(
                    "QUERY_LITERAL_REQUIRED",
                    "Null coalescing requires one supported literal.");
            }

            if (_text[_position] == '"')
            {
                return TryReadStringLiteral();
            }

            if (_text[_position..].StartsWith("null", StringComparison.Ordinal) &&
                (_position + 4 == _text.Length || !IsIdentifierPart(_text[_position + 4])))
            {
                _position += 4;
                return LiteralReadResult.Success(new DumpQueryLiteral(DumpQueryLiteralKind.Null, 0, null));
            }

            var start = _position;
            if (_text[_position] is '+' or '-')
            {
                _position++;
            }

            var digitStart = _position;
            while (_position < _text.Length && _text[_position] is >= '0' and <= '9')
            {
                _position++;
            }

            if (_position == digitStart)
            {
                _position = start;
                return LiteralReadResult.Failure(
                    "QUERY_LITERAL_UNSUPPORTED",
                    "The expression uses a literal outside the supported null, Int32, and string set.");
            }

            var token = _text[start.._position];
            if (!int.TryParse(token, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value))
            {
                return LiteralReadResult.Failure(
                    "QUERY_INT32_LITERAL_INVALID",
                    "The integer literal is outside the supported Int32 range.");
            }

            return LiteralReadResult.Success(new DumpQueryLiteral(DumpQueryLiteralKind.Int32, value, null));
        }

        private LiteralReadResult TryReadStringLiteral()
        {
            _position++;
            var builder = new StringBuilder();
            while (_position < _text.Length)
            {
                var character = _text[_position++];
                if (character == '"')
                {
                    return LiteralReadResult.Success(new DumpQueryLiteral(
                        DumpQueryLiteralKind.String,
                        0,
                        builder.ToString()));
                }

                if (character is '\r' or '\n')
                {
                    return LiteralReadResult.Failure(
                        "QUERY_STRING_LITERAL_INVALID",
                        "String literals cannot contain unescaped line breaks.");
                }

                if (character == '\\')
                {
                    if (_position >= _text.Length)
                    {
                        return LiteralReadResult.Failure(
                            "QUERY_STRING_LITERAL_INVALID",
                            "The string literal has an incomplete escape sequence.");
                    }

                    character = _text[_position++] switch
                    {
                        '"' => '"',
                        '\\' => '\\',
                        'n' => '\n',
                        'r' => '\r',
                        't' => '\t',
                        '0' => '\0',
                        _ => '\uffff',
                    };
                    if (character == '\uffff')
                    {
                        return LiteralReadResult.Failure(
                            "QUERY_STRING_ESCAPE_UNSUPPORTED",
                            "The string literal contains an unsupported escape sequence.");
                    }
                }

                builder.Append(character);
                if (builder.Length > MaximumStringLiteralLength)
                {
                    return LiteralReadResult.Failure(
                        "QUERY_STRING_LITERAL_TOO_LONG",
                        "The string literal exceeds the deterministic decoded-length limit.");
                }
            }

            return LiteralReadResult.Failure(
                "QUERY_STRING_LITERAL_INVALID",
                "The string literal is not terminated.");
        }
    }

    internal sealed record LiteralReadResult(
        DumpQueryLiteral? Literal,
        string? DiagnosticCode,
        string? DiagnosticMessage)
    {
        internal bool IsSuccess => Literal is not null;

        internal static LiteralReadResult Success(DumpQueryLiteral literal) => new(literal, null, null);

        internal static LiteralReadResult Failure(string code, string message) => new(null, code, message);
    }
}
