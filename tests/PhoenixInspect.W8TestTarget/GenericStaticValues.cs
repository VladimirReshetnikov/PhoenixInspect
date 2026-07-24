using System.Runtime.CompilerServices;

namespace PhoenixInspect.W8TestTarget;

/// <summary>Identifies the request-shaped construction used by the W8 physical fixture.</summary>
/// <remarks>This is an emitted fixture type, not a product contract.</remarks>
public sealed class RequestContext
{
    /// <summary>Initializes the request context.</summary>
    /// <param name="name">The stable fixture name.</param>
    public RequestContext(string name) => Name = name;

    /// <summary>Gets the stable fixture name.</summary>
    public string Name { get; }
}

/// <summary>Identifies the batch-shaped construction used by the W8 physical fixture.</summary>
/// <remarks>This is an emitted fixture type, not a product contract.</remarks>
public sealed class BatchContext
{
    /// <summary>Initializes the batch context.</summary>
    /// <param name="name">The stable fixture name.</param>
    public BatchContext(string name) => Name = name;

    /// <summary>Gets the stable fixture name.</summary>
    public string Name { get; }
}

/// <summary>Identifies the coordinator-shaped construction used by the W8 physical fixture.</summary>
/// <remarks>This is an emitted fixture type, not a product contract.</remarks>
public sealed class CoordinatorContext
{
    /// <summary>Initializes the coordinator context.</summary>
    /// <param name="name">The stable fixture name.</param>
    public CoordinatorContext(string name) => Name = name;

    /// <summary>Gets the stable fixture name.</summary>
    public string Name { get; }
}

/// <summary>Identifies the workflow-shaped construction used by the W8 physical fixture.</summary>
/// <remarks>This is an emitted fixture type, not a product contract.</remarks>
public sealed class WorkflowContext
{
    /// <summary>Initializes the workflow context.</summary>
    /// <param name="name">The stable fixture name.</param>
    public WorkflowContext(string name) => Name = name;

    /// <summary>Gets the stable fixture name.</summary>
    public string Name { get; }
}

/// <summary>Supplies a non-reference generic argument for construction-kind and substitution probes.</summary>
/// <param name="Marker">The stable integer marker.</param>
/// <remarks>This is an emitted fixture value, not a product value contract.</remarks>
public readonly record struct ValueContext(int Marker);

/// <summary>Supplies enum-underlying metadata and stored-value evidence.</summary>
/// <remarks>This is an emitted fixture enum, not a product value contract.</remarks>
public enum MarkerKind : short
{
    /// <summary>The request marker.</summary>
    Request = 0x171,

    /// <summary>The workflow marker.</summary>
    Workflow = 0x272,
}

/// <summary>Supplies a signed eight-bit enum representation for exact decoder evidence.</summary>
/// <remarks>This is an emitted fixture enum, not a product value contract.</remarks>
public enum SignedByteMarker : sbyte
{
    /// <summary>The retained negative marker.</summary>
    Retained = -0x35,
}

/// <summary>Supplies an unsigned eight-bit enum representation for exact decoder evidence.</summary>
/// <remarks>This is an emitted fixture enum, not a product value contract.</remarks>
public enum UnsignedByteMarker : byte
{
    /// <summary>The retained high-bit marker.</summary>
    Retained = 0xD3,
}

/// <summary>Supplies an unsigned sixteen-bit enum representation for exact decoder evidence.</summary>
/// <remarks>This is an emitted fixture enum, not a product value contract.</remarks>
public enum UnsignedInt16Marker : ushort
{
    /// <summary>The retained high-bit marker.</summary>
    Retained = 0xD3E5,
}

/// <summary>Supplies a signed thirty-two-bit enum representation for exact decoder evidence.</summary>
/// <remarks>This is an emitted fixture enum, not a product value contract.</remarks>
public enum SignedInt32Marker : int
{
    /// <summary>The retained negative marker.</summary>
    Retained = unchecked((int)0xD3E5A719),
}

/// <summary>Supplies an unsigned thirty-two-bit enum representation for exact decoder evidence.</summary>
/// <remarks>This is an emitted fixture enum, not a product value contract.</remarks>
public enum UnsignedInt32Marker : uint
{
    /// <summary>The retained high-bit marker.</summary>
    Retained = 0xD3E5A719U,
}

/// <summary>Supplies a signed sixty-four-bit enum representation for exact decoder evidence.</summary>
/// <remarks>This is an emitted fixture enum, not a product value contract.</remarks>
public enum SignedInt64Marker : long
{
    /// <summary>The retained negative marker.</summary>
    Retained = unchecked((long)0xD3E5A7192B4C6D8EUL),
}

