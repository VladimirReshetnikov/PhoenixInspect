extern alias requestlib;

using System.Runtime.CompilerServices;
using ExternalRequestSlot = requestlib::Interpreter.W8AliasTarget.ExternalSlot<requestlib::Interpreter.W8AliasTarget.ExternalRequestContext>;
using RequestSlot = Interpreter.W8TestTarget.GenericSlot<Interpreter.W8TestTarget.RequestContext>;
using RequestVectorSlot = Interpreter.W8TestTarget.GenericSlot<Interpreter.W8TestTarget.RequestContext[]>;
using static Interpreter.W8TestTarget.StaticImports<Interpreter.W8TestTarget.RequestContext>;

namespace Interpreter.W8TestTarget;

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

        return W8TruthGate.Run(profile);
    }
}

/// <summary>Materializes every mandatory W8.1 construction and value before the full dump is written.</summary>
/// <remarks>This is a draft target entry point and not a product API.</remarks>
public static class W8TruthGate
{
    /// <summary>Initializes the physical fixture and enters the selected frame profile.</summary>
    /// <param name="profile">The predeclared selected-frame profile.</param>
    /// <returns>A process exit code if the target does not remain paused.</returns>
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    public static int Run(string profile)
    {
        var request = new RequestContext("request-17");
        var batch = new BatchContext("batch-29");
        var coordinator = new CoordinatorContext("coordinator-41");
        var workflow = new WorkflowContext("workflow-53");

        GenericSlot<RequestContext>.Sentinel = 0x11017A01;
        GenericSlot<RequestContext>.Current = request;
        GenericSlot<RequestContext>.ThreadSentinel = 0x11017A02;
        GenericSlot<BatchContext>.Sentinel = 0x22027A01;
        GenericSlot<BatchContext>.Current = batch;
        GenericSlot<ValueContext>.Sentinel = 0x33037A01;
        GenericSlot<ValueContext>.Current = new ValueContext(0x33037A02);
        GenericSlot<RequestContext[]>.Sentinel = 0x44047A01;
        GenericSlot<RequestContext[]>.Current = [request];

        GenericStructSlot<CoordinatorContext>.Sentinel = 0x55057A01;
        GenericStructSlot<CoordinatorContext>.Current = coordinator;
        IGenericInterfaceSlot<WorkflowContext>.Sentinel = 0x66067A01;
        IGenericInterfaceSlot<WorkflowContext>.Current = workflow;

        Outer<RequestContext>.Middle.Inner<BatchContext>.OuterValue = request;
        Outer<RequestContext>.Middle.Inner<BatchContext>.InnerValue = batch;
        Outer<RequestContext>.Middle.Inner<BatchContext>.Count = 0x77077A01;
        DerivedSlot<RequestContext>.Value = [new List<RequestContext> { request }];

        ConstructibleSlot<ConstructibleContext>.Sentinel = unchecked((int)0x88087A01);
        UnmanagedSlot<ValueContext>.Sentinel = unchecked((int)0x99097A01);
        DependentSlot<object, RequestContext>.Sentinel = unchecked((int)0xAA0A7A01);
        StaticImports<RequestContext>.ImportedSentinel = unchecked((int)0xBB0B7A01);

        ExternalRequestSlot.Sentinel = unchecked((int)0xCC0C7A01);
        ExternalRequestSlot.Current = new requestlib::Interpreter.W8AliasTarget.ExternalRequestContext("external-67");
        requestlib::Interpreter.W8AliasTarget.IExternalInterfaceSlot<
            requestlib::Interpreter.W8AliasTarget.ExternalRequestContext>.Sentinel = unchecked((int)0xDD0D7A01);

        var retainedRva = PrimitiveStorage.RvaBytes.Length;
        var importedNested = new ImportedNested("imported-nested");
        return profile switch
        {
            "generic-frame" => W8FrameProbe.Run<RequestContext, BatchContext>(
                profile,
                request,
                PrimitiveStorage.Int32,
                importedNested,
                retainedRva),
            "shadow-frame" => ScopeEvidence.OuterScopeProbe.Run(profile, request, batch, importedNested, retainedRva),
            _ => 92,
        };
    }
}

/// <summary>Supplies a public parameterless-constructor constraint witness.</summary>
/// <remarks>This is a draft emitted fixture type, not a product contract.</remarks>
public sealed class ConstructibleContext
{
    /// <summary>Initializes the constraint witness.</summary>
    public ConstructibleContext()
    {
    }
}

/// <summary>Supplies selected generic-type, generic-method, parameter, local, and import evidence.</summary>
/// <remarks>This is a draft frame probe and not a frame-value product contract.</remarks>
public static class W8FrameProbe
{
    /// <summary>Pauses inside one generic method after retaining all named values.</summary>
    /// <typeparam name="TType">The exact declaring-type-style argument used by the fixture.</typeparam>
    /// <typeparam name="TMethod">The exact method argument used by the fixture.</typeparam>
    /// <param name="profile">The selected target profile.</param>
    /// <param name="request">The named reference parameter.</param>
    /// <param name="number">The named value parameter.</param>
    /// <param name="importedNested">The nested type imported through constructed <c>using static</c>.</param>
    /// <param name="rvaLength">The compiler-generated RVA payload length.</param>
    /// <returns>A process exit code if the pause unexpectedly returns.</returns>
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    public static int Run<TType, TMethod>(
        string profile,
        RequestContext request,
        int number,
        ImportedNested importedNested,
        int rvaLength)
    {
        var localRequest = request;
        var localNumber = number ^ RequestSlot.Sentinel;
        var localVector = RequestVectorSlot.Current;
        var localExternal = ExternalRequestSlot.Current;
        var localImported = ImportedSentinel;
        var typeWitness = typeof(TType);
        var methodWitness = typeof(TMethod);

        Console.WriteLine("READY");
        Console.Out.Flush();
        Thread.Sleep(Timeout.Infinite);

        GC.KeepAlive(profile);
        GC.KeepAlive(localRequest);
        GC.KeepAlive(localNumber);
        GC.KeepAlive(localVector);
        GC.KeepAlive(localExternal);
        GC.KeepAlive(localImported);
        GC.KeepAlive(importedNested);
        GC.KeepAlive(rvaLength);
        GC.KeepAlive(typeWitness);
        GC.KeepAlive(methodWitness);
        return 0;
    }
}
