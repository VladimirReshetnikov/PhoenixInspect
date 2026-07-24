namespace PhoenixInspect.W8TestTarget;

/// <summary>Supplies a directly imported non-generic static-member owner.</summary>
/// <remarks>This type is a draft compiler/PDB fixture and not a product lookup contract.</remarks>
public static class NonGenericImports
{
    /// <summary>Stores the directly imported non-generic sentinel.</summary>
    public static int NonGenericImportedSentinel;

    /// <summary>Provides a directly imported non-generic literal.</summary>
    public const int NonGenericImportedLiteral = 0x1B017A01;

    /// <summary>Supplies a nested type imported from the non-generic owner.</summary>
    public sealed class NonGenericNested
    {
        /// <summary>Initializes the nested import witness.</summary>
        /// <param name="marker">The exact retained marker.</param>
        public NonGenericNested(int marker) => Marker = marker;

        /// <summary>Gets the retained marker.</summary>
        public int Marker { get; }
    }
}

/// <summary>Supplies a non-generic nested-owner chain.</summary>
/// <remarks>This type exists only to freeze source and metadata declaring-type identity.</remarks>
public static class NonGenericOuter
{
    /// <summary>Supplies the nested non-generic owner.</summary>
    public static class Nested
    {
        /// <summary>Stores the nested-owner sentinel.</summary>
        public static int Sentinel;
    }
}

/// <summary>Supplies class-base substitution and inherited-name lookup evidence.</summary>
/// <typeparam name="T">The exact value stored by the base construction.</typeparam>
/// <remarks>This type exists only as a draft emitted lookup fixture.</remarks>
public class ClassLookupBase<T>
{
    /// <summary>Stores a value on the exact base construction.</summary>
    public static T? InheritedValue;

    /// <summary>Supplies an instance member that can block a field-only route.</summary>
    public int InstanceBlocker;
}

/// <summary>Substitutes a constructed list into the class-base lookup fixture.</summary>
/// <typeparam name="T">The argument wrapped by the derived base edge.</typeparam>
/// <remarks>This type exists only as a draft emitted lookup fixture.</remarks>
public sealed class ClassLookupDerived<T> : ClassLookupBase<List<T>>
{
    /// <summary>Stores a directly declared derived sentinel.</summary>
    public static int DirectSentinel;
}

/// <summary>Supplies an interface base edge with a stored static field.</summary>
/// <typeparam name="T">The exact interface construction argument.</typeparam>
/// <remarks>This type exists only as a draft emitted lookup fixture.</remarks>
public interface IInterfaceLookupBase<T>
{
    /// <summary>Stores the base-interface sentinel.</summary>
    public static int BaseSentinel = 0x1B027A01;
}

/// <summary>Substitutes an array into the interface-base lookup fixture.</summary>
/// <typeparam name="T">The array element argument.</typeparam>
/// <remarks>This type exists only as a draft emitted lookup fixture.</remarks>
public interface IInterfaceLookupDerived<T> : IInterfaceLookupBase<T[]>
{
    /// <summary>Stores the directly declared derived-interface sentinel.</summary>
    public static int DirectSentinel = 0x1B027A02;
}

/// <summary>Supplies all CLI field-accessibility flags on one emitted owner.</summary>
/// <remarks>This type exists only as a draft accessibility metadata fixture.</remarks>
public class AccessibilityMatrix
{
    /// <summary>Stores the public sentinel.</summary>
    public static int PublicSentinel = 0x1B037A01;

    /// <summary>Stores the family sentinel.</summary>
    protected static int FamilySentinel = 0x1B037A02;

    /// <summary>Stores the assembly-or-family sentinel.</summary>
    protected internal static int FamilyOrAssemblySentinel = 0x1B037A03;

    /// <summary>Stores the assembly-and-family sentinel.</summary>
    private protected static int FamilyAndAssemblySentinel = 0x1B037A04;

    internal static int AssemblySentinel = 0x1B037A05;

    private static int PrivateSentinel = 0x1B037A06;

