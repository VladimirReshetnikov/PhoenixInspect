using RequestSlot = Interpreter.W8TestTarget.GenericSlot<Interpreter.W8TestTarget.RequestContext>;
using System.Runtime.CompilerServices;

namespace Interpreter.W8TestTarget.ScopeEvidence
{

/// <summary>Supplies an outer alias and frame for nested ImportScope evidence.</summary>
/// <remarks>This is a draft Portable-PDB fixture, not a binding contract.</remarks>
public static class OuterScopeProbe
{
    /// <summary>Calls the inner scope while retaining the outer alias target.</summary>
    /// <param name="profile">The selected target profile.</param>
    /// <param name="request">The named request parameter.</param>
    /// <param name="batch">The named batch parameter.</param>
    /// <param name="importedNested">The imported nested-type witness.</param>
    /// <param name="rvaLength">The compiler-generated RVA payload length.</param>
    /// <returns>A process exit code if the pause unexpectedly returns.</returns>
    public static int Run(
        string profile,
        RequestContext request,
        BatchContext batch,
        StaticImports<RequestContext>.ImportedNested importedNested,
        int rvaLength)
    {
        var outer = RequestSlot.Current;
        var result = InnerScope.InnerScopeProbe.Run(profile, request, batch, importedNested, rvaLength);
        GC.KeepAlive(outer);
        return result;
    }
}

namespace InnerScope
{
    using RequestSlot = Interpreter.W8TestTarget.GenericSlot<Interpreter.W8TestTarget.BatchContext>;

    /// <summary>Supplies an inner same-name alias and complete lexical-name catalog.</summary>
    /// <remarks>This is a draft Portable-PDB fixture, not a binding contract.</remarks>
    public static class InnerScopeProbe
    {
        /// <summary>Pauses with active locals after using the inner alias.</summary>
        /// <param name="profile">The selected target profile.</param>
        /// <param name="request">The named request parameter.</param>
        /// <param name="batch">The named batch parameter.</param>
        /// <param name="importedNested">The imported nested-type witness.</param>
        /// <param name="rvaLength">The compiler-generated RVA payload length.</param>
        /// <returns>A process exit code if the pause unexpectedly returns.</returns>
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public static int Run(
            string profile,
            RequestContext request,
            BatchContext batch,
            StaticImports<RequestContext>.ImportedNested importedNested,
            int rvaLength)
        {
            var shadowedAliasValue = RequestSlot.Current;
            var localRequest = request;
            var localBatch = batch;
            var localNumber = RequestSlot.Sentinel;

            Console.WriteLine("READY");
            Console.Out.Flush();
            Thread.Sleep(Timeout.Infinite);

            GC.KeepAlive(profile);
            GC.KeepAlive(shadowedAliasValue);
            GC.KeepAlive(localRequest);
            GC.KeepAlive(localBatch);
            GC.KeepAlive(localNumber);
            GC.KeepAlive(importedNested);
            GC.KeepAlive(rvaLength);
            return 0;
        }
    }
}
}
