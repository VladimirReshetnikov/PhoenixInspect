using PhoenixInspect.Inspection;
using Xunit;

namespace PhoenixInspect.IntegrationTests;

/// <summary>
/// Proves the sessionless evaluation entry: the evidence-free constant subset folds with no snapshot loaded,
/// constant-domain errors keep their typed analogues, and anything beyond the subset produces the typed
/// no-snapshot stop instead of an attempt.
/// </summary>
/// <remarks>
/// This entry consults no session surface at all, so loaded-session behavior is untouched by construction; the
/// frozen watch and static-field lanes prove that side. What these arms freeze is the immediate window's
/// before-any-dump contract.
/// </remarks>
public sealed class SessionlessEvaluationTests
{
    /// <summary>A pure constant folds, a constant-domain error stays typed, and a dump name refuses.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Sessionless_entry_folds_constants_and_refuses_dump_names()
    {
        var folded = ExpressionEvaluationService.EvaluateWithoutSnapshot("(2 + 3) * 7");
        var divided = ExpressionEvaluationService.EvaluateWithoutSnapshot("1 / 0");
        var dumpName = ExpressionEvaluationService.EvaluateWithoutSnapshot(
            "global::PhoenixInspect.W8TestTarget.GenericSlot<int>.Sentinel");
        Assert.Equal(EvaluationSeverity.Exact, folded.Severity);
        Assert.Equal("35", folded.Value);
        Assert.Equal("Int32 · checked constant folding", folded.ValueKind);

        // The constant-domain error keeps its typed exception analogue with no snapshot, exactly as it would
        // with one: the evidence-free subset owns its own error vocabulary.
        Assert.Equal(EvaluationSeverity.Stopped, divided.Severity);
        Assert.Equal("Blocked", divided.Status);
        Assert.Contains(divided.Diagnostics, static row => row.Code == "System.DivideByZeroException");

        // A name that reaches for target state refuses with the one typed no-snapshot stop; nothing is guessed.
        Assert.Equal(EvaluationSeverity.Stopped, dumpName.Severity);
        Assert.Equal("Unavailable", dumpName.Status);
        Assert.Contains(
            dumpName.Diagnostics,
            static row => row.Code == "EXPLORER_EVALUATION_REQUIRES_LOADED_SNAPSHOT");
    }
}