    /// <summary>Reads all values so every declared member remains materially used.</summary>
    /// <returns>A deterministic checksum over the accessibility sentinels.</returns>
    public static int ReadAllForFixture() =>
        PublicSentinel ^
        FamilySentinel ^
        FamilyOrAssemblySentinel ^
        FamilyAndAssemblySentinel ^
        AssemblySentinel ^
        PrivateSentinel;
}

/// <summary>Supplies an effectively non-public nested owner.</summary>
/// <remarks>This type exists only as a draft effective-accessibility fixture.</remarks>
public static class NestedAccessibilityOwner
{
    private static class PrivateNested
    {
        public static int Sentinel = 0x1B047A01;
    }

    /// <summary>Reads the private nested owner's sentinel.</summary>
    /// <returns>The exact private nested-owner value.</returns>
    public static int ReadPrivateNestedForFixture() => PrivateNested.Sentinel;
}

/// <summary>Supplies a reference and public-constructor constraint.</summary>
/// <typeparam name="T">A reference type with a public parameterless constructor.</typeparam>
/// <remarks>This type exists only as a draft generic-constraint fixture.</remarks>
public static class ReferenceConstructorConstraintSlot<T>
    where T : class, new()
{
    /// <summary>Stores the constraint-construction sentinel.</summary>
    public static int Sentinel;
}

/// <summary>Supplies a nullable-reference constraint.</summary>
/// <typeparam name="T">A nullable reference type.</typeparam>
/// <remarks>This type exists only as a draft generic-constraint fixture.</remarks>
public static class NullableReferenceConstraintSlot<T>
    where T : class?
{
    /// <summary>Stores the constraint-construction sentinel.</summary>
    public static int Sentinel;
}

/// <summary>Supplies a non-null warning constraint.</summary>
/// <typeparam name="T">A type subject to the non-null warning constraint.</typeparam>
/// <remarks>This type exists only as a draft generic-constraint fixture.</remarks>
public static class NonNullConstraintSlot<T>
    where T : notnull
{
    /// <summary>Stores the constraint-construction sentinel.</summary>
    public static int Sentinel;
}

/// <summary>Supplies a value-type constraint.</summary>
/// <typeparam name="T">A non-nullable value type.</typeparam>
/// <remarks>This type exists only as a draft generic-constraint fixture.</remarks>
public static class ValueConstraintSlot<T>
    where T : struct
{
    /// <summary>Stores the constraint-construction sentinel.</summary>
    public static int Sentinel;
}

/// <summary>Supplies an enum constraint.</summary>
/// <typeparam name="T">A non-nullable enum type.</typeparam>
/// <remarks>This type exists only as a draft generic-constraint fixture.</remarks>
public static class EnumConstraintSlot<T>
    where T : struct, Enum
{
    /// <summary>Stores the constraint-construction sentinel.</summary>
    public static int Sentinel;
}

/// <summary>Supplies a delegate constraint.</summary>
/// <typeparam name="T">A delegate type.</typeparam>
/// <remarks>This type exists only as a draft generic-constraint fixture.</remarks>
public static class DelegateConstraintSlot<T>
    where T : Delegate
{
    /// <summary>Stores the constraint-construction sentinel.</summary>
    public static int Sentinel;
}

/// <summary>Supplies recursively substituted base, interface, and constructor constraints.</summary>
/// <typeparam name="T">A stream implementation with a public parameterless constructor.</typeparam>
/// <remarks>This type exists only as a draft generic-constraint fixture.</remarks>
public static class CompositeConstraintSlot<T>
    where T : Stream, IDisposable, new()
{
    /// <summary>Stores the constraint-construction sentinel.</summary>
    public static int Sentinel;
}

/// <summary>Supplies the ref-like anti-constraint encoding accepted by the pinned compiler.</summary>
/// <typeparam name="T">A type that may be ref-like.</typeparam>
/// <remarks>This type exists only as a draft generic-constraint fixture.</remarks>
public static class RefLikeConstraintSlot<T>
    where T : allows ref struct
{
    /// <summary>Stores the constraint-construction sentinel.</summary>
    public static int Sentinel;
}
