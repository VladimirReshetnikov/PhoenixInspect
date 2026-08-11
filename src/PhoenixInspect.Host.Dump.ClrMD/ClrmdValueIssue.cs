namespace PhoenixInspect.Host.Dump.ClrMD;

/// <summary>
/// Classifies why a dump-backed value observation is not exact.
/// </summary>
public enum ClrmdValueIssue
{
    /// <summary>
    /// No issue was observed; the value is exact for the supported layout.
    /// </summary>
    None = 0,

    /// <summary>
    /// Evidence selected from a different immutable dump was supplied to this session.
    /// </summary>
    SnapshotMismatch = 1,

    /// <summary>
    /// The selected runtime module instance was not present in this session.
    /// </summary>
    ModuleUnavailable = 2,

    /// <summary>
    /// The selected runtime module did not expose a complete metadata image.
    /// </summary>
    MetadataUnavailable = 3,

    /// <summary>
    /// The requested dump artifact could not be opened from the supplied location.
    /// </summary>
    ArtifactUnavailable = 4,

    /// <summary>
    /// The dump artifact is not structurally valid for the active ClrMD reader.
    /// </summary>
    ArtifactInvalid = 5,

    /// <summary>
    /// The dump contains a runtime configuration outside the supported single-runtime profile.
    /// </summary>
    RuntimeUnsupported = 6,

    /// <summary>
    /// The selected object could not be revalidated in the dump heap.
    /// </summary>
    ObjectUnavailable = 7,

    /// <summary>
    /// The requested field was absent from the runtime type description.
    /// </summary>
    FieldUnavailable = 8,

    /// <summary>
    /// The requested runtime type was absent from the selected module.
    /// </summary>
    TypeUnavailable = 9,

    /// <summary>
    /// The requested method was absent from the selected runtime type.
    /// </summary>
    MethodUnavailable = 10,

    /// <summary>
    /// More than one runtime candidate matched a request that requires unique selection.
    /// </summary>
    AmbiguousMatch = 11,

    /// <summary>
    /// Runtime method metadata was present, but no supported IL body location was available.
    /// </summary>
    MethodBodyUnavailable = 12,

    /// <summary>
    /// The method body uses a module layout whose RVA-to-target-address mapping is not yet supported.
    /// </summary>
    MethodBodyLayoutUnsupported = 13,

    /// <summary>
    /// The physical method header uses a valid extensibility shape outside the current twelve-byte fat profile.
    /// </summary>
    MethodHeaderUnsupported = 14,

    /// <summary>
    /// A declared method data section has semantics outside the supported exception-table profile.
    /// </summary>
    MethodSectionUnsupported = 15,

    /// <summary>
    /// Runtime-selected method identity disagreed with its declaring type or MethodDef name in counted dump metadata.
    /// </summary>
    MethodIdentityMismatch = 16,

    /// <summary>
    /// Exact metadata described a valid member or getter shape outside the deliberately admitted data projection.
    /// </summary>
    MemberShapeUnsupported = 17,

    /// <summary>
    /// The runtime field or referenced object had an incompatible type.
    /// </summary>
    TypeMismatch = 18,

    /// <summary>
    /// Required target bytes were missing or only partially captured.
    /// </summary>
    MemoryUnavailable = 19,

    /// <summary>
    /// Captured bytes or addresses violated the supported runtime layout.
    /// </summary>
    InvalidData = 20,

    /// <summary>
    /// The configured observation cap intentionally truncated an otherwise readable value.
    /// </summary>
    LimitExceeded = 21,

    /// <summary>
    /// The runtime's exported contract descriptor or a descriptor field required for physical layout was absent.
    /// </summary>
    RuntimeContractUnavailable = 22,

    /// <summary>
    /// An edit-enabled module's physical generation counter was zero, below the measured runtime floor of one.
    /// </summary>
    EditGenerationCounterUnderflow = 23,
}
