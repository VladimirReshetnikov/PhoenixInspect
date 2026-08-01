using System.Buffers.Binary;
using System.Collections.Immutable;
using Microsoft.Diagnostics.Runtime;
using PhoenixInspect.Host.Abstractions;

namespace PhoenixInspect.Host.Dump.ClrMD;

/// <summary>Identifies the element domain of one single-dimension array observation.</summary>
public enum ClrmdArrayElementKind
{
    /// <summary>Elements decoded as exact Int32 values: SByte, Byte, Int16, UInt16, and Int32 arrays.</summary>
    Int32 = 1,

    /// <summary>Elements decoded as exact Int64 values: UInt32 and Int64 arrays.</summary>
    Int64 = 2,

    /// <summary>Elements decoded as exact IEEE-754 doubles.</summary>
    Double = 3,

    /// <summary>Elements decoded as exact IEEE-754 singles.</summary>
    Single = 4,

    /// <summary>Elements decoded as exact Booleans.</summary>
    Boolean = 5,

    /// <summary>Elements decoded as exact UTF-16 code units.</summary>
    Char = 6,

    /// <summary>Elements that are string references: exact bounded strings or exact nulls.</summary>
    String = 7,
}

/// <summary>One exactly decoded array element: a scalar in the observation's element domain, or an exact null.</summary>
public sealed class ClrmdArrayElementValue
{
    private ClrmdArrayElementValue(
        bool isNull,
        int? int32Value,
        long? int64Value,
        double? doubleValue,
        float? singleValue,
        bool? booleanValue,
        char? charValue,
        string? stringValue)
    {
        IsNull = isNull;
        Int32Value = int32Value;
        Int64Value = int64Value;
        DoubleValue = doubleValue;
        SingleValue = singleValue;
        BooleanValue = booleanValue;
        CharValue = charValue;
        StringValue = stringValue;
    }

    /// <summary>Gets whether the element is an exact null reference.</summary>
    public bool IsNull { get; }

    /// <summary>Gets the exact Int32 value for <see cref="ClrmdArrayElementKind.Int32"/> elements.</summary>
    public int? Int32Value { get; }

    /// <summary>Gets the exact Int64 value for <see cref="ClrmdArrayElementKind.Int64"/> elements.</summary>
    public long? Int64Value { get; }

    /// <summary>Gets the exact double value for <see cref="ClrmdArrayElementKind.Double"/> elements.</summary>
    public double? DoubleValue { get; }

    /// <summary>Gets the exact single value for <see cref="ClrmdArrayElementKind.Single"/> elements.</summary>
    public float? SingleValue { get; }

    /// <summary>Gets the exact Boolean value for <see cref="ClrmdArrayElementKind.Boolean"/> elements.</summary>
    public bool? BooleanValue { get; }

    /// <summary>Gets the exact char value for <see cref="ClrmdArrayElementKind.Char"/> elements.</summary>
    public char? CharValue { get; }

    /// <summary>Gets the exact string value for non-null <see cref="ClrmdArrayElementKind.String"/> elements.</summary>
    public string? StringValue { get; }

    internal static ClrmdArrayElementValue Null() => new(true, null, null, null, null, null, null, null);

    internal static ClrmdArrayElementValue FromInt32(int value) =>
        new(false, value, null, null, null, null, null, null);

    internal static ClrmdArrayElementValue FromInt64(long value) =>
        new(false, null, value, null, null, null, null, null);

    internal static ClrmdArrayElementValue FromDouble(double value) =>
        new(false, null, null, value, null, null, null, null);

    internal static ClrmdArrayElementValue FromSingle(float value) =>
        new(false, null, null, null, value, null, null, null);

    internal static ClrmdArrayElementValue FromBoolean(bool value) =>
        new(false, null, null, null, null, value, null, null);

    internal static ClrmdArrayElementValue FromChar(char value) =>
        new(false, null, null, null, null, null, value, null);

    internal static ClrmdArrayElementValue FromString(string value) =>
        new(false, null, null, null, null, null, null, value);
}

