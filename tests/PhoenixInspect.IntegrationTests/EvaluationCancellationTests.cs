using System.Diagnostics;
using PhoenixInspect.Inspection;
using PhoenixInspect.Product.DumpQuery;
using Xunit;

namespace PhoenixInspect.IntegrationTests;

/// <summary>
/// Freezes cooperative cancellation: a pre-cancelled token stops before any evidence, a cancellation mid-run
/// unwinds a long fold promptly with the typed outcome instead of an abort, and the BigInteger magnitude bounds
/// keep the calls no checkpoint can reach inside from outliving the request.
/// </summary>
public sealed class EvaluationCancellationTests
{
    /// <summary>Proves a pre-cancelled token produces the typed cancelled outcome, not an exception.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Precancelled_token_reports_the_typed_cancelled_outcome()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var constant = ExpressionEvaluationService.EvaluateConstantValue(
            "1 + 2", cancellationToken: cancellation.Token);
        Assert.Equal(ConstantExpressionStatus.Invalid, constant.Status);
        Assert.Equal(ConstantExpressionEvaluator.CancellationCode, constant.DiagnosticCode);

        var report = ExpressionEvaluationService.EvaluateWithoutSnapshot(
            "1 + 2", cancellationToken: cancellation.Token);
        Assert.Equal("Cancelled", report.Status);
        Assert.Equal(EvaluationSeverity.Stopped, report.Severity);
    }

    /// <summary>Proves a cancellation mid-run unwinds a long fold at a safe boundary, promptly.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Midrun_cancellation_unwinds_a_long_fold_promptly()
    {
        // Without cancellation this nested count performs ~16.8 million predicate folds — well over a minute of
        // work. The cancelled run must observe the request at a fold boundary and return in a small fraction.
        const string LongFold =
            "Enumerable.Range(0, 4096).Count(x => Enumerable.Range(0, 4096).Count(y => x + y > 99999999) > 4096)";
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(250));
        var stopwatch = Stopwatch.StartNew();
        var evaluation = ExpressionEvaluationService.EvaluateConstantValue(
            LongFold, cancellationToken: cancellation.Token);
        stopwatch.Stop();

        Assert.Equal(ConstantExpressionStatus.Invalid, evaluation.Status);
        Assert.Equal(ConstantExpressionEvaluator.CancellationCode, evaluation.DiagnosticCode);
        Assert.True(
            stopwatch.Elapsed < TimeSpan.FromSeconds(10),
            $"The cancelled evaluation took {stopwatch.Elapsed} to unwind.");
    }

    /// <summary>Proves a request cancelled while still queued never starts on the session thread.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public async Task Queued_request_cancelled_before_start_never_runs()
    {
        using var host = new DumpSessionHost();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => host.QueryAsync((_, _) => 0, cancellation.Token));
    }

    /// <summary>
    /// Proves the BigInteger magnitude bounds: a single library call no checkpoint can reach inside carries a
    /// deterministic argument bound instead, so cancellation latency stays bounded by construction.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void BigInteger_magnitude_bounds_stop_uninterruptible_calls()
    {
        var pow = ExpressionEvaluationService.EvaluateConstantValue("BigInteger.Pow(2, 2000000000)");
        Assert.Equal(ConstantExpressionStatus.Invalid, pow.Status);
        Assert.Equal("CONSTANT_NUMERIC_MAGNITUDE_BOUND_EXCEEDED", pow.DiagnosticCode);

        var shift = ExpressionEvaluationService.EvaluateConstantValue("BigInteger.Parse(\"1\") << 2000000000");
        Assert.Equal(ConstantExpressionStatus.Invalid, shift.Status);
        Assert.Equal("CONSTANT_NUMERIC_MAGNITUDE_BOUND_EXCEEDED", shift.DiagnosticCode);

        // Bounded arguments still fold exactly.
        Assert.Equal(
            ConstantExpressionStatus.Exact,
            ExpressionEvaluationService.EvaluateConstantValue("BigInteger.Pow(2, 64)").Status);
    }
}
