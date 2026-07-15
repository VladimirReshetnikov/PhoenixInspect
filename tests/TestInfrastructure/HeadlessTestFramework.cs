using System.Reflection;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Interpreter.Tests.Infrastructure;

/// <summary>
/// Supplies an execution hook that reasserts the test host's non-interactive Windows failure policy immediately before
/// xUnit schedules test cases. This draft test-infrastructure framework complements the earlier process-wrapper and
/// module-initializer protections; it is not a product execution contract.
/// </summary>
public sealed class HeadlessTestFramework : XunitTestFramework
{
    /// <summary>
    /// Initializes the xUnit framework that creates a headless-policy-aware executor for the test assembly.
    /// </summary>
    /// <param name="messageSink">The diagnostic sink supplied by the xUnit runner.</param>
    public HeadlessTestFramework(IMessageSink messageSink)
        : base(messageSink)
    {
    }

    internal static bool IsExecutionPolicyInitialized { get; private set; }

    /// <summary>
    /// Creates the draft execution-stage adapter that reapplies and verifies the headless process policy immediately
    /// before the supplied test cases are scheduled.
    /// </summary>
    /// <param name="assemblyName">The identity of the test assembly that xUnit will execute.</param>
    /// <returns>An xUnit executor with the additional headless-policy boundary.</returns>
    protected override ITestFrameworkExecutor CreateExecutor(AssemblyName assemblyName) =>
        new HeadlessTestFrameworkExecutor(assemblyName, SourceInformationProvider, DiagnosticMessageSink);

    internal static void MarkExecutionPolicyInitialized() => IsExecutionPolicyInitialized = true;
}

internal sealed class HeadlessTestFrameworkExecutor : XunitTestFrameworkExecutor
{
    internal HeadlessTestFrameworkExecutor(
        AssemblyName assemblyName,
        ISourceInformationProvider sourceInformationProvider,
        IMessageSink diagnosticMessageSink)
        : base(assemblyName, sourceInformationProvider, diagnosticMessageSink)
    {
    }

    protected override void RunTestCases(
        IEnumerable<IXunitTestCase> testCases,
        IMessageSink executionMessageSink,
        ITestFrameworkExecutionOptions executionOptions)
    {
        // This is xUnit v2's last deterministic extension boundary before its assembly runner schedules test cases.
        HeadlessTestProcess.EnsureCurrentPolicy();
        HeadlessTestFramework.MarkExecutionPolicyInitialized();
        base.RunTestCases(testCases, executionMessageSink, executionOptions);
    }
}
