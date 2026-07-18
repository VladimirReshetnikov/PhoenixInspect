using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Reflection;
using Interpreter.Core.Abstractions;
using Interpreter.Host.Dump.ClrMD;

namespace Interpreter.Product.DumpQuery;

/// <summary>Identifies whether a common dump object has a TypeDef-backed or TypeDef-less array runtime type.</summary>
public enum DumpObjectRuntimeTypeKind
{
    /// <summary>The exact runtime type is correlated with one metadata TypeDef.</summary>
    TypeDefinition = 1,

    /// <summary>The exact runtime type is a TypeDef-less array with recursively retained raw topology.</summary>
    Array = 2,
}

/// <summary>Identifies one exact dump object independently of how a caller selected it.</summary>
/// <remarks>
/// This draft W7 product identity deliberately excludes handles, host labels, expressions, fields, and static slots.
/// Equal physical objects selected through different sources therefore compare equal while their bindings do not.
/// </remarks>
public sealed class DumpObjectIdentity : IEquatable<DumpObjectIdentity>
{
    /// <summary>Gets the shared maximum copied runtime type-name character count.</summary>
    public const int MaximumTypeNameCharacters = ClrmdStaticRuntimeTypeIdentity.MaximumRuntimeNameCharacters;

    private readonly ImmutableArray<byte> canonicalBytes;

    private DumpObjectIdentity(
        DumpObjectRuntimeTypeKind runtimeTypeKind,
        ClrmdSnapshotIdentity snapshot,
        int pointerWidth,
        ulong address,
        ulong methodTable,
        ClrmdRuntimeModuleIdentity? runtimeModule,
        ModuleContentIdentity? moduleContent,
        int? typeMetadataToken,
        string typeName,
        ClrmdStaticRuntimeTypeIdentity? arrayRuntimeType)
    {
        RuntimeTypeKind = runtimeTypeKind;
        Snapshot = snapshot;
        PointerWidth = pointerWidth;
        Address = address;
        MethodTable = methodTable;
        RuntimeModule = runtimeModule;
        ModuleContent = moduleContent;
        TypeMetadataToken = typeMetadataToken;
        TypeName = typeName;
        ArrayRuntimeType = arrayRuntimeType;

        var writer = new CanonicalReplayEncoding.Writer("dump-object-identity", 3);
        writer.WriteInt32((int)runtimeTypeKind);
        DumpObjectCanonical.WriteSnapshot(writer, snapshot);
        writer.WriteInt32(pointerWidth);
        writer.WriteUInt64(address);
        writer.WriteUInt64(methodTable);
        writer.WriteString(typeName);
        if (runtimeTypeKind == DumpObjectRuntimeTypeKind.TypeDefinition)
        {
            DumpObjectCanonical.WriteRuntimeModule(writer, runtimeModule!.Value);
            DumpObjectCanonical.WriteModuleContent(writer, moduleContent!);
            writer.WriteInt32(typeMetadataToken!.Value);
        }
        else
        {
            writer.WriteLengthPrefixedBytes(arrayRuntimeType!.CanonicalBytes.AsSpan());
        }
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the immutable dump snapshot containing the object.</summary>
    public ClrmdSnapshotIdentity Snapshot { get; }

    /// <summary>Gets whether the runtime type is TypeDef-backed or a TypeDef-less array.</summary>
    public DumpObjectRuntimeTypeKind RuntimeTypeKind { get; }

    /// <summary>Gets the target architecture pointer width.</summary>
    public int PointerWidth { get; }

    /// <summary>Gets the exact nonzero managed object address.</summary>
    public ulong Address { get; }

    /// <summary>Gets the exact nonzero runtime method-table pointer.</summary>
    public ulong MethodTable { get; }

    /// <summary>Gets the snapshot-scoped runtime module defining the exact runtime type.</summary>
    public ClrmdRuntimeModuleIdentity? RuntimeModule { get; }

    /// <summary>Gets the complete counted metadata identity containing the exact runtime TypeDef.</summary>
    public ModuleContentIdentity? ModuleContent { get; }

    /// <summary>Gets the non-nil exact runtime TypeDef token.</summary>
    public int? TypeMetadataToken { get; }

    /// <summary>Gets the exact ordinal decoded runtime type name.</summary>
    public string TypeName { get; }

    /// <summary>Gets complete raw array topology only for <see cref="DumpObjectRuntimeTypeKind.Array"/>.</summary>
    public ClrmdStaticRuntimeTypeIdentity? ArrayRuntimeType { get; }

    /// <summary>Gets the shared fixed type-name character bound.</summary>
    public static EvaluationDeterministicBound DeclaredTypeNameCharacterBound =>
        new(ClrmdStaticRuntimeTypeIdentity.RuntimeNameCharacterBoundName, MaximumTypeNameCharacters);

    /// <summary>Gets a defensive copy of the source-agnostic canonical object identity.</summary>
    public ImmutableArray<byte> CanonicalBytes => CanonicalReplayEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of <see cref="CanonicalBytes"/>.</summary>
    public string Sha256 { get; }

    /// <summary>Creates a source-agnostic exact object identity from detached physical facts.</summary>
    /// <param name="snapshot">Immutable dump identity.</param>
    /// <param name="pointerWidth">Target architecture pointer width, exactly four or eight.</param>
    /// <param name="address">Exact nonzero managed object address.</param>
    /// <param name="methodTable">Exact nonzero method-table pointer.</param>
    /// <param name="runtimeModule">Snapshot-scoped module defining the exact runtime type.</param>
    /// <param name="moduleContent">Complete counted metadata identity containing the TypeDef.</param>
    /// <param name="typeMetadataToken">Non-nil exact runtime TypeDef token.</param>
    /// <param name="typeName">Exact ordinal decoded runtime type name.</param>
    /// <returns>An immutable intrinsic identity containing no selection provenance.</returns>
    /// <exception cref="ArgumentException">Snapshot, module, metadata, or name facts are incomplete.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A pointer is invalid for the target architecture.</exception>
    public static DumpObjectIdentity Create(
        ClrmdSnapshotIdentity snapshot,
        int pointerWidth,
        ulong address,
        ulong methodTable,
        ClrmdRuntimeModuleIdentity runtimeModule,
        ModuleContentIdentity moduleContent,
        int typeMetadataToken,
        string typeName)
    {
        CanonicalReplayEncoding.ValidatePointerWidth(pointerWidth);
        CanonicalReplayEncoding.ValidatePointerValue(address, pointerWidth, allowZero: false, nameof(address));
        CanonicalReplayEncoding.ValidatePointerValue(methodTable, pointerWidth, allowZero: false, nameof(methodTable));
        DumpObjectCanonical.ValidateRuntimeModule(runtimeModule, snapshot, pointerWidth, nameof(runtimeModule));
        ArgumentNullException.ThrowIfNull(moduleContent);
        CanonicalReplayEncoding.ValidateMetadataToken(typeMetadataToken, 0x02, nameof(typeMetadataToken));
        DumpObjectCanonical.ValidateName(typeName, nameof(typeName));

        return new DumpObjectIdentity(
            DumpObjectRuntimeTypeKind.TypeDefinition,
            snapshot,
            pointerWidth,
            address,
            methodTable,
            runtimeModule,
            moduleContent,
            typeMetadataToken,
            typeName,
            arrayRuntimeType: null);
    }

    /// <summary>Creates source-agnostic identity directly from an exact raw-header-first object reference.</summary>
    /// <param name="value">Exact slot target, raw method-table header, and detached runtime TypeDef facts.</param>
    /// <returns>An immutable common identity that does not require or retain object extent.</returns>
    public static DumpObjectIdentity FromExactObject(ClrmdExactObjectReference value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var runtimeType = value.HeaderRuntimeType;
        if (runtimeType.Kind == ClrmdStaticRuntimeTypeIdentityKind.TypeDefinition)
        {
            return Create(
                value.Snapshot,
                value.PointerWidth,
                value.Address,
                value.MethodTable,
                runtimeType.RuntimeModule!.Value,
                runtimeType.ModuleContent!,
                runtimeType.TypeDefinitionToken!.Value,
                runtimeType.FullName);
        }
        return new DumpObjectIdentity(
            DumpObjectRuntimeTypeKind.Array,
            value.Snapshot,
            value.PointerWidth,
            value.Address,
            value.MethodTable,
            runtimeModule: null,
            moduleContent: null,
            typeMetadataToken: null,
            runtimeType.FullName,
            runtimeType);
    }

    /// <summary>Correlates raw-header-first physical evidence with an independently established W2/W6 object identity.</summary>
    /// <param name="value">Exact raw header and post-header runtime type evidence.</param>
    /// <param name="identity">Existing source-agnostic object identity.</param>
    /// <returns><paramref name="identity"/> after exact physical parity validation.</returns>
    /// <exception cref="ArgumentException">Address, header, runtime type, module, or TypeDef facts disagree.</exception>
    public static DumpObjectIdentity FromExactObject(
        ClrmdExactObjectReference value,
        DumpObjectIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(identity);
        var runtimeType = value.HeaderRuntimeType;
        var runtimeTypeMatches = identity.RuntimeTypeKind switch
        {
            DumpObjectRuntimeTypeKind.TypeDefinition =>
                runtimeType.Kind == ClrmdStaticRuntimeTypeIdentityKind.TypeDefinition &&
                runtimeType.RuntimeModule == identity.RuntimeModule &&
                Equals(runtimeType.ModuleContent, identity.ModuleContent) &&
                runtimeType.TypeDefinitionToken == identity.TypeMetadataToken,
            DumpObjectRuntimeTypeKind.Array =>
                runtimeType.Kind == ClrmdStaticRuntimeTypeIdentityKind.Array &&
                runtimeType.Equals(identity.ArrayRuntimeType),
            _ => false,
        };
        if (value.Snapshot != identity.Snapshot ||
            value.PointerWidth != identity.PointerWidth ||
            value.Address != identity.Address ||
            value.MethodTable != identity.MethodTable ||
            !runtimeTypeMatches ||
            !string.Equals(runtimeType.FullName, identity.TypeName, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Raw-header-first evidence and the independently established object identity disagree.",
                nameof(value));
        }
        return identity;
    }

    /// <inheritdoc />
    public bool Equals(DumpObjectIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as DumpObjectIdentity);

    /// <inheritdoc />
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
}

/// <summary>Identifies the typed source that selected one exact dump object.</summary>
public enum DumpObjectProvenanceKind
{
    /// <summary>A counted CLR strong-handle slot selected the object.</summary>
    StrongHandle = 1,

