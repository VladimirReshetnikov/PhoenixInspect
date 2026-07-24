using System.Runtime.CompilerServices;

// The compilation-unit alias names the outer declaration; the inner namespace redeclares the same spelling.
using ScopedRequestSlot = PhoenixInspect.W8RequestShapeTarget.OuterScopedSlot;

namespace PhoenixInspect.W8RequestShapeTarget.Inner
{
    using ScopedRequestSlot = PhoenixInspect.W8RequestShapeTarget.Inner.InnerScopedSlot;

    /// <summary>Owns the inner-scope declaration that the innermost alias must reach.</summary>
    /// <remarks>This emitted draft fixture type is a physical oracle, not a product contract.</remarks>
    public static class InnerScopedSlot
    {
        /// <summary>Stores the inner-scope sentinel selected by the innermost alias declaration.</summary>
        public static int Sentinel;
    }

    /// <summary>Pauses inside an inner alias scope whose alias shadows an identically spelled outer alias.</summary>
    /// <remarks>This is a draft frame probe and not a frame-value product contract.</remarks>
    public static class InnerAliasProbe
    {
        /// <summary>Enters the inner alias scope and pauses for the full dump.</summary>
        /// <param name="profile">The selected truth-gate profile.</param>
        /// <returns>A process exit code if the pause unexpectedly returns.</returns>
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public static int Run(string profile)
        {
            var scoped = ScopedRequestSlot.Sentinel;
            return RequestPause.WaitForDump(profile, scoped);
        }
    }
}

namespace PhoenixInspect.W8RequestShapeTarget.Outer
{
    /// <summary>Pauses inside a scope whose only alias for the shared spelling is the outer declaration.</summary>
    /// <remarks>This is a draft frame probe and not a frame-value product contract.</remarks>
    public static class OuterAliasProbe
    {
        /// <summary>Enters the outer alias scope and pauses for the full dump.</summary>
        /// <param name="profile">The selected truth-gate profile.</param>
        /// <returns>A process exit code if the pause unexpectedly returns.</returns>
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public static int Run(string profile)
        {
            var scoped = ScopedRequestSlot.Sentinel;
            return RequestPause.WaitForDump(profile, scoped);
        }
    }
}