/// <summary>Supplies an unsigned sixty-four-bit enum representation for exact decoder evidence.</summary>
/// <remarks>This is an emitted fixture enum, not a product value contract.</remarks>
public enum UnsignedInt64Marker : ulong
{
    /// <summary>The retained high-bit marker.</summary>
    Retained = 0xD3E5A7192B4C6D8EUL,
}

/// <summary>Supplies one generic class definition with construction-specific static storage.</summary>
/// <typeparam name="T">The exact closed argument distinguishing one runtime construction.</typeparam>
/// <remarks>The members are W8 physical evidence and do not constitute a stable API.</remarks>
public static class GenericSlot<T>
{
    /// <summary>Stores a construction-specific integer.</summary>
    public static int Sentinel;

    /// <summary>Stores a construction-specific value or reference.</summary>
    public static T? Current;

    /// <summary>Stores a construction-specific thread-relative integer.</summary>
    [ThreadStatic]
    public static int ThreadSentinel;

    /// <summary>Provides a runtime-free literal whose declaration belongs to the generic definition.</summary>
    public const int Literal = 0x18B17A5;
}

/// <summary>Supplies one context-relative static field for the W8 physical branch probe.</summary>
/// <remarks>The member is runtime evidence and does not constitute a stable storage API.</remarks>
public static class ContextRelativeStorage
{
    /// <summary>Stores the value associated with the active runtime context.</summary>
    [ContextStatic]
    public static int ContextSentinel;
}

/// <summary>Supplies one generic value-type definition with construction-specific static storage.</summary>
/// <typeparam name="T">The exact closed argument distinguishing one runtime construction.</typeparam>
/// <remarks>The members are W8 physical evidence and do not constitute a stable API.</remarks>
public struct GenericStructSlot<T>
{
    /// <summary>Stores a construction-specific integer.</summary>
    public static int Sentinel;

    /// <summary>Stores a construction-specific value or reference.</summary>
    public static T? Current;
}

/// <summary>Supplies one generic interface definition with construction-specific static storage.</summary>
/// <typeparam name="T">The exact closed argument distinguishing one runtime construction.</typeparam>
/// <remarks>The members are W8 physical evidence and do not constitute a stable API.</remarks>
public interface IGenericInterfaceSlot<T>
{
    /// <summary>Stores a construction-specific integer.</summary>
    public static int Sentinel = 0x18B17A6;

    /// <summary>Stores a construction-specific value or reference.</summary>
    public static T? Current = default;

    /// <summary>Provides a runtime-free literal on the generic interface definition.</summary>
    public const int Literal = 0x18B17A7;
}

/// <summary>Supplies nested generic and non-generic segments for emitted arity mapping.</summary>
/// <typeparam name="TOuter">The exact outer construction argument.</typeparam>
/// <remarks>This hierarchy exists only for W8 compiler, metadata, and runtime evidence.</remarks>
public static class Outer<TOuter>
{
    /// <summary>Supplies a non-generic source segment that still carries the enclosing construction.</summary>
    public static class Middle
    {
        /// <summary>Supplies the inner generic segment.</summary>
        /// <typeparam name="TInner">The exact inner construction argument.</typeparam>
        public static class Inner<TInner>
        {
            /// <summary>Stores the exact outer argument value.</summary>
            public static TOuter? OuterValue;

            /// <summary>Stores the exact inner argument value.</summary>
            public static TInner? InnerValue;

            /// <summary>Stores the construction-specific count.</summary>
            public static int Count;
        }
    }
}

/// <summary>Supplies the terminal declaring type for recursive constructed-base substitution.</summary>
/// <typeparam name="T">The exact substituted terminal argument.</typeparam>
/// <remarks>This hierarchy exists only for W8 compiler, metadata, and runtime evidence.</remarks>
public class BaseSlot<T>
{
    /// <summary>Stores the exact substituted terminal value.</summary>
    public static T? Value;
}

/// <summary>Substitutes an array construction into <see cref="BaseSlot{T}"/>.</summary>
/// <typeparam name="T">The argument wrapped in an array construction.</typeparam>
/// <remarks>This hierarchy exists only for W8 compiler, metadata, and runtime evidence.</remarks>
public class MidSlot<T> : BaseSlot<T[]>
{
}

/// <summary>Substitutes a constructed list through the multi-hop base chain.</summary>
/// <typeparam name="T">The argument wrapped in the constructed list.</typeparam>
/// <remarks>This hierarchy exists only for W8 compiler, metadata, and runtime evidence.</remarks>
public sealed class DerivedSlot<T> : MidSlot<List<T>>
{
}

/// <summary>Supplies a generic owner with a reference and public-constructor constraint.</summary>
/// <typeparam name="T">A reference type with a public parameterless constructor.</typeparam>
/// <remarks>This type exists only to freeze emitted constraint evidence.</remarks>
public static class ConstructibleSlot<T>
    where T : class, new()
{
    /// <summary>Stores the construction-specific integer.</summary>
    public static int Sentinel;
}