    /// <summary>The host supplied an already exact object through an explicitly identified boundary.</summary>
    HostSuppliedExactObject = 2,

    /// <summary>A bound static-field expression and exact nonzero slot selected the object.</summary>
    StaticFieldExpression = 3,
}

/// <summary>Identifies the detached ClrMD handle kinds admitted by the strong-handle selector.</summary>
public enum DumpStrongHandleKind
{
    /// <summary>An ordinary strong handle.</summary>
    Strong = 1,

    /// <summary>A pinned handle.</summary>
    Pinned = 2,

    /// <summary>A reference-counted strong handle.</summary>
    RefCounted = 3,

    /// <summary>An asynchronous pinned handle.</summary>
    AsyncPinned = 4,

    /// <summary>A sized-reference strong handle.</summary>
    SizedRef = 5,
}

/// <summary>Retains typed strong-handle selection identity without making it part of object identity.</summary>
public sealed class DumpStrongHandleSourceIdentity : IEquatable<DumpStrongHandleSourceIdentity>
{
    private readonly ImmutableArray<byte> canonicalBytes;

    private DumpStrongHandleSourceIdentity(
        DumpObjectIdentity selectedObject,
        ClrmdExactObjectReference objectEvidence,
        ClrmdSnapshotIdentity snapshot,
        int pointerWidth,
        ulong handleSlotAddress,
        DumpStrongHandleKind handleKind,
        uint referenceCount,
        ClrmdRawMemoryEvidence slotEvidence)
    {
        SelectedObject = selectedObject;
        ObjectEvidence = objectEvidence;
        Snapshot = snapshot;
        PointerWidth = pointerWidth;
        HandleSlotAddress = handleSlotAddress;
        HandleKind = handleKind;
        ReferenceCount = referenceCount;
        SlotEvidence = slotEvidence;

        var writer = new CanonicalReplayEncoding.Writer("dump-strong-handle-source", 3);
        writer.WriteLengthPrefixedBytes(selectedObject.CanonicalBytes.AsSpan());
        writer.WriteLengthPrefixedBytes(objectEvidence.CanonicalBytes.AsSpan());
        DumpObjectCanonical.WriteSnapshot(writer, snapshot);
        writer.WriteInt32(pointerWidth);
        writer.WriteUInt64(handleSlotAddress);
        writer.WriteInt32(DumpObjectCanonical.Tag(handleKind));
        writer.WriteUInt32(referenceCount);
        writer.WriteLengthPrefixedBytes(slotEvidence.CanonicalBytes.AsSpan());
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the immutable dump snapshot containing the handle slot.</summary>
    public ClrmdSnapshotIdentity Snapshot { get; }

    /// <summary>Gets the intrinsic object selected by this exact handle source.</summary>
    public DumpObjectIdentity SelectedObject { get; }

    /// <summary>Gets the raw header and post-header runtime type evidence for the selected object.</summary>
    public ClrmdExactObjectReference ObjectEvidence { get; }

    /// <summary>Gets the target architecture pointer width.</summary>
    public int PointerWidth { get; }

    /// <summary>Gets the nonzero CLR handle-slot address.</summary>
    public ulong HandleSlotAddress { get; }

    /// <summary>Gets the exact admitted detached strong-handle kind.</summary>
    public DumpStrongHandleKind HandleKind { get; }

    /// <summary>Gets the exact ClrHandle reference count; positive only for an admitted RefCounted root.</summary>
    public uint ReferenceCount { get; }

    /// <summary>Gets the exact pointer-width handle-slot read proving selection of <see cref="SelectedObject"/>.</summary>
    public ClrmdRawMemoryEvidence SlotEvidence { get; }

    /// <summary>Gets a defensive copy of the typed source's canonical bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => CanonicalReplayEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of <see cref="CanonicalBytes"/>.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one typed strong-handle source identity.</summary>
    /// <param name="selectedObject">Intrinsic exact object selected through the handle.</param>
    /// <param name="objectEvidence">Complete exact object/header/type evidence independently proving the selected identity.</param>
    /// <param name="pointerWidth">Target architecture pointer width.</param>
    /// <param name="handleSlotAddress">Exact nonzero handle-slot address.</param>
    /// <param name="handleKind">Exact admitted detached strong-handle kind.</param>
    /// <param name="referenceCount">Exact ClrHandle reference count; positive for RefCounted and zero otherwise.</param>
    /// <param name="slotEvidence">Exact pointer-width slot read containing the selected object address.</param>
    /// <returns>An immutable typed handle source.</returns>
    /// <exception cref="ArgumentException">
    /// Object identity, exact object evidence, architecture, slot evidence, handle kind, or reference-count topology disagrees.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">The handle kind or slot does not fit the admitted target architecture.</exception>
    public static DumpStrongHandleSourceIdentity Create(
        DumpObjectIdentity selectedObject,
        ClrmdExactObjectReference objectEvidence,
        int pointerWidth,
        ulong handleSlotAddress,
        DumpStrongHandleKind handleKind,
        uint referenceCount,
        ClrmdRawMemoryEvidence slotEvidence)
    {
        ArgumentNullException.ThrowIfNull(selectedObject);
        ArgumentNullException.ThrowIfNull(objectEvidence);
        var snapshot = selectedObject.Snapshot;
        if (!DumpObjectIdentity.FromExactObject(objectEvidence, selectedObject).Equals(selectedObject))
        {
            throw new ArgumentException(
                "The intrinsic object identity must exactly match the retained object/header/type evidence.",
                nameof(objectEvidence));
        }

        CanonicalReplayEncoding.ValidatePointerWidth(pointerWidth);
        if (pointerWidth != selectedObject.PointerWidth)
        {
            throw new ArgumentException(
                "The handle and selected object require the same target pointer width.",
                nameof(pointerWidth));
        }

        CanonicalReplayEncoding.ValidateAddressRange(
            handleSlotAddress,
            pointerWidth,
            pointerWidth,
            nameof(handleSlotAddress));
        if (!Enum.IsDefined(handleKind))
        {
            throw new ArgumentOutOfRangeException(nameof(handleKind));
        }
        if (handleKind == DumpStrongHandleKind.RefCounted
            ? referenceCount == 0
            : referenceCount != 0)
        {
            throw new ArgumentException(
                "A RefCounted handle is a strong root only with a positive exact count; every other admitted kind requires zero.",
                nameof(referenceCount));
        }
        ArgumentNullException.ThrowIfNull(slotEvidence);
        if (slotEvidence.Snapshot != snapshot ||
            !slotEvidence.IsExact ||
            slotEvidence.Address != handleSlotAddress ||
            slotEvidence.RequestedLength != pointerWidth ||
            DecodePointer(slotEvidence.Bytes.AsSpan(), pointerWidth) != selectedObject.Address)
        {
            throw new ArgumentException(
                "Strong-handle selection requires an exact matching slot read containing the selected object address.",
                nameof(slotEvidence));
        }

        return new DumpStrongHandleSourceIdentity(
            selectedObject,
            objectEvidence,
            snapshot,
            pointerWidth,
            handleSlotAddress,
            handleKind,
            referenceCount,
            slotEvidence);
    }

    /// <inheritdoc />
    public bool Equals(DumpStrongHandleSourceIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as DumpStrongHandleSourceIdentity);

    /// <inheritdoc />
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    private static ulong DecodePointer(ReadOnlySpan<byte> bytes, int pointerWidth) => pointerWidth switch
    {
        sizeof(uint) when bytes.Length == sizeof(uint) => BinaryPrimitives.ReadUInt32LittleEndian(bytes),
        sizeof(ulong) when bytes.Length == sizeof(ulong) => BinaryPrimitives.ReadUInt64LittleEndian(bytes),
        _ => throw new ArgumentException("A complete target pointer is required.", nameof(bytes)),
    };
}

/// <summary>Retains the typed boundary through which a host supplied one already exact object.</summary>
public sealed class DumpHostSuppliedObjectSourceIdentity : IEquatable<DumpHostSuppliedObjectSourceIdentity>
{
    private readonly ImmutableArray<byte> canonicalBytes;

    private DumpHostSuppliedObjectSourceIdentity(
        DumpObjectIdentity selectedObject,
        ClrmdSnapshotIdentity snapshot,
        string sourceName)
    {
        SelectedObject = selectedObject;
        Snapshot = snapshot;
        SourceName = sourceName;

        var writer = new CanonicalReplayEncoding.Writer("dump-host-supplied-object-source", 1);
        writer.WriteLengthPrefixedBytes(selectedObject.CanonicalBytes.AsSpan());
        DumpObjectCanonical.WriteSnapshot(writer, snapshot);
        writer.WriteString(sourceName);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the immutable dump snapshot containing the supplied object.</summary>
    public ClrmdSnapshotIdentity Snapshot { get; }

    /// <summary>Gets the intrinsic exact object supplied through this host boundary.</summary>
    public DumpObjectIdentity SelectedObject { get; }

    /// <summary>Gets the stable host boundary name, not a local path or display label.</summary>
    public string SourceName { get; }

    /// <summary>Gets a defensive copy of the typed source's canonical bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => CanonicalReplayEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of <see cref="CanonicalBytes"/>.</summary>
    public string Sha256 { get; }

    /// <summary>Creates a typed host-supplied source identity.</summary>
    /// <param name="selectedObject">Intrinsic exact object supplied by the host.</param>
    /// <param name="sourceName">Stable non-empty host boundary name.</param>
    /// <returns>An immutable host-supplied source.</returns>
    /// <exception cref="ArgumentException">The stable source name is invalid.</exception>
    public static DumpHostSuppliedObjectSourceIdentity Create(
        DumpObjectIdentity selectedObject,
        string sourceName)
    {
        ArgumentNullException.ThrowIfNull(selectedObject);
        var snapshot = selectedObject.Snapshot;
        DumpObjectCanonical.ValidateName(sourceName, nameof(sourceName));
        return new DumpHostSuppliedObjectSourceIdentity(
            selectedObject,
            snapshot,
            sourceName);
    }

    /// <inheritdoc />
    public bool Equals(DumpHostSuppliedObjectSourceIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as DumpHostSuppliedObjectSourceIdentity);

    /// <inheritdoc />
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
}

/// <summary>Retains expression, context, symbol, declaration, storage, read, and target identity for a static object.</summary>
/// <remarks>
/// This is a typed static-field source, never a synthetic handle. Its composed observation retains the application
/// domain, nonzero slot, detached pointer/header reads, and exact intrinsic target selected by the expression.
/// </remarks>
public sealed class DumpStaticFieldExpressionSourceIdentity : IEquatable<DumpStaticFieldExpressionSourceIdentity>
{
    private readonly ImmutableArray<byte> canonicalBytes;

