extern alias wflib;

using System.Runtime.CompilerServices;
using System.Runtime.Loader;

namespace Interpreter.W8WorkflowShapeTarget;

internal static class Program
{
    private static int Main(string[] args)
    {
        if (Environment.GetEnvironmentVariable("OPENAI_API_KEY") is not null ||
            Environment.GetEnvironmentVariable("GITHUB_TOKEN") is not null ||
            Environment.GetEnvironmentVariable("GH_TOKEN") is not null ||
            Environment.GetEnvironmentVariable("INTERPRETER_TEST_ARTIFACT_CANARY") is not null)
        {
            Console.WriteLine("UNEXPECTED_ENVIRONMENT");
            Console.Out.Flush();
            return 90;
        }

        if (args is not ["--truth-gate", var profile])
        {
            Console.WriteLine("INVALID_ARGUMENTS");
            Console.Out.Flush();
            return 91;
        }

        return WorkflowShapeGate.Run(profile);
    }
}

/// <summary>Holds the sole pause the workflow-shape target uses to signal dump readiness.</summary>
/// <remarks>This is a draft target entry point and not a product API.</remarks>
public static class WorkflowPause
{
    /// <summary>Signals readiness and pauses so an external writer can capture one full dump.</summary>
    /// <param name="profile">The selected truth-gate profile.</param>
    /// <param name="retained">One retained fixture value the pause keeps alive.</param>
    /// <returns>A process exit code if the pause unexpectedly returns.</returns>
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    public static int WaitForDump(string profile, int retained)
    {
        Console.WriteLine("READY");
        Console.Out.Flush();
        Thread.Sleep(Timeout.Infinite);
        GC.KeepAlive(profile);
        GC.KeepAlive(retained);
        return 0;
    }
}

/// <summary>Materializes the workflow-shape object graph and enters the selected truth-gate profile.</summary>
/// <remarks>This is a draft target entry point and not a product API.</remarks>
public static class WorkflowShapeGate
{
    private static AssemblyLoadContext? retainedSecondLoadContext;
    private static object? retainedSecondDefinitionValue;

    /// <summary>Initializes the workflow-shape physical fixture and enters the selected profile.</summary>
    /// <param name="profile">The predeclared truth-gate profile of exactly one corpus incident.</param>
    /// <returns>A process exit code if the target does not remain paused.</returns>
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    public static int Run(string profile)
    {
        var step = new StepContext("workflow-step-label");

        Stage<StepContext>.Current = step;
        Stage<StepContext[]>.Current = null;
        Stage<string>.Current = "workflow-shape-307";

        BaseStage.Sentinel = 0x41_00_04_0C;
        DuplicatedStageStatics.Sentinel = 0x41_00_04_18;
        FirstAmbiguousStage.Sentinel = 0x41_00_04_14;
        SecondAmbiguousStage.Sentinel = 0x41_00_04_15;

        wflib::Interpreter.W8AliasTarget.IExternalInterfaceSlot<
            wflib::Interpreter.W8AliasTarget.ExternalRequestContext>.Sentinel = 0x41_00_04_08;
        wflib::Interpreter.W8AliasTarget.ExternalSlot<
            wflib::Interpreter.W8AliasTarget.ExternalRequestContext>.Current =
            new wflib::Interpreter.W8AliasTarget.ExternalRequestContext("workflow-external-311");

        MaterializeSecondDefinition();

        return profile switch
        {
            "workflow-var-substitution" => WorkflowPause.WaitForDump(profile, Stage<StepContext>.Current!.Label.Length),
            "workflow-extern-alias-interface" => WorkflowPause.WaitForDump(
                profile,
                wflib::Interpreter.W8AliasTarget.IExternalInterfaceSlot<
                    wflib::Interpreter.W8AliasTarget.ExternalRequestContext>.Sentinel),
            "workflow-member-hiding" => WorkflowPause.WaitForDump(profile, HidingStage.Sentinel),
            "workflow-array-argument-null" => WorkflowPause.WaitForDump(profile, BaseStage.Sentinel),
            "workflow-alias-ambiguity" => AmbiguousAliasProbe.Run(profile),
            "workflow-definition-ambiguity" => WorkflowPause.WaitForDump(profile, DuplicatedStageStatics.Sentinel),
            "workflow-reference-target-conflict" => WorkflowPause.WaitForDump(profile, step.Label.Length),
            "workflow-enum-literal" => GroundLiteralProbe.Run<StepContext>(profile),
            "workflow-frame-value" => FrameValueProbe.Run(profile, 0x41_00_04_23),
            _ => 92,
        };
    }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    private static void MaterializeSecondDefinition()
    {
        if (retainedSecondLoadContext is not null)
        {
            return;
        }

        var loadContext = new AssemblyLoadContext("w8-workflow-second-definition", isCollectible: true);
        var loadedAssembly = loadContext.LoadFromAssemblyPath(typeof(WorkflowShapeGate).Assembly.Location);
        var duplicated = loadedAssembly.GetType(
            "Interpreter.W8WorkflowShapeTarget.DuplicatedStageStatics",
            throwOnError: true,
            ignoreCase: false)!;
        var sentinel = duplicated.GetField(nameof(DuplicatedStageStatics.Sentinel))!;
        sentinel.SetValue(null, 0x41_00_04_19);

        retainedSecondLoadContext = loadContext;
        retainedSecondDefinitionValue = sentinel.GetValue(null);
    }
}