/// <summary>
/// The complete exact content of one single-dimension array read from the dump heap. The dump is immutable, so the
/// observation is a read-only copy: nothing is ever written back, and consumers treat the array as a value.
/// </summary>
public sealed class ClrmdArrayValueObservation
{
    internal ClrmdArrayValueObservation(
        ClrmdArrayElementKind elementKind,
        string elementTypeName,
        string arrayTypeName,
        ulong address,
        int length,
        ImmutableArray<ClrmdArrayElementValue> elements)
    {
        ElementKind = elementKind;
        ElementTypeName = elementTypeName;
        ArrayTypeName = arrayTypeName;
        Address = address;
        Length = length;
        Elements = elements;
    }

    /// <summary>Gets the element domain every entry of <see cref="Elements"/> decodes into.</summary>
    public ClrmdArrayElementKind ElementKind { get; }

    /// <summary>Gets the display name of the element domain, for example <c>Int32</c> or <c>String</c>.</summary>
    public string ElementTypeName { get; }

    /// <summary>Gets the runtime-reported array type name.</summary>
    public string ArrayTypeName { get; }

    /// <summary>Gets the exact array object address.</summary>
    public ulong Address { get; }

    /// <summary>Gets the exact declared array length.</summary>
    public int Length { get; }

    /// <summary>Gets every element, decoded exactly and in order.</summary>
    public ImmutableArray<ClrmdArrayElementValue> Elements { get; }
}