    private DumpStaticFieldExpressionSourceIdentity(StaticFieldObservation observation)
    {
        Observation = observation;

        var writer = new CanonicalReplayEncoding.Writer("dump-static-field-expression-source", 1);
        writer.WriteLengthPrefixedBytes(observation.CanonicalBytes.AsSpan());
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the immutable dump snapshot selected by the composed observation.</summary>
    public ClrmdSnapshotIdentity Snapshot => Observation.Snapshot;

    /// <summary>Gets the exact projected expression descriptor retained by symbol binding.</summary>
    public StaticFieldExpressionDescriptor Expression => Observation.SymbolBinding.Descriptor;

    /// <summary>Gets the digest of the exact projected expression descriptor.</summary>
    public string ExpressionSha256 => Expression.Sha256;

    /// <summary>Gets the typed identity containing only frame and import facts actually consulted by binding.</summary>
    public DumpConsultedBindingContextIdentity ConsultedContext => Observation.SymbolBinding.ConsultedContext;

    /// <summary>Gets the digest of <see cref="ConsultedContext"/>.</summary>
    public string ContextSha256 => ConsultedContext.Sha256;

    /// <summary>Gets the exact exhaustive typed symbol binding outcome.</summary>
    public StaticFieldSymbolBindingOutcome SymbolBinding => Observation.SymbolBinding;

    /// <summary>Gets the exact symbol plus exact object-valued host observation.</summary>
    public StaticFieldObservation Observation { get; }

    /// <summary>Gets a defensive copy of the complete typed static-source bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => CanonicalReplayEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of <see cref="CanonicalBytes"/>.</summary>
    public string Sha256 { get; }

    /// <summary>Creates a typed static-field object source from one fully composed exact observation.</summary>
    /// <param name="observation">Composed exact-symbol, exact-object observation.</param>
    /// <returns>An immutable typed static-field source.</returns>
    /// <exception cref="ArgumentException">The symbol or host observation is not an exact object selection.</exception>
    public static DumpStaticFieldExpressionSourceIdentity Create(StaticFieldObservation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        if (observation.SymbolStatus != StaticFieldSymbolIdentityStatus.Exact ||
            observation.HostObservation is not { } hostObservation ||
            hostObservation.Status != ClrmdStaticFieldObservationStatus.Exact ||
            hostObservation.Value?.Kind != ClrmdStaticFieldTerminalKind.ObjectReference)
        {
            throw new ArgumentException(
                "A static object source requires exact symbol and exact object-reference storage evidence.",
                nameof(observation));
        }

        return new DumpStaticFieldExpressionSourceIdentity(observation);
    }

    /// <inheritdoc />
    public bool Equals(DumpStaticFieldExpressionSourceIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as DumpStaticFieldExpressionSourceIdentity);

    /// <inheritdoc />
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
}

/// <summary>Represents exactly one typed object-selection source without optional-payload ambiguity.</summary>
public sealed class DumpObjectProvenance : IEquatable<DumpObjectProvenance>
{
    private readonly ImmutableArray<byte> canonicalBytes;

