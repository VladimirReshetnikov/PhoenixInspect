namespace Interpreter.Host.Dump.ClrMD;

/// <summary>
/// Classifies why a dump-backed value observation is not exact.
/// </summary>
public enum ClrmdValueIssue
{
    /// <summary>
    /// No issue was observed; the value is exact for the supported layout.
    /// </summary>
    None,

    /// <summary>
    /// Evidence selected from a different immutable dump was supplied to this session.
    /// </summary>
    SnapshotMismatch,

    /// <summary>
    /// The selected runtime module instance was not present in this session.
    /// </summary>
    ModuleUnavailable,

    /// <summary>
    /// The selected runtime module did not expose a complete metadata image.
    /// </summary>
    MetadataUnavailable,

    /// <summary>
    /// The requested dump artifact could not be opened from the supplied location.
    /// </summary>
    ArtifactUnavailable,

    /// <summary>
    /// The dump artifact is not structurally valid for the active ClrMD reader.
    /// </summary>
    ArtifactInvalid,

    /// <summary>
    /// The dump contains a runtime configuration outside the supported single-runtime profile.
    /// </summary>
    RuntimeUnsupported,

    /// <summary>
    /// The selected object could not be revalidated in the dump heap.
    /// </summary>
    ObjectUnavailable,

    /// <summary>
    /// The requested field was absent from the runtime type description.
    /// </summary>
    FieldUnavailable,

    /// <summary>
    /// The requested runtime type was absent from the selected module.
    /// </summary>
    TypeUnavailable,

    /// <summary>
    /// The requested method was absent from the selected runtime type.
    /// </summary>
    MethodUnavailable,

    /// <summary>
    /// More than one runtime candidate matched a request that requires unique selection.
    /// </summary>
    AmbiguousMatch,

    /// <summary>
    /// Runtime method metadata was present, but no supported IL body location was available.
    /// </summary>
    MethodBodyUnavailable,

    /// <summary>
    /// The method body uses a module layout whose RVA-to-target-address mapping is not yet supported.
    /// </summary>
    MethodBodyLayoutUnsupported,

    /// <summary>
    /// The physical method header uses a valid extensibility shape outside the current twelve-byte fat profile.
    /// </summary>
    MethodHeaderUnsupported,

    /// <summary>
    /// A declared method data section has semantics outside the supported exception-table profile.
    /// </summary>
    MethodSectionUnsupported,

    /// <summary>
    /// Runtime-selected method identity disagreed with its declaring type or MethodDef name in counted dump metadata.
    /// </summary>
    MethodIdentityMismatch,

    /// <summary>
    /// Exact metadata described a valid member or getter shape outside the deliberately admitted data projection.
    /// </summary>
    MemberShapeUnsupported,

    /// <summary>
    /// The runtime field or referenced object had an incompatible type.
    /// </summary>
    TypeMismatch,

    /// <summary>
    /// Required target bytes were missing or only partially captured.
    /// </summary>
    MemoryUnavailable,

    /// <summary>
    /// Captured bytes or addresses violated the supported runtime layout.
    /// </summary>
    InvalidData,

    /// <summary>
    /// The configured observation cap intentionally truncated an otherwise readable value.
    /// </summary>
    LimitExceeded,
}