/// <summary>Supplies a generic owner with an unmanaged constraint.</summary>
/// <typeparam name="T">The unmanaged argument.</typeparam>
/// <remarks>This type exists only to freeze emitted constraint evidence.</remarks>
public static class UnmanagedSlot<T>
    where T : unmanaged
{
    /// <summary>Stores the construction-specific integer.</summary>
    public static int Sentinel;
}

/// <summary>Supplies dependent generic-parameter constraint evidence.</summary>
/// <typeparam name="TBase">The required base argument.</typeparam>
/// <typeparam name="TDerived">The argument constrained to <typeparamref name="TBase"/>.</typeparam>
/// <remarks>This type exists only to freeze emitted constraint evidence.</remarks>
public static class DependentSlot<TBase, TDerived>
    where TDerived : TBase
{
    /// <summary>Stores the construction-specific integer.</summary>
    public static int Sentinel;
}

/// <summary>Supplies complete primitive, enum, nullable, array, reference, and literal encodings.</summary>
/// <remarks>These fields are physical W8 decoder evidence and are not a stable value surface.</remarks>
public static class PrimitiveStorage
{
    /// <summary>Stores a Boolean value.</summary>
    public static bool Boolean = true;

    /// <summary>Stores a signed byte.</summary>
    public static sbyte Int8 = -0x12;

    /// <summary>Stores an unsigned byte.</summary>
    public static byte UInt8 = 0xE1;

    /// <summary>Stores a signed 16-bit value.</summary>
    public static short Int16 = -0x1234;

    /// <summary>Stores an unsigned 16-bit value.</summary>
    public static ushort UInt16 = 0xE123;

    /// <summary>Stores a signed 32-bit value.</summary>
    public static int Int32 = unchecked((int)0x81A2B3C4);

    /// <summary>Stores an unsigned 32-bit value.</summary>
    public static uint UInt32 = 0xE1A2B3C4;

    /// <summary>Stores a signed 64-bit value.</summary>
    public static long Int64 = unchecked((long)0x81A2B3C4D5E6F708);

    /// <summary>Stores an unsigned 64-bit value.</summary>
    public static ulong UInt64 = 0xE1A2B3C4D5E6F708;

    /// <summary>Stores a target-width signed value.</summary>
    public static nint NativeInt = unchecked((nint)(-0x1234567));

    /// <summary>Stores a target-width unsigned value.</summary>
    public static nuint NativeUInt = (nuint)0x1234567;

    /// <summary>Stores a UTF-16 code unit.</summary>
    public static char Character = '\u4E2D';

    /// <summary>Stores a single-precision value.</summary>
    public static float Single = 17.25F;

    /// <summary>Stores a double-precision value.</summary>
    public static double Double = -29.5D;

    /// <summary>Stores a decimal value through ordinary static storage.</summary>
    public static decimal Decimal = 1234.5678M;

    /// <summary>Stores an enum-underlying value.</summary>
    public static MarkerKind Enum = MarkerKind.Workflow;

    /// <summary>Stores an enum with a signed eight-bit representation.</summary>
    public static SignedByteMarker SignedByteEnum = SignedByteMarker.Retained;

    /// <summary>Stores an enum with an unsigned eight-bit representation.</summary>
    public static UnsignedByteMarker UnsignedByteEnum = UnsignedByteMarker.Retained;

    /// <summary>Stores an enum with an unsigned sixteen-bit representation.</summary>
    public static UnsignedInt16Marker UnsignedInt16Enum = UnsignedInt16Marker.Retained;

    /// <summary>Stores an enum with a signed thirty-two-bit representation.</summary>
    public static SignedInt32Marker SignedInt32Enum = SignedInt32Marker.Retained;

    /// <summary>Stores an enum with an unsigned thirty-two-bit representation.</summary>
    public static UnsignedInt32Marker UnsignedInt32Enum = UnsignedInt32Marker.Retained;

    /// <summary>Stores an enum with a signed sixty-four-bit representation.</summary>
    public static SignedInt64Marker SignedInt64Enum = SignedInt64Marker.Retained;

    /// <summary>Stores an enum with an unsigned sixty-four-bit representation.</summary>
    public static UnsignedInt64Marker UnsignedInt64Enum = UnsignedInt64Marker.Retained;

    /// <summary>Stores an exact nullable value.</summary>
    public static int? Nullable = 0x17283940;

    /// <summary>Stores an exact null nullable value.</summary>
    public static int? NullableNull;

    /// <summary>Stores an exact string reference.</summary>
    public static string Text = "w8-primitive-storage";

    /// <summary>Stores an exact null reference.</summary>
    public static RequestContext? NullReference;

    /// <summary>Stores a single-dimensional array reference.</summary>
    public static RequestContext[] Vector = [new("vector")];