    private DumpObjectProvenance(
        DumpObjectProvenanceKind kind,
        DumpStrongHandleSourceIdentity? strongHandle,
        DumpHostSuppliedObjectSourceIdentity? hostSupplied,
        DumpStaticFieldExpressionSourceIdentity? staticField)
    {
        Kind = kind;
        StrongHandle = strongHandle;
        HostSupplied = hostSupplied;
        StaticField = staticField;

        var writer = new CanonicalReplayEncoding.Writer("dump-object-provenance", 1);
        writer.WriteInt32(DumpObjectCanonical.Tag(kind));
        var activeBytes = kind switch
        {
            DumpObjectProvenanceKind.StrongHandle => strongHandle!.CanonicalBytes,
            DumpObjectProvenanceKind.HostSuppliedExactObject => hostSupplied!.CanonicalBytes,
            DumpObjectProvenanceKind.StaticFieldExpression => staticField!.CanonicalBytes,
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
        writer.WriteLengthPrefixedBytes(activeBytes.AsSpan());
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the active typed selection source.</summary>
    public DumpObjectProvenanceKind Kind { get; }

    /// <summary>Gets the strong-handle source only when <see cref="Kind"/> selects it.</summary>
    public DumpStrongHandleSourceIdentity? StrongHandle { get; }

    /// <summary>Gets the host-supplied source only when <see cref="Kind"/> selects it.</summary>
    public DumpHostSuppliedObjectSourceIdentity? HostSupplied { get; }

    /// <summary>Gets the static-field source only when <see cref="Kind"/> selects it.</summary>
    public DumpStaticFieldExpressionSourceIdentity? StaticField { get; }

    /// <summary>Gets the immutable snapshot retained by the active typed source.</summary>
    public ClrmdSnapshotIdentity Snapshot => Kind switch
    {
        DumpObjectProvenanceKind.StrongHandle => StrongHandle!.Snapshot,
        DumpObjectProvenanceKind.HostSuppliedExactObject => HostSupplied!.Snapshot,
        DumpObjectProvenanceKind.StaticFieldExpression => StaticField!.Snapshot,
        _ => throw new InvalidOperationException("The provenance kind is invalid."),
    };

    /// <summary>Gets a defensive copy of the complete typed provenance bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => CanonicalReplayEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of <see cref="CanonicalBytes"/>.</summary>
    public string Sha256 { get; }

    /// <summary>Creates provenance for a counted strong-handle source.</summary>
    /// <param name="source">Typed handle identity.</param>
    /// <returns>A provenance union with only <see cref="StrongHandle"/> active.</returns>
    public static DumpObjectProvenance FromStrongHandle(DumpStrongHandleSourceIdentity source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return new DumpObjectProvenance(DumpObjectProvenanceKind.StrongHandle, source, null, null);
    }

    /// <summary>Creates provenance for an explicit host-supplied exact object boundary.</summary>
    /// <param name="source">Typed host-supplied source identity.</param>
    /// <returns>A provenance union with only <see cref="HostSupplied"/> active.</returns>
    public static DumpObjectProvenance FromHostSuppliedExactObject(DumpHostSuppliedObjectSourceIdentity source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return new DumpObjectProvenance(DumpObjectProvenanceKind.HostSuppliedExactObject, null, source, null);
    }

    /// <summary>Creates provenance for an exact object selected by one static-field expression.</summary>
    /// <param name="source">Typed static expression, context, symbol, storage, and target source identity.</param>
    /// <returns>A provenance union with only <see cref="StaticField"/> active.</returns>
    public static DumpObjectProvenance FromStaticFieldExpression(DumpStaticFieldExpressionSourceIdentity source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return new DumpObjectProvenance(DumpObjectProvenanceKind.StaticFieldExpression, null, null, source);
    }

    /// <inheritdoc />
    public bool Equals(DumpObjectProvenance? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as DumpObjectProvenance);

    /// <inheritdoc />
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
}

/// <summary>Binds one intrinsic exact object to one complete typed selection provenance.</summary>
/// <remarks>
/// Semantic object equality uses <see cref="Identity"/>. Binding equality additionally includes provenance and the
/// source-specific evidence, so selecting the same address through a handle and static expression remains distinct.
/// No synthesized "request" is exposed: discovered handle slots and consulted binding facts are post-request results.
/// </remarks>
public sealed class DumpObjectBinding : IEquatable<DumpObjectBinding>
{
    private readonly ImmutableArray<byte> canonicalBytes;

    private DumpObjectBinding(
        DumpObjectIdentity identity,
        DumpObjectProvenance provenance)
    {
        Identity = identity;
        Provenance = provenance;

        var writer = new CanonicalReplayEncoding.Writer("dump-object-binding", 1);
        writer.WriteLengthPrefixedBytes(identity.CanonicalBytes.AsSpan());
        writer.WriteLengthPrefixedBytes(provenance.CanonicalBytes.AsSpan());
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the source-agnostic exact object identity.</summary>
    public DumpObjectIdentity Identity { get; }

    /// <summary>Gets the typed source that selected the object.</summary>
    public DumpObjectProvenance Provenance { get; }

    /// <summary>Gets a defensive copy of the complete binding identity.</summary>
    public ImmutableArray<byte> CanonicalBytes => CanonicalReplayEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of <see cref="CanonicalBytes"/>.</summary>
    public string Sha256 { get; }

    /// <summary>Creates a snapshot-consistent object and complete typed selection-provenance binding.</summary>
    /// <param name="identity">Source-agnostic exact object identity.</param>
    /// <param name="provenance">Typed selection source.</param>
    /// <returns>An immutable binding whose intrinsic identity and selection provenance remain independently inspectable.</returns>
    /// <exception cref="ArgumentException">Snapshots or the selected intrinsic object disagree.</exception>
    public static DumpObjectBinding Create(
        DumpObjectIdentity identity,
        DumpObjectProvenance provenance)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(provenance);
        if (identity.Snapshot != provenance.Snapshot)
        {
            throw new ArgumentException("Object identity and provenance belong to different snapshots.", nameof(provenance));
        }

        var selectedObject = provenance.Kind switch
        {
            DumpObjectProvenanceKind.StrongHandle => provenance.StrongHandle!.SelectedObject,
            DumpObjectProvenanceKind.HostSuppliedExactObject => provenance.HostSupplied!.SelectedObject,
            DumpObjectProvenanceKind.StaticFieldExpression => DumpObjectIdentity.FromExactObject(
                provenance.StaticField!.Observation.HostObservation!.Value!.ObjectReference!),
            _ => throw new ArgumentOutOfRangeException(nameof(provenance)),
        };
        if (!identity.Equals(selectedObject))
        {
            throw new ArgumentException("Typed provenance selected a different intrinsic object.", nameof(provenance));
        }

        return new DumpObjectBinding(identity, provenance);
    }

    /// <inheritdoc />
    public bool Equals(DumpObjectBinding? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as DumpObjectBinding);

    /// <inheritdoc />
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
}

/// <summary>Freezes one raw ClrMD instance-field projection used only to prove Nullable&lt;Int32&gt; layout.</summary>
/// <remarks>
/// This detached value retains the physical row, name, offset, width, and observed runtime type. It deliberately
/// carries no semantic role: <see cref="StaticFieldNullableInt32RuntimeLayoutIdentity"/> assigns the HasValue or
/// value role only after comparing it with exact Product-owned metadata and runtime-core-library type anchors.
/// </remarks>
public sealed class StaticFieldNullableRuntimeFieldIdentity :
    IEquatable<StaticFieldNullableRuntimeFieldIdentity>
{
    private readonly ImmutableArray<byte> canonicalBytes;

    private StaticFieldNullableRuntimeFieldIdentity(
        ClrmdStaticRuntimeTypeIdentity declaringRuntimeType,
        int fieldDefinitionToken,
        string name,
        int offset,
        int size,
        ClrmdStaticRuntimeTypeIdentity observedType)
    {
        DeclaringRuntimeType = declaringRuntimeType;
        FieldDefinitionToken = fieldDefinitionToken;
        Name = name;
        Offset = offset;
        Size = size;
        ObservedType = observedType;

        var writer = new CanonicalReplayEncoding.Writer("static-field-nullable-runtime-field-identity", 1);
        writer.WriteLengthPrefixedBytes(declaringRuntimeType.CanonicalBytes.AsSpan());
        writer.WriteInt32(fieldDefinitionToken);
        writer.WriteString(name);
        writer.WriteInt32(offset);
        writer.WriteInt32(size);
        writer.WriteLengthPrefixedBytes(observedType.CanonicalBytes.AsSpan());
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact constructed runtime type whose instance-field catalog supplied this row.</summary>
    public ClrmdStaticRuntimeTypeIdentity DeclaringRuntimeType { get; }

    /// <summary>Gets the non-nil runtime-reported FieldDef token.</summary>
    public int FieldDefinitionToken { get; }

    /// <summary>Gets the exact bounded ordinal runtime field name.</summary>
    public string Name { get; }

    /// <summary>Gets the nonnegative byte offset relative to the specialized value storage.</summary>
    public int Offset { get; }

    /// <summary>Gets the positive runtime-reported child storage width.</summary>
    public int Size { get; }

    /// <summary>Gets the detached raw runtime type observed for the child field.</summary>
    public ClrmdStaticRuntimeTypeIdentity ObservedType { get; }

    /// <summary>Gets a defensive copy of the versioned canonical runtime-field bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => CanonicalReplayEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of <see cref="CanonicalBytes"/>.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one detached raw instance-field projection without assigning a semantic child role.</summary>
    /// <param name="declaringRuntimeType">The exact constructed runtime type whose field catalog was inspected.</param>
    /// <param name="fieldDefinitionToken">The non-nil FieldDef token reported by the runtime field.</param>
    /// <param name="name">The exact non-empty ordinal runtime field name.</param>
    /// <param name="offset">The nonnegative byte offset relative to specialized value storage.</param>
    /// <param name="size">The positive runtime-reported child storage width.</param>
    /// <param name="observedType">The exact detached runtime type reported for the child.</param>
    /// <returns>An immutable raw runtime child projection.</returns>
    /// <exception cref="ArgumentException">The name or observed type is absent.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The token, offset, or size is invalid.</exception>
    public static StaticFieldNullableRuntimeFieldIdentity Create(
        ClrmdStaticRuntimeTypeIdentity declaringRuntimeType,
        int fieldDefinitionToken,
        string name,
        int offset,
        int size,
        ClrmdStaticRuntimeTypeIdentity observedType)
    {
        ArgumentNullException.ThrowIfNull(declaringRuntimeType);
        CanonicalReplayEncoding.ValidateMetadataToken(fieldDefinitionToken, 0x04, nameof(fieldDefinitionToken));
        ArgumentNullException.ThrowIfNull(name);
        if (name.Length == 0 ||
            name.Length > ClrmdStaticRuntimeTypeIdentity.MaximumRuntimeNameCharacters ||
            name.Any(char.IsControl))
        {
            throw new ArgumentException("A non-empty bounded decoded runtime field name is required.", nameof(name));
        }
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);
        ArgumentNullException.ThrowIfNull(observedType);
        return new StaticFieldNullableRuntimeFieldIdentity(
            declaringRuntimeType,
            fieldDefinitionToken,
            name,
            offset,
            size,
            observedType);
    }

    /// <summary>Adapts one metadata-blind Host nullable child row into Product's semantic-composition input.</summary>
    /// <param name="runtimeField">The exact detached raw Host row from the exhausted nullable field catalog.</param>
    /// <returns>An equivalent Product-owned raw row; no semantic HasValue or value role is assigned.</returns>
    public static StaticFieldNullableRuntimeFieldIdentity Create(
        ClrmdStaticNullableRuntimeFieldIdentity runtimeField)
    {
        ArgumentNullException.ThrowIfNull(runtimeField);
        return Create(
            runtimeField.DeclaringRuntimeType,
            runtimeField.FieldDefinitionToken,
            runtimeField.Name,
            runtimeField.Offset,
            runtimeField.Size,
            runtimeField.ObservedType);
    }

    /// <inheritdoc />
    public bool Equals(StaticFieldNullableRuntimeFieldIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as StaticFieldNullableRuntimeFieldIdentity);

    /// <inheritdoc />
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
}

/// <summary>
/// Proves the specialized Nullable&lt;Int32&gt; physical layout by composing exact metadata with raw runtime fields.
/// </summary>
/// <remarks>
/// Product owns every semantic assertion in this proof: the exact resolved Nullable TypeDef, its two directly owned
/// FieldDefs, the runtime-selected Boolean and Int32 core-library anchors, and parity with the detached ClrMD rows.
/// Host receives only the resulting storage size and offsets, so the physical decoder never rebinds metadata names.
/// </remarks>
public sealed class StaticFieldNullableInt32RuntimeLayoutIdentity :
    IEquatable<StaticFieldNullableInt32RuntimeLayoutIdentity>
{
    private static readonly ImmutableArray<byte> HasValueSignature = ImmutableArray.Create<byte>(0x06, 0x02);
    private static readonly ImmutableArray<byte> ValueSignature = ImmutableArray.Create<byte>(0x06, 0x13, 0x00);
    private readonly ImmutableArray<byte> canonicalBytes;

    private StaticFieldNullableInt32RuntimeLayoutIdentity(
        StaticFieldSymbolDeclarationIdentity declaration,
        ClrmdStaticRuntimeTypeIdentity runtimeNullableType,
        StaticFieldTypeAncestryIdentity systemBooleanTypeAncestry,
        int storageSize,
        StaticFieldDefinitionIdentity hasValueFieldDefinition,
        StaticFieldNullableRuntimeFieldIdentity hasValueRuntimeField,
        StaticFieldDefinitionIdentity valueFieldDefinition,
        StaticFieldNullableRuntimeFieldIdentity valueRuntimeField)
    {
        Declaration = declaration;
        RuntimeNullableType = runtimeNullableType;
        SystemBooleanTypeAncestry = systemBooleanTypeAncestry;
        StorageSize = storageSize;
        HasValueFieldDefinition = hasValueFieldDefinition;
        HasValueRuntimeField = hasValueRuntimeField;
        ValueFieldDefinition = valueFieldDefinition;
        ValueRuntimeField = valueRuntimeField;

        var writer = new CanonicalReplayEncoding.Writer("static-field-nullable-int32-runtime-layout-identity", 1);
        writer.WriteLengthPrefixedBytes(declaration.CanonicalBytes.AsSpan());
        writer.WriteLengthPrefixedBytes(runtimeNullableType.CanonicalBytes.AsSpan());
        writer.WriteLengthPrefixedBytes(systemBooleanTypeAncestry.CanonicalBytes.AsSpan());
        writer.WriteInt32(storageSize);
        writer.WriteLengthPrefixedBytes(hasValueFieldDefinition.CanonicalBytes.AsSpan());
        writer.WriteLengthPrefixedBytes(hasValueRuntimeField.CanonicalBytes.AsSpan());
        writer.WriteLengthPrefixedBytes(valueFieldDefinition.CanonicalBytes.AsSpan());
        writer.WriteLengthPrefixedBytes(valueRuntimeField.CanonicalBytes.AsSpan());
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact Product declaration proved to be Nullable&lt;Int32&gt;.</summary>
    public StaticFieldSymbolDeclarationIdentity Declaration { get; }

    /// <summary>Gets the raw constructed Nullable runtime type whose layout was inspected.</summary>
    public ClrmdStaticRuntimeTypeIdentity RuntimeNullableType { get; }

    /// <summary>Gets exact ancestry for runtime-selected core-library System.Boolean.</summary>
    public StaticFieldTypeAncestryIdentity SystemBooleanTypeAncestry { get; }

    /// <summary>Gets the complete specialized value storage size in bytes.</summary>
    public int StorageSize { get; }

    /// <summary>Gets the exact directly owned metadata FieldDef for the Boolean HasValue child.</summary>
    public StaticFieldDefinitionIdentity HasValueFieldDefinition { get; }

    /// <summary>Gets the matching raw runtime HasValue child projection.</summary>
    public StaticFieldNullableRuntimeFieldIdentity HasValueRuntimeField { get; }

    /// <summary>Gets the exact directly owned metadata FieldDef for the generic value child.</summary>
    public StaticFieldDefinitionIdentity ValueFieldDefinition { get; }

    /// <summary>Gets the matching raw runtime value child projection specialized as Int32.</summary>
    public StaticFieldNullableRuntimeFieldIdentity ValueRuntimeField { get; }

    /// <summary>Gets a defensive copy of the complete versioned canonical proof bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => CanonicalReplayEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of <see cref="CanonicalBytes"/>.</summary>
    public string Sha256 { get; }

    /// <summary>Creates an exact semantic-to-runtime proof for specialized Nullable&lt;Int32&gt; child storage.</summary>
    /// <param name="declaration">The exact admitted Nullable&lt;Int32&gt; static-field declaration.</param>
    /// <param name="runtimeNullableType">The raw constructed runtime type observed for the outer static field.</param>
    /// <param name="systemBooleanTypeAncestry">Exact runtime-core-library System.Boolean ancestry.</param>
    /// <param name="storageSize">The complete specialized value storage size.</param>
    /// <param name="hasValueFieldDefinition">The exact directly owned Boolean HasValue FieldDef.</param>
    /// <param name="hasValueRuntimeField">The raw runtime field correlated with that FieldDef.</param>
    /// <param name="valueFieldDefinition">The exact directly owned generic-parameter value FieldDef.</param>
    /// <param name="valueRuntimeField">The raw runtime field correlated with that FieldDef and specialized as Int32.</param>
    /// <returns>An immutable proof from which the metadata-blind Host layout can be created.</returns>
    /// <exception cref="ArgumentException">Any metadata, runtime type, child row, signature, or role disagrees.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A child range does not fit the specialized storage.</exception>
    public static StaticFieldNullableInt32RuntimeLayoutIdentity Create(
        StaticFieldSymbolDeclarationIdentity declaration,
        ClrmdStaticRuntimeTypeIdentity runtimeNullableType,
        StaticFieldTypeAncestryIdentity systemBooleanTypeAncestry,
        int storageSize,
        StaticFieldDefinitionIdentity hasValueFieldDefinition,
        StaticFieldNullableRuntimeFieldIdentity hasValueRuntimeField,
        StaticFieldDefinitionIdentity valueFieldDefinition,
        StaticFieldNullableRuntimeFieldIdentity valueRuntimeField)
    {
        ArgumentNullException.ThrowIfNull(declaration);
        ArgumentNullException.ThrowIfNull(runtimeNullableType);
        ArgumentNullException.ThrowIfNull(systemBooleanTypeAncestry);
        ArgumentNullException.ThrowIfNull(hasValueFieldDefinition);
        ArgumentNullException.ThrowIfNull(hasValueRuntimeField);
        ArgumentNullException.ThrowIfNull(valueFieldDefinition);
        ArgumentNullException.ThrowIfNull(valueRuntimeField);
        if (declaration.DeclaredValueKind != StaticFieldDeclaredValueKind.NullableInt32 ||
            declaration.NullableType is not { } nullable ||
            declaration.SystemInt32TypeAncestry is not { } systemInt32)
        {
            throw new ArgumentException("A complete Nullable<Int32> declaration is required.", nameof(declaration));
        }

        StaticFieldRuntimeComposition.ValidateRuntimeTypeAnchor(
            runtimeNullableType,
            nullable.TargetTypeAncestry,
            expectedPrimitive: false,
            expectedGenericArgumentCount: 1,
            StaticFieldRuntimeComposition.ConstructedRuntimeFullName(
                nullable.TargetTypeAncestry.SubjectType,
                systemInt32.SubjectType),
            nameof(runtimeNullableType));
        StaticFieldRuntimeComposition.ValidateRuntimeTypeAnchor(
            runtimeNullableType.GenericArguments[0],
            systemInt32,
            expectedPrimitive: true,
            expectedGenericArgumentCount: 0,
            StaticFieldRuntimeComposition.RuntimeFullName(systemInt32.SubjectType),
            nameof(runtimeNullableType));
        ValidateSystemBoolean(systemBooleanTypeAncestry, systemInt32, nullable.TargetTypeAncestry);
        StaticFieldRuntimeComposition.ValidateRuntimeTypeAnchor(
            hasValueRuntimeField.ObservedType,
            systemBooleanTypeAncestry,
            expectedPrimitive: true,
            expectedGenericArgumentCount: 0,
            StaticFieldRuntimeComposition.RuntimeFullName(systemBooleanTypeAncestry.SubjectType),
            nameof(hasValueRuntimeField));
        StaticFieldRuntimeComposition.ValidateRuntimeTypeAnchor(
            valueRuntimeField.ObservedType,
            systemInt32,
            expectedPrimitive: true,
            expectedGenericArgumentCount: 0,
            StaticFieldRuntimeComposition.RuntimeFullName(systemInt32.SubjectType),
            nameof(valueRuntimeField));

        var nullableDefinition = nullable.TargetTypeAncestry.SubjectType;
        ValidateChild(
            nullableDefinition,
            runtimeNullableType,
            hasValueFieldDefinition,
            hasValueRuntimeField,
            "hasValue",
            sizeof(byte),
            HasValueSignature,
            nameof(hasValueFieldDefinition));
        ValidateChild(
            nullableDefinition,
            runtimeNullableType,
            valueFieldDefinition,
            valueRuntimeField,
            "value",
            sizeof(int),
            ValueSignature,
            nameof(valueFieldDefinition));
        if (hasValueFieldDefinition.FieldDefinitionToken == valueFieldDefinition.FieldDefinitionToken)
        {
            throw new ArgumentException("Nullable child roles require distinct FieldDef rows.");
        }

        ValidateRange(storageSize, hasValueRuntimeField.Offset, hasValueRuntimeField.Size, nameof(hasValueRuntimeField));
        ValidateRange(storageSize, valueRuntimeField.Offset, valueRuntimeField.Size, nameof(valueRuntimeField));
        var hasValueEnd = hasValueRuntimeField.Offset + hasValueRuntimeField.Size;
        var valueEnd = valueRuntimeField.Offset + valueRuntimeField.Size;
        if (hasValueRuntimeField.Offset < valueEnd && valueRuntimeField.Offset < hasValueEnd)
        {
            throw new ArgumentException("Nullable child storage ranges must be distinct and non-overlapping.");
        }

        return new StaticFieldNullableInt32RuntimeLayoutIdentity(
            declaration,
            runtimeNullableType,
            systemBooleanTypeAncestry,
            storageSize,
            hasValueFieldDefinition,
            hasValueRuntimeField,
            valueFieldDefinition,
            valueRuntimeField);
    }

    /// <summary>
    /// Correlates a complete metadata-blind Host layout with Product-selected nullable child FieldDefs and type anchors.
    /// </summary>
    /// <param name="declaration">The exact admitted Nullable&lt;Int32&gt; outer static declaration.</param>
    /// <param name="systemBooleanTypeAncestry">Exact runtime-core-library System.Boolean ancestry.</param>
    /// <param name="hasValueFieldDefinition">The exact directly owned Boolean HasValue FieldDef selected by Product.</param>
    /// <param name="valueFieldDefinition">The exact directly owned generic-parameter value FieldDef selected by Product.</param>
    /// <param name="runtimeLayout">The complete raw Host payload extent and exhausted runtime child catalog.</param>
    /// <returns>An exact semantic layout proof usable to construct the metadata-blind physical request.</returns>
    /// <exception cref="ArgumentException">
    /// The outer mapping, child tokens, catalog uniqueness, metadata signatures, raw runtime types, or offsets disagree.
    /// </exception>
    public static StaticFieldNullableInt32RuntimeLayoutIdentity Create(
        StaticFieldSymbolDeclarationIdentity declaration,
        StaticFieldTypeAncestryIdentity systemBooleanTypeAncestry,
        StaticFieldDefinitionIdentity hasValueFieldDefinition,
        StaticFieldDefinitionIdentity valueFieldDefinition,
        ClrmdStaticNullableRuntimeLayoutIdentity runtimeLayout)
    {
        ArgumentNullException.ThrowIfNull(declaration);
        ArgumentNullException.ThrowIfNull(systemBooleanTypeAncestry);
        ArgumentNullException.ThrowIfNull(hasValueFieldDefinition);
        ArgumentNullException.ThrowIfNull(valueFieldDefinition);
        ArgumentNullException.ThrowIfNull(runtimeLayout);
        if (runtimeLayout.RuntimeMapping.Field.ExpectedDecoderKind != ClrmdStaticExpectedDecoderKind.NullableInt32 ||
            !runtimeLayout.RuntimeMapping.Field.ObservedFieldType.Equals(
                runtimeLayout.Fields[0].DeclaringRuntimeType))
        {
            throw new ArgumentException(
                "The Host layout must describe the exact outer Nullable<Int32> runtime mapping.",
                nameof(runtimeLayout));
        }

        var hasValueMatches = runtimeLayout.Fields
            .Where(field => field.FieldDefinitionToken == hasValueFieldDefinition.FieldDefinitionToken)
            .Take(2)
            .ToImmutableArray();
        var valueMatches = runtimeLayout.Fields
            .Where(field => field.FieldDefinitionToken == valueFieldDefinition.FieldDefinitionToken)
            .Take(2)
            .ToImmutableArray();
        if (hasValueMatches.Length != 1 || valueMatches.Length != 1)
        {
            throw new ArgumentException(
                "Each Product-selected nullable child FieldDef must map to exactly one raw runtime row.",
                nameof(runtimeLayout));
        }

        return Create(
            declaration,
            runtimeLayout.RuntimeMapping.Field.ObservedFieldType,
            systemBooleanTypeAncestry,
            runtimeLayout.StorageSize,
            hasValueFieldDefinition,
            StaticFieldNullableRuntimeFieldIdentity.Create(hasValueMatches[0]),
            valueFieldDefinition,
            StaticFieldNullableRuntimeFieldIdentity.Create(valueMatches[0]));
    }

    /// <inheritdoc />
    public bool Equals(StaticFieldNullableInt32RuntimeLayoutIdentity? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as StaticFieldNullableInt32RuntimeLayoutIdentity);

    /// <inheritdoc />
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal ClrmdStaticNullableInt32Layout CreatePhysicalLayout() =>
        ClrmdStaticNullableInt32Layout.Create(
            StorageSize,
            HasValueRuntimeField.Offset,
            ValueRuntimeField.Offset);

    private static void ValidateSystemBoolean(
        StaticFieldTypeAncestryIdentity systemBoolean,
        StaticFieldTypeAncestryIdentity systemInt32,
        StaticFieldTypeAncestryIdentity nullable)
    {
        var type = systemBoolean.SubjectType;
        var flags = (TypeAttributes)type.TypeAttributes;
        if (systemBoolean.Classification != StaticFieldTypeClassification.ValueType ||
            systemBoolean.CoreLibrary is null ||
            systemInt32.CoreLibrary is null ||
            nullable.CoreLibrary is null ||
            !systemBoolean.CoreLibrary.Equals(systemInt32.CoreLibrary) ||
            !systemBoolean.CoreLibrary.Equals(nullable.CoreLibrary) ||
            !type.MetadataModule.Equals(systemBoolean.CoreLibrary.MetadataModule) ||
            !type.IsTopLevel || type.GenericArity != 0 || type.IsAbstract ||
            !string.Equals(type.NamespaceName, "System", StringComparison.Ordinal) ||
            !string.Equals(type.TypeName, "Boolean", StringComparison.Ordinal) ||
            (flags & TypeAttributes.VisibilityMask) != TypeAttributes.Public ||
            (flags & TypeAttributes.Sealed) == 0)
        {
            throw new ArgumentException(
                "The Boolean anchor must be exact public sealed System.Boolean in the same runtime-selected core library.",
                nameof(systemBoolean));
        }
    }

    private static void ValidateChild(
        StaticFieldTypeDefinitionIdentity nullableDefinition,
        ClrmdStaticRuntimeTypeIdentity runtimeNullableType,
        StaticFieldDefinitionIdentity metadataField,
        StaticFieldNullableRuntimeFieldIdentity runtimeField,
        string expectedName,
        int expectedSize,
        ImmutableArray<byte> expectedSignature,
        string parameterName)
    {
        var attributes = (FieldAttributes)metadataField.Attributes;
        var excluded = FieldAttributes.Static | FieldAttributes.Literal | FieldAttributes.HasFieldRVA;
        if (!metadataField.DeclaringType.Equals(nullableDefinition) ||
            !runtimeField.DeclaringRuntimeType.Equals(runtimeNullableType) ||
            runtimeField.FieldDefinitionToken != metadataField.FieldDefinitionToken ||
            !string.Equals(metadataField.Name, expectedName, StringComparison.Ordinal) ||
            !string.Equals(runtimeField.Name, expectedName, StringComparison.Ordinal) ||
            runtimeField.Size != expectedSize ||
            (attributes & excluded) != 0 ||
            metadataField.IsThreadStatic || metadataField.IsContextStatic ||
            !metadataField.Signature.AsSpan().SequenceEqual(expectedSignature.AsSpan()))
        {
            throw new ArgumentException(
                "Nullable child metadata and raw runtime row do not describe the required directly owned instance field.",
                parameterName);
        }
    }

    private static void ValidateRange(int storageSize, int offset, int size, string parameterName)
    {
        if (storageSize <= 0 || offset < 0 || size <= 0 || offset > storageSize || size > storageSize - offset)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                "The exact child storage range must fit completely in the specialized Nullable value.");
        }
    }
}

/// <summary>Classifies whether canonical symbol identity represents one exact declaration or a failed binding.</summary>
public enum StaticFieldSymbolIdentityStatus
{
    /// <summary>One exact symbol declaration was selected.</summary>
    Exact = 1,

