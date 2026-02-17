using Interpreter.Core.Abstractions;
using Interpreter.Metadata.Abstractions;
using Interpreter.Types;

namespace Interpreter.Host.Abstractions;

/// <summary>
/// Host-specific external object identity, typically backed by runtime memory addresses.
/// </summary>
/// <param name="Address">Host-defined object address or stable object key.</param>
public readonly record struct ExternalObjectRef(ulong Address);

/// <summary>
/// Host-specific external thread identity.
/// </summary>
/// <param name="OsId">Operating-system thread identifier.</param>
public readonly record struct ExternalThreadId(uint OsId);

/// <summary>
/// Host-specific external frame identity.
/// </summary>
/// <param name="Index">Frame index in host-defined stack order.</param>
public readonly record struct ExternalFrameId(int Index);

/// <summary>
/// Classifies primitive and reference payload shapes used by host value snapshots.
/// </summary>
public enum ExternalValueKind
{
    Unavailable,
    Int32,
    Int64,
    Float64,
    Boolean,
    ObjectRef,
    StringRef,
    RawBytes,
}

/// <summary>
/// Represents a host-provided raw value before adaptation into the interpreter value domain.
/// </summary>
/// <param name="Kind">Payload kind discriminator.</param>
/// <param name="I64">Integral payload storage.</param>
/// <param name="F64">Floating-point payload storage.</param>
/// <param name="Obj">Object-reference payload storage.</param>
/// <param name="Bytes">Optional raw byte payload for value types and blobs.</param>
public sealed record ExternalValue(
    ExternalValueKind Kind,
    long I64 = 0,
    double F64 = 0,
    ExternalObjectRef Obj = default,
    ReadOnlyMemory<byte>? Bytes = null);

/// <summary>
/// Captures session-wide environment metadata for deterministic modeling decisions.
/// </summary>
/// <param name="DumpCaptureTimeUtc">Dump capture timestamp in UTC when known.</param>
/// <param name="TargetLocalOffset">Target machine local offset when known.</param>
/// <param name="TargetMachineName">Target machine name when known.</param>
/// <param name="TargetProcessId">Target process identifier when known.</param>
/// <param name="EnvironmentVariables">Optional environment variable snapshot.</param>
public sealed record SessionSnapshot(
    DateTimeOffset? DumpCaptureTimeUtc,
    TimeSpan? TargetLocalOffset,
    string? TargetMachineName,
    int? TargetProcessId,
    IReadOnlyDictionary<string, string>? EnvironmentVariables);

/// <summary>
/// Provides session-snapshot metadata for host-integrated call models and diagnostics.
/// </summary>
public interface ISessionSnapshotProvider
{
    /// <summary>
    /// Gets the current session snapshot.
    /// </summary>
    /// <returns>Session snapshot data captured by the active host implementation.</returns>
    SessionSnapshot GetSnapshot();
}

/// <summary>
/// Provides read-only external heap/object access for overlay memory and projection models.
/// </summary>
public interface IExternalObjectModel
{
    /// <summary>Tries to get the runtime type of an external object.</summary>
    bool TryGetObjectType(ExternalObjectRef obj, out TypeHandle runtimeType);

    /// <summary>Tries to read a string object with a maximum character cap.</summary>
    bool TryReadString(ExternalObjectRef obj, int maxChars, out string? value);

    /// <summary>Tries to get array length for an external array object.</summary>
    bool TryGetArrayLength(ExternalObjectRef arrayObj, out int length);

    /// <summary>Tries to read an object field value.</summary>
    bool TryReadField(ExternalObjectRef obj, FieldHandle field, out ExternalValue value);

    /// <summary>Tries to read an array element value.</summary>
    bool TryReadArrayElement(ExternalObjectRef arrayObj, int index, out ExternalValue value);
}

/// <summary>
/// Optional low-level process-memory reader contract for advanced host scenarios.
/// </summary>
public interface IProcessMemoryReader
{
    /// <summary>
    /// Tries to copy memory bytes from a process address into a destination span.
    /// </summary>
    /// <param name="address">Starting address to read.</param>
    /// <param name="destination">Destination span receiving copied bytes.</param>
    /// <returns><see langword="true"/> when read succeeds; otherwise <see langword="false"/>.</returns>
    bool TryRead(ulong address, Span<byte> destination);
}

/// <summary>
/// Represents host-provided initial frame values used to seed interpreter execution.
/// </summary>
/// <param name="ThisObject">Optional instance receiver for instance methods.</param>
/// <param name="Arguments">Ordered argument values.</param>
/// <param name="LocalsByName">Best-effort local values keyed by display name.</param>
public sealed record FrameSeed(
    ExternalObjectRef? ThisObject,
    IReadOnlyList<ExternalValue> Arguments,
    IReadOnlyDictionary<string, ExternalValue> LocalsByName);

/// <summary>
/// Provides host-specific frame seeding for a chosen thread/frame pair.
/// </summary>
public interface IFrameSeeder
{
    /// <summary>
    /// Tries to materialize frame seed values for the requested frame.
    /// </summary>
    /// <param name="thread">Target thread identity.</param>
    /// <param name="frame">Target frame identity.</param>
    /// <param name="seed">Resolved seed values when available.</param>
    /// <returns><see langword="true"/> when seeding succeeds; otherwise <see langword="false"/>.</returns>
    bool TrySeedFrame(ExternalThreadId thread, ExternalFrameId frame, out FrameSeed seed);
}

/// <summary>
/// Host-defined runtime method identity.
/// </summary>
/// <param name="Value">Runtime method identifier value.</param>
public readonly record struct RuntimeMethodId(ulong Value);

/// <summary>
/// Host-defined runtime module identity.
/// </summary>
/// <param name="Value">Runtime module identifier value.</param>
public readonly record struct RuntimeModuleId(ulong Value);

/// <summary>
/// Maps host runtime method identity to metadata identity and token.
/// </summary>
/// <param name="RuntimeId">Host runtime method identity.</param>
/// <param name="Module">Metadata module identity.</param>
/// <param name="MethodToken">Method-definition metadata token when known.</param>
public sealed record RuntimeMethodInfo(RuntimeMethodId RuntimeId, ModuleId Module, int MethodToken);

/// <summary>
/// Bridges host runtime identities to metadata abstraction identities.
/// </summary>
public interface IRuntimeMetadataBridge
{
    /// <summary>Tries to map a runtime method identity to metadata information.</summary>
    bool TryMapMethod(RuntimeMethodId runtimeMethod, out RuntimeMethodInfo info);

    /// <summary>Tries to map a runtime module identity to a metadata module identity.</summary>
    bool TryMapModule(RuntimeModuleId runtimeModule, out ModuleId module);
}

/// <summary>
/// Optional host-specific generic context resolver.
/// </summary>
public interface IGenericContextResolver
{
    /// <summary>
    /// Tries to resolve generic context for a runtime method and optional receiver.
    /// </summary>
    /// <param name="runtimeMethod">Runtime method identity.</param>
    /// <param name="thisObj">Optional runtime receiver object.</param>
    /// <param name="ctx">Resolved generic context when available.</param>
    /// <returns><see langword="true"/> when context resolution succeeds; otherwise <see langword="false"/>.</returns>
    bool TryResolveGenericContext(RuntimeMethodId runtimeMethod, ExternalObjectRef? thisObj, out GenericContext ctx);
}
