namespace PhoenixInspect.Core.Abstractions;

/// <summary>
/// Represents the evaluation-stack category of a value.
/// </summary>
public enum StackKind
{
    /// <summary>The CLI 32-bit integer stack category, including smaller integers and Boolean values.</summary>
    I4,

    /// <summary>The CLI 64-bit integer stack category.</summary>
    I8,

    /// <summary>The CLI nominal 32-bit floating-point stack category.</summary>
    R4,

    /// <summary>The CLI nominal 64-bit floating-point stack category.</summary>
    R8,

    /// <summary>The target-native integer stack category.</summary>
    NativeInt,

    /// <summary>An object-reference stack value.</summary>
    Ref,

    /// <summary>A managed-reference stack value.</summary>
    ByRef,

    /// <summary>An unboxed value-type stack value not represented by a primitive category.</summary>
    ValueType,
}