    /// <summary>Binding stopped without an exact declaration; the canonical failure identity is retained.</summary>
    Failed = 2,
}

/// <summary>Composes canonical product symbol identity with one matching physical host observation.</summary>
/// <remarks>
/// The draft bridge keeps product binding identity distinct from host declaration/storage evidence. Exact symbol
/// composition compares every shared snapshot/module/metadata/token/name/signature/value-shape fact once physical
/// declaration mapping succeeds. It can also retain a typed non-exact Host prefix when exact product binding was
/// followed by physical module/type/field mapping failure. Failed symbol composition never manufactures Host work.
/// </remarks>
public sealed class StaticFieldObservation : IEquatable<StaticFieldObservation>
{
    private readonly ImmutableArray<byte> canonicalBytes;

    private StaticFieldObservation(
        StaticFieldSymbolIdentityStatus symbolStatus,
        StaticFieldSymbolBindingOutcome symbolBinding,
        ClrmdStaticFieldValueObservation? hostObservation,
        StaticFieldNullableInt32RuntimeLayoutIdentity? nullableInt32RuntimeLayout,
        StaticFieldRuntimeAssignabilityProof? runtimeAssignabilityProof)
    {
        Snapshot = new ClrmdSnapshotIdentity(symbolBinding.SnapshotSha256);
        SymbolStatus = symbolStatus;
        SymbolBinding = symbolBinding;
        HostObservation = hostObservation;
        NullableInt32RuntimeLayout = nullableInt32RuntimeLayout;
        RuntimeAssignabilityProof = runtimeAssignabilityProof;

        var writer = new CanonicalReplayEncoding.Writer("static-field-observation", 3);
        DumpObjectCanonical.WriteSnapshot(writer, Snapshot);
        writer.WriteInt32(DumpObjectCanonical.Tag(symbolStatus));
        writer.WriteLengthPrefixedBytes(symbolBinding.CanonicalBytes.AsSpan());
        writer.WriteBoolean(hostObservation is not null);
        if (hostObservation is not null)
        {
            writer.WriteLengthPrefixedBytes(hostObservation.CanonicalBytes.AsSpan());
        }
        writer.WriteBoolean(nullableInt32RuntimeLayout is not null);
        if (nullableInt32RuntimeLayout is not null)
        {
            writer.WriteLengthPrefixedBytes(nullableInt32RuntimeLayout.CanonicalBytes.AsSpan());
        }
        writer.WriteBoolean(runtimeAssignabilityProof is not null);
        if (runtimeAssignabilityProof is not null)
        {
            writer.WriteLengthPrefixedBytes(runtimeAssignabilityProof.CanonicalBytes.AsSpan());
        }
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the immutable dump snapshot shared by symbol and host evidence.</summary>
    public ClrmdSnapshotIdentity Snapshot { get; }

    /// <summary>Gets whether the canonical symbol identity is exact or failed.</summary>
    public StaticFieldSymbolIdentityStatus SymbolStatus { get; }

    /// <summary>Gets the complete typed expression, consulted-context, search, candidate, and disposition identity.</summary>
    public StaticFieldSymbolBindingOutcome SymbolBinding { get; }

    /// <summary>Gets the exact product symbol declaration only when <see cref="SymbolStatus"/> is exact.</summary>
    public StaticFieldSymbolDeclarationIdentity? SymbolDeclaration => SymbolBinding.SelectedDeclaration;

    /// <summary>Gets a defensive copy of the exact or failed canonical symbol identity.</summary>
    public ImmutableArray<byte> SymbolCanonicalBytes => SymbolBinding.CanonicalBytes;

    /// <summary>Gets the validated digest of <see cref="SymbolCanonicalBytes"/>.</summary>
    public string SymbolSha256 => SymbolBinding.Sha256;

    /// <summary>Gets the snapshot-consistent physical declaration/storage/value observation.</summary>
    public ClrmdStaticFieldValueObservation? HostObservation { get; }

    /// <summary>Gets Product's exact Nullable child-layout proof when the physical request used that decoder.</summary>
    public StaticFieldNullableInt32RuntimeLayoutIdentity? NullableInt32RuntimeLayout { get; }

    /// <summary>
    /// Gets Product's exact runtime assignability proof whenever a reference decoder reached a matched non-null raw
    /// target, including a string whose later length or character read stopped; otherwise null.
    /// </summary>
    public StaticFieldRuntimeAssignabilityProof? RuntimeAssignabilityProof { get; }

    /// <summary>Gets a defensive copy of the composed canonical observation bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => CanonicalReplayEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of <see cref="CanonicalBytes"/>.</summary>
    public string Sha256 { get; }

    /// <summary>
    /// Creates the sole production physical request after exact Product binding and runtime-declaration correlation.
    /// </summary>
    /// <param name="symbolBinding">One exact exhaustive Product symbol binding.</param>
    /// <param name="runtimeDeclaringType">The uniquely retained raw ClrMD declaring type.</param>
    /// <param name="runtimeFieldDefinitionToken">The runtime-reported non-nil FieldDef token.</param>
    /// <param name="runtimeFieldName">The exact bounded runtime-reported field name.</param>
    /// <param name="runtimeFieldAttributes">The complete raw runtime-reported FieldAttributes bits.</param>
    /// <param name="runtimeReportsThreadStatic">The raw ClrMD thread-local storage flag.</param>
    /// <param name="runtimeReportsContextStatic">The raw ClrMD context-local storage flag.</param>
    /// <param name="observedFieldType">The exact raw type observed from ClrStaticField.Type.</param>
    /// <param name="mappingCounters">Exact exhaustive bounded runtime type and static-field catalogs.</param>
    /// <param name="nullableInt32RuntimeLayout">Product's exact child-layout proof only for Nullable&lt;Int32&gt;.</param>
    /// <returns>A minimal metadata-blind Host request whose decoder tag was derived from the exact Product symbol.</returns>
    /// <exception cref="ArgumentException">Binding, mapping, raw declaration, type anchors, or layout disagree.</exception>
    public static ClrmdStaticFieldEvaluationRequest CreatePhysicalRequest(
        StaticFieldSymbolBindingOutcome symbolBinding,
        ClrmdStaticRuntimeTypeIdentity runtimeDeclaringType,
        int runtimeFieldDefinitionToken,
        string runtimeFieldName,
        FieldAttributes runtimeFieldAttributes,
        bool runtimeReportsThreadStatic,
        bool runtimeReportsContextStatic,
        ClrmdStaticRuntimeTypeIdentity observedFieldType,
        ClrmdStaticRuntimeDeclarationMappingCounters mappingCounters,
        StaticFieldNullableInt32RuntimeLayoutIdentity? nullableInt32RuntimeLayout = null)
    {
        ArgumentNullException.ThrowIfNull(symbolBinding);
        ArgumentNullException.ThrowIfNull(runtimeDeclaringType);
        ArgumentNullException.ThrowIfNull(observedFieldType);
        ArgumentNullException.ThrowIfNull(mappingCounters);
        if (symbolBinding.Status != StaticFieldBindingStatus.Exact ||
            symbolBinding.SelectedDeclaration is not { } declaration)
        {
            throw new ArgumentException(
                "Physical request construction requires one exact exhaustive Product binding.",
                nameof(symbolBinding));
        }

        var expectedDecoder = ExpectedDecoder(declaration.DeclaredValueKind);
        var runtimeField = ClrmdStaticRuntimeFieldIdentity.Create(
            runtimeDeclaringType,
            runtimeFieldDefinitionToken,
            runtimeFieldName,
            runtimeFieldAttributes,
            runtimeReportsThreadStatic,
            runtimeReportsContextStatic,
            expectedDecoder,
            observedFieldType);
        var mapping = ClrmdStaticRuntimeDeclarationMappingIdentity.Create(
            runtimeDeclaringType,
            runtimeField,
            mappingCounters);
        ValidateMatchingDeclarations(declaration, mapping);
        var physicalLayout = ValidateNullableLayout(declaration, mapping, nullableInt32RuntimeLayout);
        return ClrmdStaticFieldEvaluationRequest.Create(mapping, physicalLayout);
    }

    /// <summary>Composes one exact product symbol declaration with matching physical Host evidence or mapping failure.</summary>
    /// <param name="symbolBinding">Exact exhaustive typed binding selecting one declaration.</param>
    /// <param name="hostObservation">Physical host outcome for the same declaration and snapshot.</param>
    /// <param name="nullableInt32RuntimeLayout">Exact Product child-layout proof when the Host request is nullable.</param>
    /// <param name="runtimeAssignabilityProof">
    /// Exact Product proof required when Host reached a matched non-null reference target, otherwise null.
    /// </param>
    /// <returns>An immutable exact-symbol composition; physical declaration, storage, and value may independently fail.</returns>
    /// <exception cref="ArgumentException">Snapshots or shared declaration facts disagree.</exception>
    public static StaticFieldObservation FromExactSymbol(
        StaticFieldSymbolBindingOutcome symbolBinding,
        ClrmdStaticFieldValueObservation hostObservation,
        StaticFieldNullableInt32RuntimeLayoutIdentity? nullableInt32RuntimeLayout = null,
        StaticFieldRuntimeAssignabilityProof? runtimeAssignabilityProof = null)
    {
        ArgumentNullException.ThrowIfNull(symbolBinding);
        ArgumentNullException.ThrowIfNull(hostObservation);
        if (symbolBinding.Status != StaticFieldBindingStatus.Exact ||
            symbolBinding.SelectedDeclaration is not { } symbolDeclaration)
        {
            throw new ArgumentException(
                "Exact symbol composition requires one exact exhaustive binding outcome.",
                nameof(symbolBinding));
        }

        if (!string.Equals(symbolBinding.SnapshotSha256, hostObservation.Snapshot.Sha256, StringComparison.Ordinal))
        {
            throw new ArgumentException("Symbol and host observations belong to different snapshots.", nameof(hostObservation));
        }

        if (hostObservation.Request is { } request)
        {
            ValidateMatchingDeclarations(symbolDeclaration, request.RuntimeMapping);
            var physicalLayout = ValidateNullableLayout(
                symbolDeclaration,
                request.RuntimeMapping,
                nullableInt32RuntimeLayout);
            if ((physicalLayout is null) != (request.NullableInt32Layout is null) ||
                physicalLayout is not null && !physicalLayout.Equals(request.NullableInt32Layout))
            {
                throw new ArgumentException(
                    "Product's exact Nullable layout proof and the metadata-blind Host request disagree.",
                    nameof(nullableInt32RuntimeLayout));
            }

            ValidateRuntimeAssignability(
                symbolDeclaration,
                hostObservation,
                runtimeAssignabilityProof);
        }
        else if (hostObservation.Status == ClrmdStaticFieldObservationStatus.Exact ||
                 hostObservation.SlotAddress.HasValue ||
                 !hostObservation.Reads.IsEmpty ||
                 hostObservation.TargetEvidence is not null ||
                 hostObservation.StorageAcquisitionEvidence is not null ||
                 nullableInt32RuntimeLayout is not null ||
                 runtimeAssignabilityProof is not null)
        {
            throw new ArgumentException(
                "A pre-request physical mapping failure cannot retain layout, acquisition, storage, reads, target evidence, or exact status.",
                nameof(hostObservation));
        }

        return new StaticFieldObservation(
            StaticFieldSymbolIdentityStatus.Exact,
            symbolBinding,
            hostObservation,
            nullableInt32RuntimeLayout,
            runtimeAssignabilityProof);
    }

    /// <summary>Retains one typed failed binding while proving physical storage was not consulted.</summary>
    /// <param name="symbolBinding">Any non-exact typed binding outcome with no selected declaration.</param>
    /// <returns>An immutable failed-symbol composition.</returns>
    /// <exception cref="ArgumentException"><paramref name="symbolBinding"/> is exact or selects a declaration.</exception>
    public static StaticFieldObservation FromFailedSymbol(StaticFieldSymbolBindingOutcome symbolBinding)
    {
        ArgumentNullException.ThrowIfNull(symbolBinding);
        if (symbolBinding.Status == StaticFieldBindingStatus.Exact || symbolBinding.SelectedDeclaration is not null)
        {
            throw new ArgumentException(
                "Failed-symbol composition requires a non-exact binding without a selected declaration.",
                nameof(symbolBinding));
        }

        return new StaticFieldObservation(
            StaticFieldSymbolIdentityStatus.Failed,
            symbolBinding,
            hostObservation: null,
            nullableInt32RuntimeLayout: null,
            runtimeAssignabilityProof: null);
    }

    /// <inheritdoc />
    public bool Equals(StaticFieldObservation? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as StaticFieldObservation);

    /// <inheritdoc />
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    private static ClrmdStaticNullableInt32Layout? ValidateNullableLayout(
        StaticFieldSymbolDeclarationIdentity symbol,
        ClrmdStaticRuntimeDeclarationMappingIdentity mapping,
        StaticFieldNullableInt32RuntimeLayoutIdentity? nullableInt32RuntimeLayout)
    {
        var nullableRequired = symbol.DeclaredValueKind == StaticFieldDeclaredValueKind.NullableInt32;
        if (nullableRequired != (nullableInt32RuntimeLayout is not null))
        {
            throw new ArgumentException(
                "Exactly a Nullable<Int32> request requires Product's complete semantic child-layout proof.",
                nameof(nullableInt32RuntimeLayout));
        }
        if (nullableInt32RuntimeLayout is null)
        {
            return null;
        }
        if (!nullableInt32RuntimeLayout.Declaration.Equals(symbol) ||
            !nullableInt32RuntimeLayout.RuntimeNullableType.Equals(mapping.Field.ObservedFieldType))
        {
            throw new ArgumentException(
                "The Nullable child-layout proof must describe this exact declaration and observed runtime field type.",
                nameof(nullableInt32RuntimeLayout));
        }
        return nullableInt32RuntimeLayout.CreatePhysicalLayout();
    }

    private static void ValidateRuntimeAssignability(
        StaticFieldSymbolDeclarationIdentity declaration,
        ClrmdStaticFieldValueObservation hostObservation,
        StaticFieldRuntimeAssignabilityProof? proof)
    {
        var request = hostObservation.Request!;
        var isReference = request.ValueShape is
            ClrmdStaticFieldValueShape.String or ClrmdStaticFieldValueShape.ObjectReference;
        var terminalObject = hostObservation.Value?.Kind switch
        {
            ClrmdStaticFieldTerminalKind.String => hostObservation.Value.StringValue!.ObjectReference,
            ClrmdStaticFieldTerminalKind.ObjectReference => hostObservation.Value.ObjectReference,
            _ => null,
        };
        ClrmdExactObjectReference? matchedTarget = null;
        if (isReference && hostObservation.TargetEvidence is { Kind: ClrmdStaticTargetEvidenceKind.Matched } target)
        {
            matchedTarget = ClrmdExactObjectReference.Create(target);
        }
        else if (isReference)
        {
            matchedTarget = terminalObject;
        }

        if (matchedTarget is null)
        {
            if (proof is not null)
            {
                throw new ArgumentException(
                    "Runtime assignability evidence is valid only after a matched non-null reference target.",
                    nameof(proof));
            }
            return;
        }

        if (proof is null ||
            !proof.Declaration.Equals(declaration) ||
            !proof.ObjectReference.Equals(matchedTarget))
        {
            throw new ArgumentException(
                "Every matched non-null reference target requires Product's exact proof for this declaration and object.",
                nameof(proof));
        }

        if (terminalObject is not null && !terminalObject.Equals(matchedTarget))
        {
            throw new ArgumentException(
                "The exact terminal object and runtime assignability target disagree.",
                nameof(hostObservation));
        }
    }

    private static void ValidateMatchingDeclarations(
        StaticFieldSymbolDeclarationIdentity symbol,
        ClrmdStaticRuntimeDeclarationMappingIdentity mapping)
    {
        ArgumentNullException.ThrowIfNull(symbol);
        ArgumentNullException.ThrowIfNull(mapping);
        var runtimeOwner = mapping.DeclaringType;
        var runtimeField = mapping.Field;
        StaticFieldRuntimeComposition.ValidateRuntimeTypeAnchor(
            runtimeOwner,
            symbol.DeclaringTypeAncestry,
            expectedPrimitive: false,
            expectedGenericArgumentCount: 0,
            StaticFieldRuntimeComposition.RuntimeFullName(symbol.DeclaringTypeAncestry.SubjectType),
            nameof(mapping));

        if (runtimeField.FieldDefinitionToken != symbol.FieldDefinitionToken ||
            !string.Equals(runtimeField.Name, symbol.FieldName, StringComparison.Ordinal) ||
            (int)runtimeField.Attributes != symbol.FieldAttributes ||
            runtimeField.RuntimeReportsThreadStatic != symbol.IsThreadStatic ||
            runtimeField.RuntimeReportsContextStatic != symbol.IsContextStatic ||
            runtimeField.ExpectedDecoderKind != ExpectedDecoder(symbol.DeclaredValueKind))
        {
            throw new ArgumentException(
                "Product metadata and raw runtime field facts do not describe the same FieldDef and storage topology.",
                nameof(mapping));
        }

        ValidateObservedFieldType(symbol, runtimeField.ObservedFieldType, nameof(mapping));
    }

    private static void ValidateObservedFieldType(
        StaticFieldSymbolDeclarationIdentity symbol,
        ClrmdStaticRuntimeTypeIdentity observedType,
        string parameterName)
    {
        switch (symbol.DeclaredValueKind)
        {
            case StaticFieldDeclaredValueKind.Int32:
            {
                var systemInt32 = symbol.SystemInt32TypeAncestry!;
                StaticFieldRuntimeComposition.ValidateRuntimeTypeAnchor(
                    observedType,
                    systemInt32,
                    expectedPrimitive: true,
                    expectedGenericArgumentCount: 0,
                    StaticFieldRuntimeComposition.RuntimeFullName(systemInt32.SubjectType),
                    parameterName);
                return;
            }
            case StaticFieldDeclaredValueKind.NullableInt32:
            {
                var nullable = symbol.NullableType!;
                var systemInt32 = symbol.SystemInt32TypeAncestry!;
                StaticFieldRuntimeComposition.ValidateRuntimeTypeAnchor(
                    observedType,
                    nullable.TargetTypeAncestry,
                    expectedPrimitive: false,
                    expectedGenericArgumentCount: 1,
                    StaticFieldRuntimeComposition.ConstructedRuntimeFullName(
                        nullable.TargetTypeAncestry.SubjectType,
                        systemInt32.SubjectType),
                    parameterName);
                StaticFieldRuntimeComposition.ValidateRuntimeTypeAnchor(
                    observedType.GenericArguments[0],
                    systemInt32,
                    expectedPrimitive: true,
                    expectedGenericArgumentCount: 0,
                    StaticFieldRuntimeComposition.RuntimeFullName(systemInt32.SubjectType),
                    parameterName);
                return;
            }
            case StaticFieldDeclaredValueKind.String:
            {
                var target = symbol.ReferenceTarget!;
                if (target.Kind != StaticFieldDeclaredReferenceKind.SystemString)
                {
                    throw new ArgumentException("A string declaration requires the exact System.String target.", parameterName);
                }
                ValidateReferenceTarget(observedType, target.TargetTypeAncestry, parameterName);
                return;
            }
            case StaticFieldDeclaredValueKind.ManagedReference:
            {
                var target = symbol.ReferenceTarget!;
                if (target.Kind != StaticFieldDeclaredReferenceKind.ManagedReference)
                {
                    throw new ArgumentException("A managed-reference declaration requires its exact resolved target.", parameterName);
                }
                ValidateReferenceTarget(observedType, target.TargetTypeAncestry, parameterName);
                return;
            }
            case StaticFieldDeclaredValueKind.Object:
            {
                var target = symbol.ReferenceTarget!;
                if (target.Kind != StaticFieldDeclaredReferenceKind.SystemObject)
                {
                    throw new ArgumentException("An object declaration requires the runtime-selected System.Object target.", parameterName);
                }
                ValidateReferenceTarget(observedType, target.TargetTypeAncestry, parameterName);
                return;
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(symbol));
        }
    }

    private static void ValidateReferenceTarget(
        ClrmdStaticRuntimeTypeIdentity observedType,
        StaticFieldTypeAncestryIdentity target,
        string parameterName) =>
        StaticFieldRuntimeComposition.ValidateRuntimeTypeAnchor(
            observedType,
            target,
            expectedPrimitive: false,
            expectedGenericArgumentCount: 0,
            StaticFieldRuntimeComposition.RuntimeFullName(target.SubjectType),
            parameterName);

    private static ClrmdStaticExpectedDecoderKind ExpectedDecoder(StaticFieldDeclaredValueKind valueKind) =>
        valueKind switch
        {
            StaticFieldDeclaredValueKind.Int32 => ClrmdStaticExpectedDecoderKind.Int32,
            StaticFieldDeclaredValueKind.NullableInt32 => ClrmdStaticExpectedDecoderKind.NullableInt32,
            StaticFieldDeclaredValueKind.String => ClrmdStaticExpectedDecoderKind.String,
            StaticFieldDeclaredValueKind.ManagedReference or StaticFieldDeclaredValueKind.Object =>
                ClrmdStaticExpectedDecoderKind.ManagedReference,
            _ => throw new ArgumentOutOfRangeException(nameof(valueKind)),
        };
}

internal static class StaticFieldRuntimeComposition
{
    internal static void ValidateRuntimeTypeAnchor(
        ClrmdStaticRuntimeTypeIdentity runtimeType,
        StaticFieldTypeAncestryIdentity metadataAncestry,
        bool expectedPrimitive,
        int expectedGenericArgumentCount,
        string expectedRuntimeFullName,
        string parameterName)
    {
        ArgumentNullException.ThrowIfNull(runtimeType);
        ArgumentNullException.ThrowIfNull(metadataAncestry);
        var metadataType = metadataAncestry.SubjectType;
        var expectedValueType = metadataAncestry.Classification is
            StaticFieldTypeClassification.ValueType or StaticFieldTypeClassification.Enum;
        var expectedInterface = metadataAncestry.Classification == StaticFieldTypeClassification.Interface;
        if (runtimeType.Kind != ClrmdStaticRuntimeTypeIdentityKind.TypeDefinition ||
            runtimeType.RuntimeModule is not { } runtimeModule ||
            runtimeType.ModuleContent is not { } runtimeContent ||
            runtimeType.TypeDefinitionToken is not { } runtimeToken ||
            metadataType.Module.PointerWidth != runtimeType.PointerWidth ||
            !RuntimeModuleMatches(metadataType.Module, runtimeModule) ||
            !metadataType.ModuleContent.Equals(runtimeContent) ||
            metadataType.TypeDefinitionToken != runtimeToken ||
            !string.Equals(expectedRuntimeFullName, runtimeType.FullName, StringComparison.Ordinal) ||
            runtimeType.IsValueType != expectedValueType ||
            runtimeType.IsPrimitive != expectedPrimitive ||
            runtimeType.IsArray ||
            runtimeType.IsInterface != expectedInterface ||
            runtimeType.GenericArguments.Length != expectedGenericArgumentCount)
        {
            throw new ArgumentException(
                "Raw runtime type facts do not match the exact Product metadata and ancestry anchor.",
                parameterName);
        }
    }