    /// <summary>Stores a multidimensional array reference.</summary>
    public static int[,] Matrix = new int[2, 3];

    /// <summary>Provides a Boolean metadata literal.</summary>
    public const bool BooleanLiteral = true;

    /// <summary>Provides a signed eight-bit metadata literal.</summary>
    public const sbyte Int8Literal = -0x35;

    /// <summary>Provides an unsigned eight-bit metadata literal.</summary>
    public const byte UInt8Literal = 0xD3;

    /// <summary>Provides a signed sixteen-bit metadata literal.</summary>
    public const short Int16Literal = -0x3527;

    /// <summary>Provides an unsigned sixteen-bit metadata literal.</summary>
    public const ushort UInt16Literal = 0xD3E5;

    /// <summary>Provides a signed 32-bit metadata literal.</summary>
    public const int Int32Literal = unchecked((int)0x81234567);

    /// <summary>Provides an unsigned thirty-two-bit metadata literal.</summary>
    public const uint UInt32Literal = 0xD3E5A719U;

    /// <summary>Provides a signed sixty-four-bit metadata literal.</summary>
    public const long Int64Literal = unchecked((long)0xD3E5A7192B4C6D8EUL);

    /// <summary>Provides an unsigned 64-bit metadata literal.</summary>
    public const ulong UInt64Literal = 0xE123456789ABCDEF;

    /// <summary>Provides a character metadata literal.</summary>
    public const char CharacterLiteral = '\u03A9';

    /// <summary>Provides a single-precision metadata literal with an exact binary representation.</summary>
    public const float SingleLiteral = -19.625F;

    /// <summary>Provides a floating-point metadata literal.</summary>
    public const double DoubleLiteral = -17.125D;

    /// <summary>Provides an enum metadata literal.</summary>
    public const MarkerKind EnumLiteral = MarkerKind.Request;

    /// <summary>Provides a signed eight-bit enum metadata literal.</summary>
    public const SignedByteMarker SignedByteEnumLiteral = SignedByteMarker.Retained;

    /// <summary>Provides an unsigned eight-bit enum metadata literal.</summary>
    public const UnsignedByteMarker UnsignedByteEnumLiteral = UnsignedByteMarker.Retained;

    /// <summary>Provides an unsigned sixteen-bit enum metadata literal.</summary>
    public const UnsignedInt16Marker UnsignedInt16EnumLiteral = UnsignedInt16Marker.Retained;

    /// <summary>Provides a signed thirty-two-bit enum metadata literal.</summary>
    public const SignedInt32Marker SignedInt32EnumLiteral = SignedInt32Marker.Retained;

    /// <summary>Provides an unsigned thirty-two-bit enum metadata literal.</summary>
    public const UnsignedInt32Marker UnsignedInt32EnumLiteral = UnsignedInt32Marker.Retained;

    /// <summary>Provides a signed sixty-four-bit enum metadata literal.</summary>
    public const SignedInt64Marker SignedInt64EnumLiteral = SignedInt64Marker.Retained;

    /// <summary>Provides an unsigned sixty-four-bit enum metadata literal.</summary>
    public const UnsignedInt64Marker UnsignedInt64EnumLiteral = UnsignedInt64Marker.Retained;

    /// <summary>Provides a non-null string metadata literal.</summary>
    public const string StringLiteral = "w8-literal";

    /// <summary>Provides an exact null metadata literal.</summary>
    public const string? NullLiteral = null;

    /// <summary>Provides a null object-reference metadata literal.</summary>
    public const object? ObjectNullLiteral = null;

    /// <summary>Provides the pinned compiler's decimal-constant attribute encoding.</summary>
    public const decimal DecimalLiteral = 9876.5432M;

    /// <summary>Materializes compiler-generated RVA data for a distinct non-field product boundary probe.</summary>
    public static ReadOnlySpan<byte> RvaBytes => "w8-rva-materialization"u8;
}

/// <summary>Supplies directly declared members for constructed <c>using static</c> evidence.</summary>
/// <typeparam name="T">The exact constructed import argument.</typeparam>
/// <remarks>This type exists only for W8 Portable-PDB and member-lookup evidence.</remarks>
public static class StaticImports<T>
{
    /// <summary>Stores the directly imported construction-specific sentinel.</summary>
    public static int ImportedSentinel;

    /// <summary>Provides a directly imported metadata literal.</summary>
    public const int ImportedLiteral = 0x18C17A5;

    /// <summary>Supplies a nested type imported by <c>using static</c>.</summary>
    public sealed class ImportedNested
    {
        /// <summary>Initializes the imported nested value.</summary>
        /// <param name="value">The stable fixture value.</param>
        public ImportedNested(string value) => Value = value;

        /// <summary>Gets the stable fixture value.</summary>
        public string Value { get; }
    }
}