/// <content>
/// The single-dimension array value profile: bounded, exactly decoded, read-only array content from the dump heap.
/// </content>
public sealed partial class ClrmdDumpSession
{
    /// <summary>
    /// Reads the complete content of one single-dimension array object. The read is bounded: an array longer than
    /// <paramref name="maximumElements"/>, or a string element longer than the exact-string character cap, is a
    /// typed non-exact outcome rather than a truncated value.
    /// </summary>
    /// <param name="arrayAddress">The exact array object address.</param>
    /// <param name="maximumElements">The deterministic element-count bound.</param>
    /// <returns>The exact observation, or a typed non-exact outcome.</returns>
    public ClrmdEvidenceResult<ClrmdArrayValueObservation> ReadSzArrayValue(ulong arrayAddress, int maximumElements)
    {
        var runtimeObject = _runtime.Heap.GetObject(arrayAddress);
        if (!runtimeObject.IsValid || runtimeObject.Type is not { } type)
        {
            return ClrmdEvidenceResult<ClrmdArrayValueObservation>.Create(
                ClrmdEvidenceStatus.Invalid,
                ClrmdValueIssue.InvalidData);
        }

        if (!type.IsArray ||
            type.Name is not { } arrayTypeName ||
            !arrayTypeName.EndsWith("[]", StringComparison.Ordinal) ||
            type.ComponentType is not { } componentType)
        {
            return ClrmdEvidenceResult<ClrmdArrayValueObservation>.Create(
                ClrmdEvidenceStatus.Unavailable,
                ClrmdValueIssue.MemberShapeUnsupported);
        }

        var array = runtimeObject.AsArray();
        var length = array.Length;
        if (length < 0)
        {
            return ClrmdEvidenceResult<ClrmdArrayValueObservation>.Create(
                ClrmdEvidenceStatus.Invalid,
                ClrmdValueIssue.InvalidData);
        }

        if (length > maximumElements)
        {
            return ClrmdEvidenceResult<ClrmdArrayValueObservation>.Create(
                ClrmdEvidenceStatus.Partial,
                ClrmdValueIssue.LimitExceeded);
        }

        if (componentType.IsString)
        {
            return ReadStringArray(array, arrayTypeName, length);
        }

        return componentType.ElementType switch
        {
            ClrElementType.Boolean => ReadPrimitiveArray(
                array, arrayTypeName, length, ClrmdArrayElementKind.Boolean, "Boolean",
                static (a, n) => a.ReadValues<bool>(0, n)?.Select(ClrmdArrayElementValue.FromBoolean)),
            ClrElementType.Char => ReadPrimitiveArray(
                array, arrayTypeName, length, ClrmdArrayElementKind.Char, "Char",
                static (a, n) => a.ReadValues<char>(0, n)?.Select(ClrmdArrayElementValue.FromChar)),
            ClrElementType.Int8 => ReadPrimitiveArray(
                array, arrayTypeName, length, ClrmdArrayElementKind.Int32, "Int32",
                static (a, n) => a.ReadValues<sbyte>(0, n)?.Select(static v => ClrmdArrayElementValue.FromInt32(v))),
            ClrElementType.UInt8 => ReadPrimitiveArray(
                array, arrayTypeName, length, ClrmdArrayElementKind.Int32, "Int32",
                static (a, n) => a.ReadValues<byte>(0, n)?.Select(static v => ClrmdArrayElementValue.FromInt32(v))),
            ClrElementType.Int16 => ReadPrimitiveArray(
                array, arrayTypeName, length, ClrmdArrayElementKind.Int32, "Int32",
                static (a, n) => a.ReadValues<short>(0, n)?.Select(static v => ClrmdArrayElementValue.FromInt32(v))),
            ClrElementType.UInt16 => ReadPrimitiveArray(
                array, arrayTypeName, length, ClrmdArrayElementKind.Int32, "Int32",
                static (a, n) => a.ReadValues<ushort>(0, n)?.Select(static v => ClrmdArrayElementValue.FromInt32(v))),
            ClrElementType.Int32 => ReadPrimitiveArray(
                array, arrayTypeName, length, ClrmdArrayElementKind.Int32, "Int32",
                static (a, n) => a.ReadValues<int>(0, n)?.Select(ClrmdArrayElementValue.FromInt32)),
            ClrElementType.UInt32 => ReadPrimitiveArray(
                array, arrayTypeName, length, ClrmdArrayElementKind.Int64, "Int64",
                static (a, n) => a.ReadValues<uint>(0, n)?.Select(static v => ClrmdArrayElementValue.FromInt64(v))),
            ClrElementType.Int64 => ReadPrimitiveArray(
                array, arrayTypeName, length, ClrmdArrayElementKind.Int64, "Int64",
                static (a, n) => a.ReadValues<long>(0, n)?.Select(ClrmdArrayElementValue.FromInt64)),
            ClrElementType.Float => ReadPrimitiveArray(
                array, arrayTypeName, length, ClrmdArrayElementKind.Single, "Single",
                static (a, n) => a.ReadValues<float>(0, n)?.Select(ClrmdArrayElementValue.FromSingle)),
            ClrElementType.Double => ReadPrimitiveArray(
                array, arrayTypeName, length, ClrmdArrayElementKind.Double, "Double",
                static (a, n) => a.ReadValues<double>(0, n)?.Select(ClrmdArrayElementValue.FromDouble)),
            _ => ClrmdEvidenceResult<ClrmdArrayValueObservation>.Create(
                ClrmdEvidenceStatus.Unavailable,
                ClrmdValueIssue.MemberShapeUnsupported),
        };
    }

    /// <summary>
    /// Walks direct instance reference members from one exact object to a single-dimension array member, then reads
    /// that array's complete content. Every hop must be a declared reference field holding a non-null valid object.
    /// </summary>
    /// <param name="objectAddress">The exact starting object address.</param>
    /// <param name="memberPath">The ordered member names, ending at the array member.</param>
    /// <param name="maximumElements">The deterministic element-count bound.</param>
    /// <returns>The exact observation, or a typed non-exact outcome.</returns>
    public ClrmdEvidenceResult<ClrmdArrayValueObservation> ReadSzArrayInstancePath(
        ulong objectAddress,
        ImmutableArray<string> memberPath,
        int maximumElements)
    {
        if (memberPath.IsDefaultOrEmpty)
        {
            return ClrmdEvidenceResult<ClrmdArrayValueObservation>.Create(
                ClrmdEvidenceStatus.Invalid,
                ClrmdValueIssue.InvalidData);
        }

        var current = _runtime.Heap.GetObject(objectAddress);
        if (!current.IsValid || current.Type is null)
        {
            return ClrmdEvidenceResult<ClrmdArrayValueObservation>.Create(
                ClrmdEvidenceStatus.Invalid,
                ClrmdValueIssue.InvalidData);
        }

        foreach (var member in memberPath)
        {
            if (current.Type?.GetFieldByName(member) is not { } field || !field.IsObjectReference)
            {
                return ClrmdEvidenceResult<ClrmdArrayValueObservation>.Create(
                    ClrmdEvidenceStatus.Unavailable,
                    ClrmdValueIssue.FieldUnavailable);
            }

            ClrObject next;
            try
            {
                next = current.ReadObjectField(member);
            }
            catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
            {
                return ClrmdEvidenceResult<ClrmdArrayValueObservation>.Create(
                    ClrmdEvidenceStatus.Unavailable,
                    ClrmdValueIssue.FieldUnavailable);
            }

            if (next.IsNull)
            {
                return ClrmdEvidenceResult<ClrmdArrayValueObservation>.Create(
                    ClrmdEvidenceStatus.Unavailable,
                    ClrmdValueIssue.ObjectUnavailable);
            }

            if (!next.IsValid)
            {
                return ClrmdEvidenceResult<ClrmdArrayValueObservation>.Create(
                    ClrmdEvidenceStatus.Invalid,
                    ClrmdValueIssue.InvalidData);
            }

            current = next;
        }

        return ReadSzArrayValue(current.Address, maximumElements);
    }