    internal static string RuntimeFullName(StaticFieldTypeDefinitionIdentity type)
    {
        ArgumentNullException.ThrowIfNull(type);
        var names = new List<string>();
        var current = type;
        while (true)
        {
            names.Add(current.TypeName);
            if (current.EnclosingType is null)
            {
                names.Reverse();
                var nestedName = string.Join("+", names);
                return current.NamespaceName.Length == 0
                    ? nestedName
                    : $"{current.NamespaceName}.{nestedName}";
            }
            current = current.EnclosingType;
        }
    }

    internal static string ConstructedRuntimeFullName(
        StaticFieldTypeDefinitionIdentity genericDefinition,
        StaticFieldTypeDefinitionIdentity argument)
    {
        var definitionName = RuntimeFullName(genericDefinition);
        var arityDelimiter = definitionName.LastIndexOf('`');
        if (arityDelimiter < 0 ||
            !int.TryParse(definitionName.AsSpan(arityDelimiter + 1), out var arity) ||
            arity != 1)
        {
            throw new ArgumentException(
                "The constructed runtime-name projection requires an exact arity-one metadata definition.",
                nameof(genericDefinition));
        }
        return $"{definitionName[..arityDelimiter]}<{RuntimeFullName(argument)}>";
    }

    internal static bool RuntimeModuleMatches(
        StaticFieldModuleInstanceIdentity metadataModule,
        ClrmdRuntimeModuleIdentity runtimeModule) =>
        string.Equals(metadataModule.SnapshotSha256, runtimeModule.Snapshot.Sha256, StringComparison.Ordinal) &&
        metadataModule.ApplicationDomainAddress == runtimeModule.AppDomainAddress &&
        metadataModule.ModuleAddress == runtimeModule.ModuleAddress &&
        metadataModule.ImageBase == runtimeModule.ImageBase &&
        metadataModule.ImageSize == runtimeModule.ImageSize;
}

internal static class DumpObjectCanonical
{
    internal static int Tag(DumpObjectProvenanceKind value) => value switch
    {
        DumpObjectProvenanceKind.StrongHandle => 1,
        DumpObjectProvenanceKind.HostSuppliedExactObject => 2,
        DumpObjectProvenanceKind.StaticFieldExpression => 3,
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    internal static int Tag(DumpStrongHandleKind value) => value switch
    {
        DumpStrongHandleKind.Strong => 1,
        DumpStrongHandleKind.Pinned => 2,
        DumpStrongHandleKind.RefCounted => 3,
        DumpStrongHandleKind.AsyncPinned => 4,
        DumpStrongHandleKind.SizedRef => 5,
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    internal static int Tag(StaticFieldSymbolIdentityStatus value) => value switch
    {
        StaticFieldSymbolIdentityStatus.Exact => 1,
        StaticFieldSymbolIdentityStatus.Failed => 2,
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    internal static void WriteSnapshot(CanonicalReplayEncoding.Writer writer, ClrmdSnapshotIdentity snapshot) =>
        writer.WriteSha256(snapshot.Sha256, nameof(snapshot));

    internal static void WriteRuntimeModule(
        CanonicalReplayEncoding.Writer writer,
        ClrmdRuntimeModuleIdentity module)
    {
        WriteSnapshot(writer, module.Snapshot);
        writer.WriteUInt64(module.AppDomainAddress);
        writer.WriteUInt64(module.ModuleAddress);
        writer.WriteUInt64(module.ImageBase);
        writer.WriteUInt64(module.ImageSize);
    }

    internal static void WriteModuleContent(
        CanonicalReplayEncoding.Writer writer,
        ModuleContentIdentity module)
    {
        writer.WriteRawBytes(module.Mvid.ToByteArray());
        writer.WriteInt32(module.MetadataLength);
        writer.WriteSha256(module.MetadataSha256, nameof(module));
    }

    internal static void ValidateRuntimeModule(
        ClrmdRuntimeModuleIdentity module,
        ClrmdSnapshotIdentity snapshot,
        int pointerWidth,
        string parameterName)
    {
        if (module.Snapshot != snapshot || module.AppDomainAddress == 0 || module.ModuleAddress == 0)
        {
            throw new ArgumentException("A complete runtime module from the same snapshot is required.", parameterName);
        }

        CanonicalReplayEncoding.ValidatePointerWidth(pointerWidth);
        var maximum = pointerWidth == sizeof(uint) ? uint.MaxValue : ulong.MaxValue;
        if (module.AppDomainAddress > maximum ||
            module.ModuleAddress > maximum ||
            module.ImageBase > maximum ||
            module.ImageSize > maximum ||
            (module.ImageBase != 0 &&
             module.ImageSize != 0 &&
             module.ImageSize - 1 > maximum - module.ImageBase))
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                "Runtime module coordinates and the mapped-image range must fit the target pointer width.");
        }
    }

    internal static void ValidateName(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            value.Length > DumpObjectIdentity.MaximumTypeNameCharacters ||
            value.Any(char.IsControl))
        {
            throw new ArgumentException(
                $"A non-empty stable ordinal name of at most {DumpObjectIdentity.MaximumTypeNameCharacters} characters is required.",
                parameterName);
        }
    }

}
