using Interpreter.Product.DumpQuery;
using Xunit;

namespace Interpreter.IntegrationTests;

/// <summary>Exercises the dump-free admission boundary for the deliberately closed W2 query grammar.</summary>
public sealed class DumpQueryParserTests
{
    /// <summary>Checks the complete admitted syntax set and decoded literal behavior.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Parser_accepts_only_the_bounded_root_field_shape()
    {
        var field = DumpQueryParser.Parse("root.Marker", "root");
        Assert.True(field.IsSuccess);
        Assert.Equal("root", field.Query!.RootName);
        Assert.Equal("Marker", field.Query.FieldName);
        Assert.Null(field.Query.CoalesceLiteral);

        var textFallback = DumpQueryParser.Parse("root.Optional ?? \"a\\n\\\"b\\\\c\"", "root");
        Assert.True(textFallback.IsSuccess);
        Assert.Equal(DumpQueryLiteralKind.String, textFallback.Query!.CoalesceLiteral!.Kind);
        Assert.Equal("a\n\"b\\c", textFallback.Query.CoalesceLiteral.StringValue);

        var intFallback = DumpQueryParser.Parse("root.Optional ?? -2147483648", "root");
        Assert.True(intFallback.IsSuccess);
        Assert.Equal(DumpQueryLiteralKind.Int32, intFallback.Query!.CoalesceLiteral!.Kind);
        Assert.Equal(int.MinValue, intFallback.Query.CoalesceLiteral.Int32Value);

        var nullFallback = DumpQueryParser.Parse("root.Optional ?? null", "root");
        Assert.True(nullFallback.IsSuccess);
        Assert.Equal(DumpQueryLiteralKind.Null, nullFallback.Query!.CoalesceLiteral!.Kind);

        var maximumNames = DumpQueryParser.Parse(
            $"{new string('r', DumpQueryParser.MaximumIdentifierLength)}.{new string('f', DumpQueryParser.MaximumIdentifierLength)}",
            new string('r', DumpQueryParser.MaximumIdentifierLength));
        Assert.True(maximumNames.IsSuccess);

        var secretBearingFailure = DumpQueryParser.Parse("root.Secret_9283()", "root");
        Assert.False(secretBearingFailure.IsSuccess);
        Assert.DoesNotContain("Secret_9283", secretBearingFailure.DiagnosticMessage!, StringComparison.Ordinal);
    }

    /// <summary>Checks stable, secret-safe rejection codes for malformed, oversized, and expanded syntax.</summary>
    /// <param name="expression">The untrusted input scenario.</param>
    /// <param name="rootName">The configured root name.</param>
    /// <param name="expectedCode">The stable admission failure code.</param>
    [Theory]
    [Trait("Category", "Fast")]
    [MemberData(nameof(InvalidQueries))]
    public void Parser_rejects_expanded_or_malformed_syntax(
        string? expression,
        string? rootName,
        string expectedCode)
    {
        var result = DumpQueryParser.Parse(expression, rootName);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Query);
        Assert.Equal(expectedCode, result.DiagnosticCode);
        Assert.False(string.IsNullOrWhiteSpace(result.DiagnosticMessage));
    }

    /// <summary>Gets malformed and out-of-scope grammar scenarios spanning every deterministic input cap.</summary>
    public static IEnumerable<object?[]> InvalidQueries =>
    [
        [null, "root", "QUERY_EXPRESSION_REQUIRED"],
        ["   ", "root", "QUERY_EXPRESSION_REQUIRED"],
        [new string('x', DumpQueryParser.MaximumExpressionLength + 1), "root", "QUERY_EXPRESSION_TOO_LONG"],
        ["root.Field", "bad-root", "QUERY_ROOT_NAME_INVALID"],
        ["root.Field", new string('r', DumpQueryParser.MaximumIdentifierLength + 1), "QUERY_ROOT_NAME_TOO_LONG"],
        [$"{new string('r', DumpQueryParser.MaximumIdentifierLength + 1)}.Field", "root", "QUERY_IDENTIFIER_TOO_LONG"],
        [$"root.{new string('f', DumpQueryParser.MaximumIdentifierLength + 1)}", "root", "QUERY_IDENTIFIER_TOO_LONG"],
        ["Root.Field", "root", "QUERY_ROOT_MISMATCH"],
        ["root", "root", "QUERY_MEMBER_ACCESS_REQUIRED"],
        ["root[0].Field", "root", "QUERY_MEMBER_ACCESS_REQUIRED"],
        ["root?.Field", "root", "QUERY_SYNTAX_UNSUPPORTED"],
        ["  root ?. Field  ", "root", "QUERY_SYNTAX_UNSUPPORTED"],
        ["root.Field()", "root", "QUERY_SYNTAX_UNSUPPORTED"],
        ["root.Field.Other", "root", "QUERY_SYNTAX_UNSUPPORTED"],
        ["root.Field + 1", "root", "QUERY_SYNTAX_UNSUPPORTED"],
        ["root.Field ??", "root", "QUERY_LITERAL_REQUIRED"],
        ["root.Field ?? true", "root", "QUERY_LITERAL_UNSUPPORTED"],
        ["root.Field ?? 2147483648", "root", "QUERY_INT32_LITERAL_INVALID"],
        ["root.Field ?? \"unterminated", "root", "QUERY_STRING_LITERAL_INVALID"],
        ["root.Field ?? \"bad\\q\"", "root", "QUERY_STRING_ESCAPE_UNSUPPORTED"],
        [$"root.Field ?? \"{new string('v', DumpQueryParser.MaximumStringLiteralLength + 1)}\"", "root", "QUERY_STRING_LITERAL_TOO_LONG"],
    ];
}