    /// <summary>
    /// Reads the complete content of a single-dimension array held by one static field, named by its declaring
    /// type's full metadata name. Exactly one loaded declaration with a non-null value may exist; several are a
    /// typed conflict, because no instance is selected by enumeration order.
    /// </summary>
    /// <param name="typeFullName">The declaring type's namespace-qualified name.</param>
    /// <param name="fieldName">The static field's exact name.</param>
    /// <param name="maximumElements">The deterministic element-count bound.</param>
    /// <returns>The exact observation, or a typed non-exact outcome.</returns>
    public ClrmdEvidenceResult<ClrmdArrayValueObservation> ReadStaticSzArrayField(
        string typeFullName,
        string fieldName,
        int maximumElements)
    {
        ArgumentNullException.ThrowIfNull(typeFullName);
        ArgumentNullException.ThrowIfNull(fieldName);
        ulong? found = null;
        var matches = 0;
        foreach (var module in _runtime.EnumerateModules())
        {
            if (module.GetTypeByName(typeFullName)?.GetStaticFieldByName(fieldName) is not { } field ||
                !field.IsObjectReference)
            {
                continue;
            }

            foreach (var domain in _runtime.AppDomains)
            {
                ClrObject value;
                try
                {
                    value = field.ReadObject(domain);
                }
                catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
                {
                    continue;
                }

                if (value.IsNull || !value.IsValid)
                {
                    continue;
                }

                matches++;
                found = value.Address;
                break;
            }
        }

        if (matches > 1)
        {
            return ClrmdEvidenceResult<ClrmdArrayValueObservation>.Create(
                ClrmdEvidenceStatus.Conflict,
                ClrmdValueIssue.AmbiguousMatch);
        }

        if (found is not { } address)
        {
            return ClrmdEvidenceResult<ClrmdArrayValueObservation>.Create(
                ClrmdEvidenceStatus.Unavailable,
                ClrmdValueIssue.FieldUnavailable);
        }

        return ReadSzArrayValue(address, maximumElements);
    }

    private ClrmdEvidenceResult<ClrmdArrayValueObservation> ReadPrimitiveArray(
        ClrArray array,
        string arrayTypeName,
        int length,
        ClrmdArrayElementKind kind,
        string elementTypeName,
        Func<ClrArray, int, IEnumerable<ClrmdArrayElementValue>?> read)
    {
        var elements = length == 0 ? [] : read(array, length);
        if (elements is null)
        {
            return ClrmdEvidenceResult<ClrmdArrayValueObservation>.Create(
                ClrmdEvidenceStatus.Unavailable,
                ClrmdValueIssue.MemoryUnavailable);
        }

        return ClrmdEvidenceResult<ClrmdArrayValueObservation>.Create(
            ClrmdEvidenceStatus.Exact,
            ClrmdValueIssue.None,
            new ClrmdArrayValueObservation(
                kind,
                elementTypeName,
                arrayTypeName,
                array.Address,
                length,
                [.. elements]));
    }

