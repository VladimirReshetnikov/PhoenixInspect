using System.Text;
using Interpreter.Core.Abstractions;
using Interpreter.Product.DumpQuery;
using Xunit;

namespace Interpreter.IntegrationTests;

/// <summary>Verifies replay-stable and display-safe behavior of the closed dump-query value union.</summary>
public sealed class DumpQueryValueTests
{
    /// <summary>Checks that canonical projections preserve exact UTF-16 while display text remains redacted.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Value_projection_is_injective_deterministic_and_redacted()
    {
        var integer = DumpQueryValue.FromInt32(-42);
        var text = DumpQueryValue.FromString("A\uD83D");
        var @null = DumpQueryValue.FromNull();

        Assert.Equal("i32:-42", integer.ToCanonicalReplayProjection());
        Assert.Equal("s16:0041D83D", text.ToCanonicalReplayProjection());
        Assert.Equal("null", @null.ToCanonicalReplayProjection());
        Assert.Equal("Int32(redacted)", integer.ToString());
        Assert.Equal("String(length=2)", text.ToString());
        Assert.DoesNotContain("-42", integer.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("A", text.ToString(), StringComparison.Ordinal);

        var result = EvaluationResult<DumpQueryValue>.Create(
            EvaluationSemanticMode.DerivedQuery,
            EvaluationCompletionStatus.Completed,
            EvaluationCompleteness.Complete,
            EvaluationEvidenceStatus.Exact,
            EvaluationEffectStatus.None,
            text);
        var first = EvaluationResultReplay.SerializeCanonical(
            result,
            static value => value.ToCanonicalReplayProjection());
        var second = EvaluationResultReplay.SerializeCanonical(
            result,
            static value => value.ToCanonicalReplayProjection());

        Assert.Equal(first, second);
        Assert.Contains("s16:0041D83D", Encoding.UTF8.GetString(first), StringComparison.Ordinal);
        Assert.Equal(
            EvaluationResultReplay.ComputeSha256(result, static value => value.ToCanonicalReplayProjection()),
            EvaluationResultReplay.ComputeSha256(result, static value => value.ToCanonicalReplayProjection()));
    }
}
