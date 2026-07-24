using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("PhoenixInspect.W8TestTarget")]

namespace PhoenixInspect.W8AliasTarget;

/// <summary>
/// Supplies a distinct assembly-owned type argument for W8 extern-alias and AssemblyRef evidence.
/// </summary>
/// <remarks>
/// This emitted fixture type is a draft physical oracle. It is not a product contract.
/// </remarks>
public sealed class ExternalRequestContext
{
    /// <summary>Initializes a context with a stable fixture label.</summary>
    /// <param name="label">The explicitly initialized label retained by the target.</param>
    public ExternalRequestContext(string label)
    {
        Label = label;
    }

    /// <summary>Gets the fixture label.</summary>
    public string Label { get; }
}

/// <summary>
/// Supplies the external half of a same-simple-name, cross-assembly lookup pair.
/// </summary>
/// <remarks>
/// This draft fixture type is intentionally distinct from the target assembly's equally named definition.
/// </remarks>
public sealed class SharedSpelling
{
    /// <summary>Initializes the external candidate with its exact retained marker.</summary>
    /// <param name="marker">The stable value distinguishing this physical candidate.</param>
    public SharedSpelling(int marker) => Marker = marker;

    /// <summary>Gets the retained external-candidate marker.</summary>
    public int Marker { get; }
}

/// <summary>
/// Supplies a generic static owner that is reachable only through the W8 target's explicit extern alias.
/// </summary>
/// <typeparam name="T">The exact closed argument distinguishing one runtime construction.</typeparam>
/// <remarks>
/// The fields exist only to establish emitted metadata, runtime construction, slot, and value facts.
/// </remarks>
public static class ExternalSlot<T>
{
    /// <summary>Stores the construction-specific integer sentinel.</summary>
    public static int Sentinel;

    /// <summary>Stores the construction-specific reference or value.</summary>
    public static T? Current;

    /// <summary>Provides a runtime-free metadata literal on an assembly-qualified generic owner.</summary>
    public const int Literal = 0x18A17A5;
}

/// <summary>
/// Supplies an external generic interface definition with a construction-specific stored static field.
/// </summary>
/// <typeparam name="T">The exact closed argument distinguishing the interface construction.</typeparam>
/// <remarks>This is an emitted W8 truth-gate fixture, not a reusable interface.</remarks>
public interface IExternalInterfaceSlot<T>
{
    /// <summary>Stores the construction-specific interface sentinel.</summary>
    public static int Sentinel = 0x18A17A6;
}

/// <summary>
/// Supplies the destination TypeDef used by the independent W8 forwarding-assembly fixture.
/// </summary>
/// <remarks>
/// This emitted fixture type exists only to establish an exact forwarding and TypeRef convergence oracle.
/// </remarks>
public sealed class ForwardedRequestContext
{
    /// <summary>Initializes the forwarded context with a stable fixture label.</summary>
    /// <param name="label">The exact label retained by the target.</param>
    public ForwardedRequestContext(string label) => Label = label;

    /// <summary>Gets the exact fixture label.</summary>
    public string Label { get; }
}

/// <summary>
/// Supplies public and non-public external members for emitted accessibility evidence.
/// </summary>
/// <remarks>
/// These members are draft compiler/metadata fixtures and do not define a reusable accessibility surface.
/// </remarks>
public class ExternalAccessibilityBase
{
    /// <summary>Stores the public external sentinel.</summary>
    public static int PublicSentinel = 0x1A017A01;

    /// <summary>Stores the family-visible external sentinel.</summary>
    protected static int FamilySentinel = 0x1A017A02;

    /// <summary>Stores the assembly-or-family external sentinel.</summary>
    protected internal static int FamilyOrAssemblySentinel = 0x1A017A03;

    /// <summary>Stores the assembly-and-family external sentinel.</summary>
    private protected static int FamilyAndAssemblySentinel = 0x1A017A04;

    internal static int AssemblySentinel = 0x1A017A05;

    private static int PrivateSentinel = 0x1A017A06;

    /// <summary>Reads all retained values so the compiler preserves the complete member set.</summary>
    /// <returns>A deterministic checksum over every external accessibility sentinel.</returns>
    public static int ReadAllForFixture() =>
        PublicSentinel ^
        FamilySentinel ^
        FamilyOrAssemblySentinel ^
        FamilyAndAssemblySentinel ^
        AssemblySentinel ^
        PrivateSentinel;
}

internal static class FriendVisibleOwner
{
    internal static int Sentinel = 0x1A027A01;
}

internal static class AssemblyOnlyOwner
{
    internal static int Sentinel = 0x1A037A01;
}
