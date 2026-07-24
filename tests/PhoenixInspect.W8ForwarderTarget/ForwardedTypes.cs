using System.Runtime.CompilerServices;
using PhoenixInspect.W8AliasTarget;

[assembly: TypeForwardedTo(typeof(ForwardedRequestContext))]

namespace PhoenixInspect.W8ForwarderTarget;

/// <summary>
/// Supplies cross-assembly member-access flags from an assembly that grants the consumer no friend visibility.
/// </summary>
/// <remarks>
/// This metadata fixture keeps public, family, combined-family, assembly, and private rows on one owner.
/// </remarks>
public class NonFriendAccessibilityOwner
{
    /// <summary>Stores the public candidate.</summary>
    public static int PublicSentinel = 0x1E017A01;

    /// <summary>Stores the family candidate.</summary>
    protected static int FamilySentinel = 0x1E017A02;

    /// <summary>Stores the assembly-or-family candidate.</summary>
    protected internal static int FamilyOrAssemblySentinel = 0x1E017A03;

    /// <summary>Stores the assembly-and-family candidate.</summary>
    private protected static int FamilyAndAssemblySentinel = 0x1E017A04;

    internal static int AssemblySentinel = 0x1E017A05;

    private static int PrivateSentinel = 0x1E017A06;

    /// <summary>Reads every candidate inside its declaring assembly so all rows remain materially rooted.</summary>
    /// <returns>A deterministic checksum over the complete member-access matrix.</returns>
    public static int ReadAllForFixture() =>
        PublicSentinel ^
        FamilySentinel ^
        FamilyOrAssemblySentinel ^
        FamilyAndAssemblySentinel ^
        AssemblySentinel ^
        PrivateSentinel;
}