    private ClrmdEvidenceResult<ClrmdArrayValueObservation> ReadStringArray(
        ClrArray array,
        string arrayTypeName,
        int length)
    {
        var references = length == 0 ? [] : array.ReadValues<ulong>(0, length);
        if (references is null)
        {
            return ClrmdEvidenceResult<ClrmdArrayValueObservation>.Create(
                ClrmdEvidenceStatus.Unavailable,
                ClrmdValueIssue.MemoryUnavailable);
        }

        var elements = ImmutableArray.CreateBuilder<ClrmdArrayElementValue>(length);
        foreach (var reference in references)
        {
            if (reference == 0)
            {
                elements.Add(ClrmdArrayElementValue.Null());
                continue;
            }

            var (status, issue, value) = ReadStringObjectValue(reference);
            if (status != ClrmdEvidenceStatus.Exact)
            {
                return ClrmdEvidenceResult<ClrmdArrayValueObservation>.Create(status, issue);
            }

            elements.Add(ClrmdArrayElementValue.FromString(value!));
        }

        return ClrmdEvidenceResult<ClrmdArrayValueObservation>.Create(
            ClrmdEvidenceStatus.Exact,
            ClrmdValueIssue.None,
            new ClrmdArrayValueObservation(
                ClrmdArrayElementKind.String,
                "String",
                arrayTypeName,
                array.Address,
                length,
                elements.ToImmutable()));
    }

    /// <summary>Reads one string object's exact value: validated header, exact length, complete characters.</summary>
    private (ClrmdEvidenceStatus Status, ClrmdValueIssue Issue, string? Value) ReadStringObjectValue(
        ulong stringAddress)
    {
        var stringObject = _runtime.Heap.GetObject(stringAddress);
        if (!stringObject.IsValid || stringObject.Type is null)
        {
            return (ClrmdEvidenceStatus.Invalid, ClrmdValueIssue.InvalidData, null);
        }

        if (!stringObject.Type.IsString)
        {
            return (ClrmdEvidenceStatus.Conflict, ClrmdValueIssue.TypeMismatch, null);
        }

        if (!TryAdd(stringAddress, (ulong)Memory.PointerSize, out var lengthAddress))
        {
            return (ClrmdEvidenceStatus.Invalid, ClrmdValueIssue.InvalidData, null);
        }

        var lengthRead = Memory.Read(lengthAddress, sizeof(int));
        if (lengthRead.Status != MemoryReadStatus.Exact)
        {
            return (ToEvidenceStatus(lengthRead.Status), ClrmdValueIssue.MemoryUnavailable, null);
        }

        var targetLength = BinaryPrimitives.ReadInt32LittleEndian(lengthRead.Bytes.AsSpan());
        if (targetLength < 0)
        {
            return (ClrmdEvidenceStatus.Invalid, ClrmdValueIssue.InvalidData, null);
        }

        if (targetLength > ClrmdExactStringValue.MaximumCharacters)
        {
            return (ClrmdEvidenceStatus.Partial, ClrmdValueIssue.LimitExceeded, null);
        }

        if (targetLength == 0)
        {
            return (ClrmdEvidenceStatus.Exact, ClrmdValueIssue.None, string.Empty);
        }

        if (!TryAdd(lengthAddress, sizeof(int), out var characterAddress) ||
            !IsRangeWithinExtent(
                stringAddress,
                stringObject.Size,
                characterAddress,
                (ulong)(uint)targetLength * sizeof(char)))
        {
            return (ClrmdEvidenceStatus.Invalid, ClrmdValueIssue.InvalidData, null);
        }

        var characterRead = Memory.Read(characterAddress, checked(targetLength * sizeof(char)));
        if (characterRead.Status != MemoryReadStatus.Exact)
        {
            return (ToEvidenceStatus(characterRead.Status), ClrmdValueIssue.MemoryUnavailable, null);
        }

        return (
            ClrmdEvidenceStatus.Exact,
            ClrmdValueIssue.None,
            DecodeLittleEndianUtf16(characterRead.Bytes.AsSpan()));
    }
}
