using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Globalization;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using Interpreter.Core.Abstractions;
using Interpreter.Host.Abstractions;
using Interpreter.Host.Dump.ClrMD;
using Microsoft.Diagnostics.Runtime;

namespace Interpreter.Product.DumpQuery;

/// <summary>Classifies one physical runtime draft acquisition answer.</summary>
/// <remarks>Only <see cref="Exact"/> exposes acquired physical facts; every alternative is a prefix-free draft stop.</remarks>
public enum StaticFieldV2RuntimeAcquisitionResultKind
{
    /// <summary>Every requested physical fact was acquired exactly from the pinned snapshot.</summary>
    Exact = 1,

    /// <summary>A required physical fact was absent, ambiguous, or reached a declared draft bound.</summary>
    NonExact = 2,

    /// <summary>Complete acquired evidence contradicted an already-declared draft invariant.</summary>
    Invalid = 3,

    /// <summary>The frozen capability vector or this draft slice's declared coverage owns no answer here.</summary>
    Unsupported = 4,
}

/// <summary>Names the exact draft acquisition step at which one runtime acquisition stopped.</summary>
/// <remarks>
/// The stage is one-to-one with the physical steps this draft adapter performs, so a consumer never has to infer the
/// stop site from a message. <see cref="None"/> applies only to an exact draft answer.
/// </remarks>
public enum StaticFieldV2RuntimeAcquisitionStage
{
    /// <summary>No stage applies to an exact draft acquisition.</summary>
    None = 0,

    /// <summary>The frozen capability vector was consulted before any physical work.</summary>
    CapabilityVector = 1,

    /// <summary>The caller-supplied runtime-module binding set was consulted.</summary>
    ModuleBinding = 2,

    /// <summary>The pinned runtime's own contract descriptor supplied every physical layout offset.</summary>
    ContractDescriptor = 3,

    /// <summary>One loader module's available-type table was swept for same-definition entries.</summary>
    AvailableTypeTable = 4,

    /// <summary>One same-definition entry's ordered closed arguments were projected through the authority.</summary>
    ConstructionProjection = 5,

    /// <summary>The landed candidate-keyed selection ran over the emitted draft candidate rows.</summary>
    ConstructionSelection = 6,

    /// <summary>One exact selected thread was chosen by its token-keyed frame predicate.</summary>
    ThreadSelection = 7,

    /// <summary>One exact static storage slot address was acquired for the selected construction.</summary>
    StaticSlotAddress = 8,

    /// <summary>One module image's FieldRVA row and mapped address geometry were acquired.</summary>
    ModuleGeometry = 9,

    /// <summary>The counted raw read copied exactly the declared width out of the pinned snapshot.</summary>
    CountedRead = 10,

    /// <summary>One frame-value root was attributed to an exact memory location.</summary>
    FrameRoot = 11,
}

/// <summary>Identifies the deterministic issue of one runtime draft acquisition.</summary>
/// <remarks>
/// The catalog stays prefix-free: a capability the frozen vector marks not required, an absent binding, an absent or
/// ambiguous physical fact, a reached bound, and a declared non-implementation are five distinct dispositions.
/// </remarks>
public enum StaticFieldV2RuntimeAcquisitionIssue
{
    /// <summary>No issue applies to an exact draft acquisition.</summary>
    None = 0,

    /// <summary>The frozen capability vector marks this capability as not required for the classified strategy.</summary>
    CapabilityNotRequiredForStrategy = 1,

    /// <summary>A metadata literal lives entirely in metadata, so it owns no runtime draft storage at all.</summary>
    MetadataLiteralHasNoRuntimeStorage = 2,

    /// <summary>No caller-supplied binding names the runtime module this acquisition requires.</summary>
    RuntimeModuleBindingAbsent = 3,

    /// <summary>The pinned runtime's contract descriptor or one derived table could not be read exactly.</summary>
    RuntimeLayoutEvidenceUnavailable = 4,

    /// <summary>The same-definition draft candidate count reached the declared runtime-construction cap plus one.</summary>
    ConstructionCandidateCountBoundReached = 5,

    /// <summary>The landed candidate-keyed selection did not produce one exact construction.</summary>
    ConstructionSelectionNonExact = 6,

    /// <summary>No thread satisfied the exact token-keyed selected-frame predicate.</summary>
    SelectedThreadAbsent = 7,

    /// <summary>Two or more threads satisfied the exact token-keyed selected-frame predicate.</summary>
    SelectedThreadAmbiguous = 8,

    /// <summary>The pinned snapshot exposes no exact static storage slot address for this construction.</summary>
    StaticSlotAddressUnavailable = 9,

    /// <summary>The declaring module's physical FieldRVA table holds no row for the selected FieldDef.</summary>
    ModuleRvaRowAbsent = 10,

    /// <summary>The FieldRVA row plus declared width leaves the mapped module image.</summary>
    ModuleRvaGeometryOutsideImage = 11,

    /// <summary>The counted raw read returned fewer bytes than the declared width.</summary>
    CountedReadIncomplete = 12,

    /// <summary>The landed slot acquisition rejected the acquired physical facts.</summary>
    StaticSlotAcquisitionNonExact = 13,

    /// <summary>A frame-value root homed in a register is not admitted by this draft slice.</summary>
    FrameRegisterHomeNotAdmitted = 15,

    /// <summary>A selected frame's own generic arguments are not admitted by this draft slice.</summary>
    FrameGenericArgumentNotAdmitted = 16,

    /// <summary>The selected thread exposes fewer selector-matching managed frames than the ordinal names.</summary>
    SelectedFrameAbsent = 17,

    /// <summary>The selected frame's native instruction pointer maps to no exact IL offset.</summary>
    FrameInstructionOffsetUnavailable = 18,

    /// <summary>The selected frame declares no argument or local at the requested physical ordinal.</summary>
    FrameRootOrdinalAbsent = 19,

    /// <summary>No supplied Portable-PDB local-scope row declares the requested local name at all.</summary>
    FrameLocalNameAbsent = 20,

    /// <summary>Every supplied row declaring the local name ends before the selected instruction offset.</summary>
    FrameLocalLexicallyInactive = 21,

    /// <summary>Two or more supplied rows declare the same live local name at the selected instruction offset.</summary>
    FrameLocalNameAmbiguous = 22,

    /// <summary>The selected frame-value root reports no location at all at the selected instruction.</summary>
    FrameRootLocationAbsent = 23,

    /// <summary>The selected frame-value root is split across more than one reported location.</summary>
    FrameRootLocationSplit = 24,

    /// <summary>The selected frame-value root reports one location whose flag word is not admitted.</summary>
    FrameRootLocationUnknown = 25,

    /// <summary>The selected frame-value root reports a payload width outside the admitted draft range.</summary>
    FrameRootWidthNotAdmitted = 26,

    /// <summary>The pinned runtime's legacy stack-walk data interface could not be reached exactly.</summary>
    FrameInteropUnavailable = 27,
}

/// <summary>Identifies one declared coverage boundary retained by a runtime draft acquisition answer.</summary>
/// <remarks>
/// Every boundary is an informational draft fact naming what this adapter deliberately does not model, so a consumer
/// can never mistake a silent gap for a proven negative.
/// </remarks>
public enum StaticFieldV2RuntimeAcquisitionBoundary
{
    /// <summary>Every physical layout offset came from the pinned runtime's own contract descriptor.</summary>
    RuntimeLayoutSuppliedByContractDescriptor = 1,

    /// <summary>Candidate arguments whose definition module is unbound were counted and excluded, never fabricated.</summary>
    UnprojectableConstructionArgumentsCountedAndExcluded = 2,

    /// <summary>No corelib-primitive collapse is modeled, so a runtime corelib argument stays unprojectable.</summary>
    CorelibPrimitiveCollapseNotModeled = 3,

    /// <summary>The physical CustomAttribute table is not modeled, so storage markers stay caller-supplied.</summary>
    CustomAttributeTableNotModeled = 4,

    /// <summary>A frame-value root homed in a register is refused rather than read out of that register.</summary>
    FrameRegisterHomeNotAdmitted = 6,

    /// <summary>No selected-frame generic-argument substitution service is modeled or exposed at all.</summary>
    FrameGenericArgumentSubstitutionNotModeled = 7,

    /// <summary>Portable-PDB local scopes are caller-supplied rows rather than a modeled physical table.</summary>
    FrameLocalScopesSuppliedByCaller = 8,

    /// <summary>Frame-value homes come from the pinned runtime's legacy stack-walk data interface.</summary>
    FrameHomeSuppliedByLegacyStackWalkInterface = 9,

    /// <summary>The selected frame's receiver is identified by the legacy interface's own argument name.</summary>
    FrameReceiverIdentifiedByLegacyArgumentName = 10,
}

/// <summary>Names the frame-value draft root families one selected-frame request may name.</summary>
/// <remarks>
/// The first three families are admitted and can reach an exact memory home. The two generic-argument families exist
/// only so a caller can name them and receive the frozen W8.1 non-admission instead of a fabricated substitution.
/// </remarks>
public enum StaticFieldV2FrameValueRootKind
{
    /// <summary>The selected frame's receiver.</summary>
    This = 1,

    /// <summary>One ordered declared parameter of the selected frame.</summary>
    Parameter = 2,

    /// <summary>One named local that is active at the selected instruction offset.</summary>
    Local = 3,

    /// <summary>One closed declaring-type generic argument of the selected frame, which is never admitted.</summary>
    DeclaringTypeGenericArgument = 4,

    /// <summary>One closed method generic argument of the selected frame, which is never admitted.</summary>
    MethodGenericArgument = 5,
}

/// <summary>Freezes the retained physical draft evidence of one opened runtime acquisition snapshot.</summary>
/// <remarks>
/// The evidence is the complete proof that every later physical read used the pinned runtime's own declared layout: the
/// contract-descriptor address, its flags, its counted JSON and pointer-data digests, and the two contract versions the
/// descriptor declares. The snapshot digest is derived from the enumerated module geometry alone, so reopening the same
/// dump reproduces it byte-identically.
/// </remarks>
public sealed class StaticFieldV2RuntimeSnapshotEvidence : IEquatable<StaticFieldV2RuntimeSnapshotEvidence>
{
    private const string CanonicalDomain = "static-field-v2-runtime-snapshot-evidence";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<byte> canonicalBytes;

    private StaticFieldV2RuntimeSnapshotEvidence(
        string snapshotSha256,
        int pointerWidth,
        ulong descriptorAddress,
        uint descriptorFlags,
        uint descriptorSize,
        string descriptorJsonSha256,
        string pointerDataSha256,
        int loaderContractVersion,
        int runtimeTypeSystemContractVersion,
        int enumeratedModuleCount)
    {
        SnapshotSha256 = snapshotSha256;
        PointerWidth = pointerWidth;
        DescriptorAddress = descriptorAddress;
        DescriptorFlags = descriptorFlags;
        DescriptorSize = descriptorSize;
        DescriptorJsonSha256 = descriptorJsonSha256;
        PointerDataSha256 = pointerDataSha256;
        LoaderContractVersion = loaderContractVersion;
        RuntimeTypeSystemContractVersion = runtimeTypeSystemContractVersion;
        EnumeratedModuleCount = enumeratedModuleCount;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteSha256(snapshotSha256, nameof(snapshotSha256));
        writer.WriteInt32(pointerWidth);
        writer.WriteUInt64(descriptorAddress);
        writer.WriteUInt32(descriptorFlags);
        writer.WriteUInt32(descriptorSize);
        writer.WriteSha256(descriptorJsonSha256, nameof(descriptorJsonSha256));
        writer.WriteSha256(pointerDataSha256, nameof(pointerDataSha256));
        writer.WriteInt32(loaderContractVersion);
        writer.WriteInt32(runtimeTypeSystemContractVersion);
        writer.WriteInt32(enumeratedModuleCount);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the deterministic draft snapshot digest derived from enumerated module geometry alone.</summary>
    public string SnapshotSha256 { get; }

    /// <summary>Gets the exact target pointer width in bytes of the pinned draft snapshot.</summary>
    public int PointerWidth { get; }

    /// <summary>Gets the nonzero address of the pinned runtime's exported contract descriptor.</summary>
    public ulong DescriptorAddress { get; }

    /// <summary>Gets the exact contract-descriptor flag word of the pinned draft snapshot.</summary>
    public uint DescriptorFlags { get; }

    /// <summary>Gets the exact declared descriptor JSON byte count.</summary>
    public uint DescriptorSize { get; }

    /// <summary>Gets the lowercase SHA-256 digest of the counted descriptor JSON bytes.</summary>
    public string DescriptorJsonSha256 { get; }

    /// <summary>Gets the lowercase SHA-256 digest of the counted descriptor pointer-data bytes.</summary>
    public string PointerDataSha256 { get; }

    /// <summary>Gets the Loader contract version the pinned runtime declares.</summary>
    public int LoaderContractVersion { get; }

    /// <summary>Gets the RuntimeTypeSystem contract version the pinned runtime declares.</summary>
    public int RuntimeTypeSystemContractVersion { get; }

    /// <summary>Gets the exact enumerated runtime-module count of the pinned draft snapshot.</summary>
    public int EnumeratedModuleCount { get; }

    /// <summary>Gets a defensive copy of the fixed-reference canonical draft evidence bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft evidence.</summary>
    public string Sha256 { get; }

    /// <summary>Tests canonical equality between two runtime snapshot draft evidences.</summary>
    /// <param name="other">The other draft evidence.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical draft content.</returns>
    public bool Equals(StaticFieldV2RuntimeSnapshotEvidence? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests runtime snapshot draft evidence equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for evidence with identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as StaticFieldV2RuntimeSnapshotEvidence);

    /// <summary>Computes a deterministic hash code from immutable canonical draft evidence content.</summary>
    /// <returns>A hash code for this canonical draft evidence.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal static StaticFieldV2RuntimeSnapshotEvidence Create(
        string snapshotSha256,
        int pointerWidth,
        ulong descriptorAddress,
        uint descriptorFlags,
        uint descriptorSize,
        string descriptorJsonSha256,
        string pointerDataSha256,
        int loaderContractVersion,
        int runtimeTypeSystemContractVersion,
        int enumeratedModuleCount) =>
        new(
            snapshotSha256,
            pointerWidth,
            descriptorAddress,
            descriptorFlags,
            descriptorSize,
            descriptorJsonSha256,
            pointerDataSha256,
            loaderContractVersion,
            runtimeTypeSystemContractVersion,
            enumeratedModuleCount);
}

/// <summary>Reports one enumerated runtime module of the pinned draft snapshot.</summary>
/// <remarks>
/// The observation carries only counted physical coordinates. It exists so a caller can bind a runtime module to an
/// on-disk artifact by exact metadata content rather than by any display name.
/// </remarks>
public sealed class StaticFieldV2RuntimeModuleObservation
{
    internal StaticFieldV2RuntimeModuleObservation(
        ulong moduleAddress,
        ulong assemblyAddress,
        ulong applicationDomainAddress,
        ulong imageBase,
        ulong imageSize,
        ulong metadataAddress,
        ulong metadataLength,
        ulong loaderAllocatorAddress)
    {
        ModuleAddress = moduleAddress;
        AssemblyAddress = assemblyAddress;
        ApplicationDomainAddress = applicationDomainAddress;
        ImageBase = imageBase;
        ImageSize = imageSize;
        MetadataAddress = metadataAddress;
        MetadataLength = metadataLength;
        LoaderAllocatorAddress = loaderAllocatorAddress;
    }

    /// <summary>Gets the nonzero runtime module structure address of this draft observation.</summary>
    public ulong ModuleAddress { get; }

    /// <summary>Gets the runtime assembly structure address containing this draft module.</summary>
    public ulong AssemblyAddress { get; }

    /// <summary>Gets the application-domain address that owns this draft module.</summary>
    public ulong ApplicationDomainAddress { get; }

    /// <summary>Gets the mapped image base of this draft module, or zero when it is not mapped.</summary>
    public ulong ImageBase { get; }

    /// <summary>Gets the mapped image size in bytes of this draft module.</summary>
    public ulong ImageSize { get; }

    /// <summary>Gets the address of this draft module's complete ECMA-335 metadata image.</summary>
    public ulong MetadataAddress { get; }

    /// <summary>Gets the byte count of this draft module's complete ECMA-335 metadata image.</summary>
    public ulong MetadataLength { get; }

    /// <summary>Gets this draft module's loader-allocator address, or zero when it is not collectible.</summary>
    public ulong LoaderAllocatorAddress { get; }
}

/// <summary>Freezes one caller-supplied binding of a runtime module to its exact metadata module draft identity.</summary>
/// <remarks>
/// The binding is the only bridge between a physical runtime module address and the landed metadata authority. An
/// unbound runtime module can never contribute a fabricated identity: it makes every candidate that names it
/// unprojectable, and the acquisition counts and excludes that candidate instead.
/// </remarks>
public sealed class StaticFieldV2RuntimeModuleBinding : IEquatable<StaticFieldV2RuntimeModuleBinding>
{
    private const string CanonicalDomain = "static-field-v2-runtime-module-binding";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<byte> canonicalBytes;

    private StaticFieldV2RuntimeModuleBinding(
        ulong runtimeModuleAddress,
        ulong imageBase,
        ulong imageSize,
        StaticFieldMetadataModuleIdentity metadataModule)
    {
        RuntimeModuleAddress = runtimeModuleAddress;
        ImageBase = imageBase;
        ImageSize = imageSize;
        MetadataModule = metadataModule;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteUInt64(runtimeModuleAddress);
        writer.WriteUInt64(imageBase);
        writer.WriteUInt64(imageSize);
        writer.WriteSha256(metadataModule.Sha256, nameof(metadataModule));
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the nonzero runtime module structure address this draft binding names.</summary>
    public ulong RuntimeModuleAddress { get; }

    /// <summary>Gets the mapped image base of the bound draft module, or zero when it is not mapped.</summary>
    public ulong ImageBase { get; }

    /// <summary>Gets the mapped image size in bytes of the bound draft module.</summary>
    public ulong ImageSize { get; }

    /// <summary>Gets the exact metadata module draft identity bound to that runtime module.</summary>
    public StaticFieldMetadataModuleIdentity MetadataModule { get; }

    /// <summary>Gets a defensive copy of the fixed-reference canonical draft binding bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft binding.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one runtime-module to metadata-module draft binding.</summary>
    /// <param name="runtimeModuleAddress">The nonzero runtime module structure address.</param>
    /// <param name="imageBase">The mapped image base, or zero when the module is not mapped.</param>
    /// <param name="imageSize">The mapped image size in bytes.</param>
    /// <param name="metadataModule">The exact metadata module identity to bind.</param>
    /// <returns>A sealed immutable draft binding.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="metadataModule"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The runtime module address is zero.</exception>
    public static StaticFieldV2RuntimeModuleBinding Create(
        ulong runtimeModuleAddress,
        ulong imageBase,
        ulong imageSize,
        StaticFieldMetadataModuleIdentity metadataModule)
    {
        ArgumentNullException.ThrowIfNull(metadataModule);
        CanonicalReplayEncoding.ValidatePointerValue(
            runtimeModuleAddress,
            metadataModule.Module.PointerWidth,
            allowZero: false,
            nameof(runtimeModuleAddress));
        return new StaticFieldV2RuntimeModuleBinding(runtimeModuleAddress, imageBase, imageSize, metadataModule);
    }

    /// <summary>Tests canonical equality between two runtime-module draft bindings.</summary>
    /// <param name="other">The other draft binding.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical draft content.</returns>
    public bool Equals(StaticFieldV2RuntimeModuleBinding? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests runtime-module draft binding equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for a binding with identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as StaticFieldV2RuntimeModuleBinding);

    /// <summary>Computes a deterministic hash code from immutable canonical draft binding content.</summary>
    /// <returns>A hash code for this canonical draft binding.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
}

/// <summary>Freezes one token-keyed predicate selecting exactly one thread of the pinned draft snapshot.</summary>
/// <remarks>
/// The selector names a runtime module address plus the TypeDef and MethodDef tokens of one managed frame. No display
/// name, no ordinal, and no enumeration order takes part, so the same selector chooses the same thread on every replay.
/// </remarks>
public sealed class StaticFieldV2RuntimeThreadSelector : IEquatable<StaticFieldV2RuntimeThreadSelector>
{
    private const string CanonicalDomain = "static-field-v2-runtime-thread-selector";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<byte> canonicalBytes;

    private StaticFieldV2RuntimeThreadSelector(
        ulong runtimeModuleAddress,
        int typeDefinitionToken,
        int methodDefinitionToken)
    {
        RuntimeModuleAddress = runtimeModuleAddress;
        TypeDefinitionToken = typeDefinitionToken;
        MethodDefinitionToken = methodDefinitionToken;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteUInt64(runtimeModuleAddress);
        writer.WriteInt32(typeDefinitionToken);
        writer.WriteInt32(methodDefinitionToken);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the nonzero runtime module address that must declare the selected draft frame's type.</summary>
    public ulong RuntimeModuleAddress { get; }

    /// <summary>Gets the exact TypeDef token that must declare the selected draft frame's method.</summary>
    public int TypeDefinitionToken { get; }

    /// <summary>Gets the exact MethodDef token the selected draft frame must execute.</summary>
    public int MethodDefinitionToken { get; }

    /// <summary>Gets a defensive copy of the fixed-reference canonical draft selector bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft selector.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one token-keyed selected-thread draft predicate.</summary>
    /// <param name="runtimeModuleAddress">The nonzero runtime module address declaring the frame's type.</param>
    /// <param name="typeDefinitionToken">The exact non-nil TypeDef token declaring the frame's method.</param>
    /// <param name="methodDefinitionToken">The exact non-nil MethodDef token the frame executes.</param>
    /// <returns>A sealed immutable draft selector.</returns>
    /// <exception cref="ArgumentOutOfRangeException">An address or token is outside its admitted range.</exception>
    public static StaticFieldV2RuntimeThreadSelector Create(
        ulong runtimeModuleAddress,
        int typeDefinitionToken,
        int methodDefinitionToken)
    {
        if (runtimeModuleAddress == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(runtimeModuleAddress),
                "A nonzero runtime module address is required.");
        }
        CanonicalReplayEncoding.ValidateMetadataToken(typeDefinitionToken, 0x02, nameof(typeDefinitionToken));
        CanonicalReplayEncoding.ValidateMetadataToken(methodDefinitionToken, 0x06, nameof(methodDefinitionToken));
        return new StaticFieldV2RuntimeThreadSelector(
            runtimeModuleAddress,
            typeDefinitionToken,
            methodDefinitionToken);
    }

    /// <summary>Tests canonical equality between two selected-thread draft selectors.</summary>
    /// <param name="other">The other draft selector.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical draft content.</returns>
    public bool Equals(StaticFieldV2RuntimeThreadSelector? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests selected-thread draft selector equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for a selector with identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as StaticFieldV2RuntimeThreadSelector);

    /// <summary>Computes a deterministic hash code from immutable canonical draft selector content.</summary>
    /// <returns>A hash code for this canonical draft selector.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
}

/// <summary>Freezes one complete constructed-runtime candidate draft acquisition request.</summary>
/// <remarks>
/// The request names the bound metadata construction, the classified strategy whose frozen capability vector governs
/// every acquisition, the landed chain and ancestry authorities that project a runtime argument, and the caller-supplied
/// runtime-module bindings. Nothing here names a type: the sweep is keyed by definition module plus TypeDef token only.
/// </remarks>
public sealed class StaticFieldV2RuntimeConstructionAcquisitionRequest :
    IEquatable<StaticFieldV2RuntimeConstructionAcquisitionRequest>
{
    /// <summary>Gets the maximum admitted caller-supplied runtime-module draft binding count.</summary>
    public const int MaximumModuleBindingCount = StaticFieldV2Limits.MaximumModuleCount;

    private const string CanonicalDomain = "static-field-v2-runtime-construction-acquisition-request";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<StaticFieldV2RuntimeModuleBinding> moduleBindings;
    private readonly ImmutableArray<byte> canonicalBytes;

    private StaticFieldV2RuntimeConstructionAcquisitionRequest(
        StaticFieldV2ClosedConstructionOutcome metadataConstruction,
        StaticFieldV2StorageStrategyOutcome storageStrategy,
        MetadataNamedTypeDefinitionChainPortfolioIdentity chainPortfolio,
        MetadataAncestryAuthorityPortfolioIdentity ancestryPortfolio,
        ImmutableArray<StaticFieldV2RuntimeModuleBinding> moduleBindings,
        ExpressionV2CapabilityProbeSet? capabilityProbes)
    {
        MetadataConstruction = metadataConstruction;
        StorageStrategy = storageStrategy;
        ChainPortfolio = chainPortfolio;
        AncestryPortfolio = ancestryPortfolio;
        this.moduleBindings = moduleBindings;
        CapabilityProbes = capabilityProbes;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteSha256(metadataConstruction.Sha256, nameof(metadataConstruction));
        writer.WriteSha256(storageStrategy.Sha256, nameof(storageStrategy));
        writer.WriteSha256(chainPortfolio.Sha256, nameof(chainPortfolio));
        writer.WriteSha256(ancestryPortfolio.Sha256, nameof(ancestryPortfolio));
        ExpressionV2ContractEncoding.WriteCanonicalArray(
            writer,
            moduleBindings,
            static binding => binding.CanonicalBytes);
        writer.WriteBoolean(capabilityProbes is not null);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact bound metadata construction the acquired draft candidates must match.</summary>
    public StaticFieldV2ClosedConstructionOutcome MetadataConstruction { get; }

    /// <summary>Gets the classified draft storage strategy whose frozen capability vector governs acquisition.</summary>
    public StaticFieldV2StorageStrategyOutcome StorageStrategy { get; }

    /// <summary>Gets the landed named-TypeDef chain draft portfolio used to project a named runtime argument.</summary>
    public MetadataNamedTypeDefinitionChainPortfolioIdentity ChainPortfolio { get; }

    /// <summary>Gets the landed ancestry authority draft portfolio supplying every semantic classification.</summary>
    public MetadataAncestryAuthorityPortfolioIdentity AncestryPortfolio { get; }

    /// <summary>Gets a defensive copy of every caller-supplied runtime-module draft binding.</summary>
    public ImmutableArray<StaticFieldV2RuntimeModuleBinding> ModuleBindings =>
        ExpressionV2ContractEncoding.Copy(moduleBindings);

    /// <summary>Gets the caller-owned capability probes this draft acquisition routes every call through.</summary>
    public ExpressionV2CapabilityProbeSet? CapabilityProbes { get; }

    /// <summary>Gets a defensive copy of the bounded fixed-reference canonical draft request bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft request.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one complete constructed-runtime candidate draft acquisition request.</summary>
    /// <param name="metadataConstruction">One exact bound owner construction carrying a named final classification.</param>
    /// <param name="storageStrategy">One exact classified storage strategy carrying its frozen capability vector.</param>
    /// <param name="chainPortfolio">The landed exact named-TypeDef chain portfolio.</param>
    /// <param name="ancestryPortfolio">The landed exact ancestry authority portfolio.</param>
    /// <param name="moduleBindings">Every caller-supplied runtime-module binding, in any order.</param>
    /// <param name="capabilityProbes">Caller-owned probes whose counters become the retained draft ledger.</param>
    /// <returns>A sealed immutable draft request with a defensively copied binding set.</returns>
    /// <exception cref="ArgumentNullException">A required reference argument is null.</exception>
    /// <exception cref="ArgumentException">A supplied outcome or portfolio is not exact.</exception>
    public static StaticFieldV2RuntimeConstructionAcquisitionRequest Create(
        StaticFieldV2ClosedConstructionOutcome metadataConstruction,
        StaticFieldV2StorageStrategyOutcome storageStrategy,
        MetadataNamedTypeDefinitionChainPortfolioIdentity chainPortfolio,
        MetadataAncestryAuthorityPortfolioIdentity ancestryPortfolio,
        ImmutableArray<StaticFieldV2RuntimeModuleBinding> moduleBindings,
        ExpressionV2CapabilityProbeSet? capabilityProbes = null)
    {
        ArgumentNullException.ThrowIfNull(metadataConstruction);
        ArgumentNullException.ThrowIfNull(storageStrategy);
        ArgumentNullException.ThrowIfNull(chainPortfolio);
        ArgumentNullException.ThrowIfNull(ancestryPortfolio);
        if (metadataConstruction.ResultKind != StaticFieldV2ClosedConstructionResultKind.Exact ||
            metadataConstruction.OwnerConstruction is not { } owner ||
            owner.FinalClassification is null)
        {
            throw new ArgumentException(
                "A runtime acquisition requires one exact bound owner construction with a named final classification.",
                nameof(metadataConstruction));
        }
        if (storageStrategy.ResultKind != StaticFieldV2StorageStrategyResultKind.Exact ||
            storageStrategy.Strategy is null)
        {
            throw new ArgumentException(
                "A runtime acquisition requires one exact classified storage strategy.",
                nameof(storageStrategy));
        }
        if (chainPortfolio.ResultKind != MetadataNamedTypeDefinitionChainPortfolioResultKind.Exact)
        {
            throw new ArgumentException(
                "A runtime acquisition requires one exact named-TypeDef chain portfolio.",
                nameof(chainPortfolio));
        }
        if (ancestryPortfolio.ResultKind != MetadataAncestryAuthorityPortfolioResultKind.Exact)
        {
            throw new ArgumentException(
                "A runtime acquisition requires one exact ancestry authority portfolio.",
                nameof(ancestryPortfolio));
        }

        var copied = ExpressionV2ContractEncoding.CopyRequired(
            moduleBindings,
            nameof(moduleBindings),
            MaximumModuleBindingCount);
        return new StaticFieldV2RuntimeConstructionAcquisitionRequest(
            metadataConstruction,
            storageStrategy,
            chainPortfolio,
            ancestryPortfolio,
            copied,
            capabilityProbes);
    }

    /// <summary>Tests canonical equality between two candidate draft acquisition requests.</summary>
    /// <param name="other">The other draft request.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical draft content.</returns>
    public bool Equals(StaticFieldV2RuntimeConstructionAcquisitionRequest? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests candidate draft acquisition request equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for a request with identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as StaticFieldV2RuntimeConstructionAcquisitionRequest);

    /// <summary>Computes a deterministic hash code from immutable canonical draft request content.</summary>
    /// <returns>A hash code for this canonical draft request.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal ImmutableArray<StaticFieldV2RuntimeModuleBinding> ModuleBindingsCore => moduleBindings;
}

/// <summary>Freezes the complete draft outcome of one constructed-runtime candidate acquisition.</summary>
/// <remarks>
/// An exact answer emits every complete same-definition draft candidate row, counts every unprojectable one, and
/// retains the landed candidate-keyed selection that ran over those rows. A candidate whose loader module, definition
/// module, or ordered argument could not be projected through the landed authority is counted and excluded; it is never
/// fabricated and never silently dropped.
/// </remarks>
public sealed class StaticFieldV2RuntimeConstructionAcquisitionOutcome :
    IEquatable<StaticFieldV2RuntimeConstructionAcquisitionOutcome>
{
    /// <summary>Gets the frozen diagnostic code retained by a capability-not-required draft answer.</summary>
    public const string CapabilityNotRequiredCode = "W8_RUNTIME_ACQUISITION_CAPABILITY_NOT_REQUIRED";

    /// <summary>Gets the frozen diagnostic code retained by an absent runtime-module binding draft stop.</summary>
    public const string ModuleBindingAbsentCode = "W8_RUNTIME_ACQUISITION_MODULE_BINDING_ABSENT";

    /// <summary>Gets the frozen diagnostic code retained by an unreadable runtime-layout draft stop.</summary>
    public const string RuntimeLayoutUnavailableCode = "W8_RUNTIME_ACQUISITION_LAYOUT_UNAVAILABLE";

    /// <summary>Gets the frozen diagnostic code retained by a candidate cap-plus-one draft stop.</summary>
    public const string CandidateCountBoundReachedCode = "W8_RUNTIME_ACQUISITION_CANDIDATE_COUNT_BOUND";

    private const string CanonicalDomain = "static-field-v2-runtime-construction-acquisition-outcome";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<StaticFieldV2RuntimeConstructionCandidate> candidates;
    private readonly ImmutableArray<StaticFieldV2RuntimeAcquisitionBoundary> declaredBoundaries;
    private readonly ImmutableArray<byte> canonicalBytes;

    private StaticFieldV2RuntimeConstructionAcquisitionOutcome(
        StaticFieldV2RuntimeAcquisitionResultKind resultKind,
        StaticFieldV2RuntimeAcquisitionStage stage,
        StaticFieldV2RuntimeAcquisitionIssue issue,
        StaticFieldV2RuntimeConstructionAcquisitionRequest request,
        StaticFieldV2RuntimeSnapshotEvidence evidence,
        ImmutableArray<StaticFieldV2RuntimeConstructionCandidate> candidates,
        StaticFieldV2RuntimeConstructionSelection? selection,
        StaticFieldV2CapabilityCallLedger capabilityCallLedger,
        ImmutableArray<StaticFieldV2RuntimeAcquisitionBoundary> declaredBoundaries,
        EvaluationDeterministicBound? reachedBound,
        int examinedEntryCount,
        int sameDefinitionEntryCount,
        int unprojectableCandidateCount,
        string? diagnosticCode)
    {
        ResultKind = resultKind;
        Stage = stage;
        Issue = issue;
        Request = request;
        Evidence = evidence;
        this.candidates = candidates;
        Selection = selection;
        CapabilityCallLedger = capabilityCallLedger;
        this.declaredBoundaries = declaredBoundaries;
        ReachedBound = reachedBound;
        ExaminedEntryCount = examinedEntryCount;
        SameDefinitionEntryCount = sameDefinitionEntryCount;
        UnprojectableCandidateCount = unprojectableCandidateCount;
        DiagnosticCode = diagnosticCode;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteInt32((int)resultKind);
        writer.WriteInt32((int)stage);
        writer.WriteInt32((int)issue);
        writer.WriteSha256(request.Sha256, nameof(request));
        writer.WriteSha256(evidence.Sha256, nameof(evidence));
        ExpressionV2ContractEncoding.WriteCanonicalArray(
            writer,
            candidates,
            static candidate => candidate.CanonicalBytes);
        ExpressionV2ContractEncoding.WriteOptionalDigest(writer, selection?.Sha256);
        writer.WriteSha256(capabilityCallLedger.Sha256, nameof(capabilityCallLedger));
        writer.WriteInt32(declaredBoundaries.Length);
        foreach (var boundary in declaredBoundaries)
        {
            writer.WriteInt32((int)boundary);
        }
        ExpressionV2ContractEncoding.WriteOptionalBound(writer, reachedBound);
        writer.WriteInt32(examinedEntryCount);
        writer.WriteInt32(sameDefinitionEntryCount);
        writer.WriteInt32(unprojectableCandidateCount);
        ExpressionV2ContractEncoding.WriteOptionalString(writer, diagnosticCode);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets whether this draft acquisition is exact, non-exact, invalid, or a typed non-admission.</summary>
    public StaticFieldV2RuntimeAcquisitionResultKind ResultKind { get; }

    /// <summary>Gets the exact draft acquisition step that stopped, or none for an exact answer.</summary>
    public StaticFieldV2RuntimeAcquisitionStage Stage { get; }

    /// <summary>Gets the typed draft acquisition issue, or none for an exact answer.</summary>
    public StaticFieldV2RuntimeAcquisitionIssue Issue { get; }

    /// <summary>Gets the complete draft request that produced this outcome.</summary>
    public StaticFieldV2RuntimeConstructionAcquisitionRequest Request { get; }

    /// <summary>Gets the retained physical draft evidence of the pinned snapshot.</summary>
    public StaticFieldV2RuntimeSnapshotEvidence Evidence { get; }

    /// <summary>Gets a defensive digest-ordered copy of every complete emitted draft candidate row.</summary>
    public ImmutableArray<StaticFieldV2RuntimeConstructionCandidate> Candidates =>
        ExpressionV2ContractEncoding.Copy(candidates);

    /// <summary>Gets the landed candidate-keyed draft selection, or null when acquisition stopped first.</summary>
    public StaticFieldV2RuntimeConstructionSelection? Selection { get; }

    /// <summary>Gets the capability-call draft ledger proving which acquisitions this outcome performed.</summary>
    public StaticFieldV2CapabilityCallLedger CapabilityCallLedger { get; }

    /// <summary>Gets a defensive ascending copy of every declared coverage boundary of this draft answer.</summary>
    public ImmutableArray<StaticFieldV2RuntimeAcquisitionBoundary> DeclaredBoundaries =>
        ExpressionV2ContractEncoding.Copy(declaredBoundaries);

    /// <summary>Gets the declared draft bound reached at cap plus one, otherwise null.</summary>
    public EvaluationDeterministicBound? ReachedBound { get; }

    /// <summary>Gets the complete available-type entry count examined across every loader module.</summary>
    public int ExaminedEntryCount { get; }

    /// <summary>Gets the entry count whose definition module and TypeDef token matched the bound construction.</summary>
    public int SameDefinitionEntryCount { get; }

    /// <summary>Gets the same-definition entry count excluded because a required projection was unavailable.</summary>
    public int UnprojectableCandidateCount { get; }

    /// <summary>Gets the frozen diagnostic code of a typed draft stop, otherwise null.</summary>
    public string? DiagnosticCode { get; }

    /// <summary>Gets a defensive copy of the bounded fixed-reference canonical draft outcome bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft outcome.</summary>
    public string Sha256 { get; }

    /// <summary>Tests canonical equality between two candidate draft acquisition outcomes.</summary>
    /// <param name="other">The other draft outcome.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical draft content.</returns>
    public bool Equals(StaticFieldV2RuntimeConstructionAcquisitionOutcome? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests candidate draft acquisition outcome equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for an outcome with identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as StaticFieldV2RuntimeConstructionAcquisitionOutcome);

    /// <summary>Computes a deterministic hash code from immutable canonical draft outcome content.</summary>
    /// <returns>A hash code for this canonical draft outcome.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal static StaticFieldV2RuntimeConstructionAcquisitionOutcome IssueOutcome(
        StaticFieldV2RuntimeAcquisitionResultKind resultKind,
        StaticFieldV2RuntimeAcquisitionStage stage,
        StaticFieldV2RuntimeAcquisitionIssue issue,
        StaticFieldV2RuntimeConstructionAcquisitionRequest request,
        StaticFieldV2RuntimeSnapshotEvidence evidence,
        ImmutableArray<StaticFieldV2RuntimeConstructionCandidate> candidates,
        StaticFieldV2RuntimeConstructionSelection? selection,
        ImmutableArray<StaticFieldV2RuntimeAcquisitionBoundary> declaredBoundaries,
        EvaluationDeterministicBound? reachedBound,
        int examinedEntryCount,
        int sameDefinitionEntryCount,
        int unprojectableCandidateCount,
        string? diagnosticCode)
    {
        if (diagnosticCode is not null)
        {
            ExpressionV2ContractEncoding.RequireDiagnosticCode(diagnosticCode, nameof(diagnosticCode));
        }
        return new StaticFieldV2RuntimeConstructionAcquisitionOutcome(
            resultKind,
            stage,
            issue,
            request,
            evidence,
            candidates,
            selection,
            StaticFieldV2LiteralValueOutcome.IssueLedger(request.CapabilityProbes),
            declaredBoundaries,
            reachedBound,
            examinedEntryCount,
            sameDefinitionEntryCount,
            unprojectableCandidateCount,
            diagnosticCode);
    }
}

/// <summary>Freezes one complete static-slot draft acquisition request over the pinned snapshot.</summary>
/// <remarks>
/// The request supplies every fact any admitted strategy could need to locate its physical storage: the classified
/// strategy, the exact owner construction selection for a constructed or thread-relative slot, the token-keyed thread
/// predicate for a thread-relative slot, and the declaring runtime module for a module RVA. The acquisition itself
/// consults the frozen capability vector before touching the snapshot at all.
/// </remarks>
public sealed class StaticFieldV2StaticSlotAcquisitionRequest :
    IEquatable<StaticFieldV2StaticSlotAcquisitionRequest>
{
    private const string CanonicalDomain = "static-field-v2-static-slot-acquisition-request";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<byte> canonicalBytes;

    private StaticFieldV2StaticSlotAcquisitionRequest(
        StaticFieldV2StorageStrategyOutcome storageStrategy,
        int readWidth,
        StaticFieldV2RuntimeConstructionSelection? constructionSelection,
        StaticFieldV2RuntimeThreadSelector? threadSelector,
        StaticFieldV2RuntimeModuleBinding? declaringModuleBinding,
        ExpressionV2CapabilityProbeSet? capabilityProbes)
    {
        StorageStrategy = storageStrategy;
        ReadWidth = readWidth;
        ConstructionSelection = constructionSelection;
        ThreadSelector = threadSelector;
        DeclaringModuleBinding = declaringModuleBinding;
        CapabilityProbes = capabilityProbes;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteSha256(storageStrategy.Sha256, nameof(storageStrategy));
        writer.WriteInt32(readWidth);
        ExpressionV2ContractEncoding.WriteOptionalDigest(writer, constructionSelection?.Sha256);
        ExpressionV2ContractEncoding.WriteOptionalDigest(writer, threadSelector?.Sha256);
        ExpressionV2ContractEncoding.WriteOptionalDigest(writer, declaringModuleBinding?.Sha256);
        writer.WriteBoolean(capabilityProbes is not null);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact classified draft storage strategy whose slot is acquired.</summary>
    public StaticFieldV2StorageStrategyOutcome StorageStrategy { get; }

    /// <summary>Gets the exact counted read width in bytes the acquired draft slot must carry.</summary>
    public int ReadWidth { get; }

    /// <summary>Gets the exact owner construction draft selection, or null when the strategy requires none.</summary>
    public StaticFieldV2RuntimeConstructionSelection? ConstructionSelection { get; }

    /// <summary>Gets the token-keyed selected-thread draft predicate, or null for every non thread-relative strategy.</summary>
    public StaticFieldV2RuntimeThreadSelector? ThreadSelector { get; }

    /// <summary>Gets the declaring runtime-module draft binding, or null for every non module-RVA strategy.</summary>
    public StaticFieldV2RuntimeModuleBinding? DeclaringModuleBinding { get; }

    /// <summary>Gets the caller-owned capability probes this draft acquisition routes every call through.</summary>
    public ExpressionV2CapabilityProbeSet? CapabilityProbes { get; }

    /// <summary>Gets the exact physical FieldDef token whose draft storage this acquisition names.</summary>
    public int FieldDefinitionToken => StorageStrategy.FieldDefinitionToken;

    /// <summary>Gets a defensive copy of the fixed-reference canonical draft request bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft request.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one complete static-slot draft acquisition request.</summary>
    /// <param name="storageStrategy">One exact classified storage strategy.</param>
    /// <param name="readWidth">The positive counted read width in bytes of the slot's value.</param>
    /// <param name="constructionSelection">The exact owner construction selection, or null.</param>
    /// <param name="threadSelector">The token-keyed selected-thread predicate, or null.</param>
    /// <param name="declaringModuleBinding">The declaring runtime-module binding for a module RVA, or null.</param>
    /// <param name="capabilityProbes">Caller-owned probes whose counters become the retained draft ledger.</param>
    /// <returns>A sealed immutable draft request.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="storageStrategy"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The read width is outside its admitted range.</exception>
    /// <exception cref="ArgumentException">The strategy is not exact.</exception>
    public static StaticFieldV2StaticSlotAcquisitionRequest Create(
        StaticFieldV2StorageStrategyOutcome storageStrategy,
        int readWidth,
        StaticFieldV2RuntimeConstructionSelection? constructionSelection = null,
        StaticFieldV2RuntimeThreadSelector? threadSelector = null,
        StaticFieldV2RuntimeModuleBinding? declaringModuleBinding = null,
        ExpressionV2CapabilityProbeSet? capabilityProbes = null)
    {
        ArgumentNullException.ThrowIfNull(storageStrategy);
        if (storageStrategy.ResultKind != StaticFieldV2StorageStrategyResultKind.Exact ||
            storageStrategy.Strategy is null)
        {
            throw new ArgumentException(
                "A static-slot acquisition requires one exact classified storage strategy.",
                nameof(storageStrategy));
        }
        if (readWidth is <= 0 or > StaticFieldV2StaticSlotRequest.MaximumReadWidth)
        {
            throw new ArgumentOutOfRangeException(
                nameof(readWidth),
                $"A counted read width of one through {StaticFieldV2StaticSlotRequest.MaximumReadWidth} is required.");
        }
        return new StaticFieldV2StaticSlotAcquisitionRequest(
            storageStrategy,
            readWidth,
            constructionSelection,
            threadSelector,
            declaringModuleBinding,
            capabilityProbes);
    }

    /// <summary>Tests canonical equality between two static-slot draft acquisition requests.</summary>
    /// <param name="other">The other draft request.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical draft content.</returns>
    public bool Equals(StaticFieldV2StaticSlotAcquisitionRequest? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests static-slot draft acquisition request equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for a request with identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as StaticFieldV2StaticSlotAcquisitionRequest);

    /// <summary>Computes a deterministic hash code from immutable canonical draft request content.</summary>
    /// <returns>A hash code for this canonical draft request.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
}

/// <summary>Freezes the complete draft outcome of one static-slot acquisition over the pinned snapshot.</summary>
/// <remarks>
/// An exact answer carries the landed slot outcome plus every physical fact this adapter acquired to produce it. The
/// landed acquisition owns the per-strategy requirement and rejection rules and owns every capability call, so the
/// retained ledger counts each frozen requirement exactly once and counts nothing a strategy marks not required.
/// </remarks>
public sealed class StaticFieldV2StaticSlotAcquisitionOutcome :
    IEquatable<StaticFieldV2StaticSlotAcquisitionOutcome>
{
    /// <summary>Gets the frozen diagnostic code retained by a metadata-literal draft non-admission.</summary>
    public const string MetadataLiteralHasNoRuntimeStorageCode = "W8_RUNTIME_ACQUISITION_LITERAL_HAS_NO_STORAGE";

    /// <summary>Gets the frozen diagnostic code retained by an unavailable slot-address draft stop.</summary>
    public const string SlotAddressUnavailableCode = "W8_RUNTIME_ACQUISITION_SLOT_ADDRESS_UNAVAILABLE";

    /// <summary>Gets the frozen diagnostic code retained by an absent selected-thread draft stop.</summary>
    public const string SelectedThreadAbsentCode = "W8_RUNTIME_ACQUISITION_THREAD_ABSENT";

    /// <summary>Gets the frozen diagnostic code retained by an ambiguous selected-thread draft stop.</summary>
    public const string SelectedThreadAmbiguousCode = "W8_RUNTIME_ACQUISITION_THREAD_AMBIGUOUS";

    /// <summary>Gets the frozen diagnostic code retained by a module-RVA geometry draft stop.</summary>
    public const string ModuleRvaGeometryUnavailableCode = "W8_RUNTIME_ACQUISITION_MODULE_RVA_UNAVAILABLE";

    private const string CanonicalDomain = "static-field-v2-static-slot-acquisition-outcome";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<StaticFieldV2RuntimeAcquisitionBoundary> declaredBoundaries;
    private readonly ImmutableArray<byte> canonicalBytes;

    private StaticFieldV2StaticSlotAcquisitionOutcome(
        StaticFieldV2RuntimeAcquisitionResultKind resultKind,
        StaticFieldV2RuntimeAcquisitionStage stage,
        StaticFieldV2RuntimeAcquisitionIssue issue,
        StaticFieldV2StaticSlotAcquisitionRequest request,
        StaticFieldV2RuntimeSnapshotEvidence evidence,
        StaticFieldV2StaticSlotOutcome? slotOutcome,
        StaticFieldV2SelectedThreadIdentity? selectedThread,
        ulong? slotAddress,
        int? fieldRvaRowToken,
        uint? mappedRelativeVirtualAddress,
        ulong? mappedAddress,
        StaticFieldV2CapabilityCallLedger capabilityCallLedger,
        ImmutableArray<StaticFieldV2RuntimeAcquisitionBoundary> declaredBoundaries,
        int candidateThreadCount,
        string? diagnosticCode)
    {
        ResultKind = resultKind;
        Stage = stage;
        Issue = issue;
        Request = request;
        Evidence = evidence;
        SlotOutcome = slotOutcome;
        SelectedThread = selectedThread;
        SlotAddress = slotAddress;
        FieldRvaRowToken = fieldRvaRowToken;
        MappedRelativeVirtualAddress = mappedRelativeVirtualAddress;
        MappedAddress = mappedAddress;
        CapabilityCallLedger = capabilityCallLedger;
        this.declaredBoundaries = declaredBoundaries;
        CandidateThreadCount = candidateThreadCount;
        DiagnosticCode = diagnosticCode;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteInt32((int)resultKind);
        writer.WriteInt32((int)stage);
        writer.WriteInt32((int)issue);
        writer.WriteSha256(request.Sha256, nameof(request));
        writer.WriteSha256(evidence.Sha256, nameof(evidence));
        ExpressionV2ContractEncoding.WriteOptionalDigest(writer, slotOutcome?.Sha256);
        ExpressionV2ContractEncoding.WriteOptionalDigest(writer, selectedThread?.Sha256);
        ExpressionV2ContractEncoding.WriteOptionalUInt64(writer, slotAddress);
        ExpressionV2ContractEncoding.WriteOptionalInt32(writer, fieldRvaRowToken);
        ExpressionV2ContractEncoding.WriteOptionalUInt64(
            writer,
            mappedRelativeVirtualAddress.HasValue ? mappedRelativeVirtualAddress.Value : null);
        ExpressionV2ContractEncoding.WriteOptionalUInt64(writer, mappedAddress);
        writer.WriteSha256(capabilityCallLedger.Sha256, nameof(capabilityCallLedger));
        writer.WriteInt32(declaredBoundaries.Length);
        foreach (var boundary in declaredBoundaries)
        {
            writer.WriteInt32((int)boundary);
        }
        writer.WriteInt32(candidateThreadCount);
        ExpressionV2ContractEncoding.WriteOptionalString(writer, diagnosticCode);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets whether this draft acquisition is exact, non-exact, invalid, or a typed non-admission.</summary>
    public StaticFieldV2RuntimeAcquisitionResultKind ResultKind { get; }

    /// <summary>Gets the exact draft acquisition step that stopped, or none for an exact answer.</summary>
    public StaticFieldV2RuntimeAcquisitionStage Stage { get; }

    /// <summary>Gets the typed draft acquisition issue, or none for an exact answer.</summary>
    public StaticFieldV2RuntimeAcquisitionIssue Issue { get; }

    /// <summary>Gets the complete draft request that produced this outcome.</summary>
    public StaticFieldV2StaticSlotAcquisitionRequest Request { get; }

    /// <summary>Gets the retained physical draft evidence of the pinned snapshot.</summary>
    public StaticFieldV2RuntimeSnapshotEvidence Evidence { get; }

    /// <summary>Gets the landed static-slot draft outcome, or null when acquisition stopped first.</summary>
    public StaticFieldV2StaticSlotOutcome? SlotOutcome { get; }

    /// <summary>Gets the exact acquired selected-thread draft identity, otherwise null.</summary>
    public StaticFieldV2SelectedThreadIdentity? SelectedThread { get; }

    /// <summary>Gets the exact acquired static slot address, otherwise null.</summary>
    public ulong? SlotAddress { get; }

    /// <summary>Gets the exact acquired FieldRVA row token, otherwise null.</summary>
    public int? FieldRvaRowToken { get; }

    /// <summary>Gets the exact acquired mapped relative virtual address, otherwise null.</summary>
    public uint? MappedRelativeVirtualAddress { get; }

    /// <summary>Gets the exact acquired mapped image address, otherwise null.</summary>
    public ulong? MappedAddress { get; }

    /// <summary>Gets the capability-call draft ledger proving which acquisitions this outcome performed.</summary>
    public StaticFieldV2CapabilityCallLedger CapabilityCallLedger { get; }

    /// <summary>Gets a defensive ascending copy of every declared coverage boundary of this draft answer.</summary>
    public ImmutableArray<StaticFieldV2RuntimeAcquisitionBoundary> DeclaredBoundaries =>
        ExpressionV2ContractEncoding.Copy(declaredBoundaries);

    /// <summary>Gets the thread count that satisfied the token-keyed selected-frame draft predicate.</summary>
    public int CandidateThreadCount { get; }

    /// <summary>Gets the frozen diagnostic code of a typed draft stop, otherwise null.</summary>
    public string? DiagnosticCode { get; }

    /// <summary>Gets a defensive copy of the fixed-reference canonical draft outcome bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft outcome.</summary>
    public string Sha256 { get; }

    /// <summary>Tests canonical equality between two static-slot draft acquisition outcomes.</summary>
    /// <param name="other">The other draft outcome.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical draft content.</returns>
    public bool Equals(StaticFieldV2StaticSlotAcquisitionOutcome? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests static-slot draft acquisition outcome equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for an outcome with identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as StaticFieldV2StaticSlotAcquisitionOutcome);

    /// <summary>Computes a deterministic hash code from immutable canonical draft outcome content.</summary>
    /// <returns>A hash code for this canonical draft outcome.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal static StaticFieldV2StaticSlotAcquisitionOutcome IssueOutcome(
        StaticFieldV2RuntimeAcquisitionResultKind resultKind,
        StaticFieldV2RuntimeAcquisitionStage stage,
        StaticFieldV2RuntimeAcquisitionIssue issue,
        StaticFieldV2StaticSlotAcquisitionRequest request,
        StaticFieldV2RuntimeSnapshotEvidence evidence,
        StaticFieldV2StaticSlotOutcome? slotOutcome,
        StaticFieldV2SelectedThreadIdentity? selectedThread,
        ulong? slotAddress,
        int? fieldRvaRowToken,
        uint? mappedRelativeVirtualAddress,
        ulong? mappedAddress,
        ImmutableArray<StaticFieldV2RuntimeAcquisitionBoundary> declaredBoundaries,
        int candidateThreadCount,
        string? diagnosticCode)
    {
        if (diagnosticCode is not null)
        {
            ExpressionV2ContractEncoding.RequireDiagnosticCode(diagnosticCode, nameof(diagnosticCode));
        }
        return new StaticFieldV2StaticSlotAcquisitionOutcome(
            resultKind,
            stage,
            issue,
            request,
            evidence,
            slotOutcome,
            selectedThread,
            slotAddress,
            fieldRvaRowToken,
            mappedRelativeVirtualAddress,
            mappedAddress,
            StaticFieldV2LiteralValueOutcome.IssueLedger(request.CapabilityProbes),
            declaredBoundaries,
            candidateThreadCount,
            diagnosticCode);
    }
}

/// <summary>Freezes one complete counted raw draft read and value decoding request.</summary>
/// <remarks>
/// The request names the acquired slot, the field's exact closed type, and the two facts the decoder never derives.
/// Exactly the declared width is copied out of the pinned snapshot; no wider read, no rounding, and no zero fill.
/// </remarks>
public sealed class StaticFieldV2RuntimeValueAcquisitionRequest :
    IEquatable<StaticFieldV2RuntimeValueAcquisitionRequest>
{
    private const string CanonicalDomain = "static-field-v2-runtime-value-acquisition-request";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<byte> canonicalBytes;

    private StaticFieldV2RuntimeValueAcquisitionRequest(
        StaticFieldV2StorageStrategyOutcome storageStrategy,
        StaticFieldV2StaticSlotOutcome slot,
        MetadataClosedTypeIdentity declaredType,
        MetadataPrimitiveTypeKind? enumUnderlyingKind,
        StaticFieldV2NullableLayoutFact? nullableLayout,
        ExpressionV2CapabilityProbeSet? capabilityProbes)
    {
        StorageStrategy = storageStrategy;
        Slot = slot;
        DeclaredType = declaredType;
        EnumUnderlyingKind = enumUnderlyingKind;
        NullableLayout = nullableLayout;
        CapabilityProbes = capabilityProbes;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteSha256(storageStrategy.Sha256, nameof(storageStrategy));
        writer.WriteSha256(slot.Sha256, nameof(slot));
        writer.WriteSha256(declaredType.Sha256, nameof(declaredType));
        ExpressionV2ContractEncoding.WriteOptionalEnum(writer, enumUnderlyingKind);
        ExpressionV2ContractEncoding.WriteOptionalDigest(writer, nullableLayout?.Sha256);
        writer.WriteBoolean(capabilityProbes is not null);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact classified draft storage strategy whose frozen capability vector governs the read.</summary>
    public StaticFieldV2StorageStrategyOutcome StorageStrategy { get; }

    /// <summary>Gets the exact acquired static-slot draft outcome naming the address and declared width.</summary>
    public StaticFieldV2StaticSlotOutcome Slot { get; }

    /// <summary>Gets the exact closed draft type of the static field whose value is decoded.</summary>
    public MetadataClosedTypeIdentity DeclaredType { get; }

    /// <summary>Gets the exact enum underlying primitive draft fact, or null when none was supplied.</summary>
    public MetadataPrimitiveTypeKind? EnumUnderlyingKind { get; }

    /// <summary>Gets the exact nullable physical layout draft fact, or null when none was supplied.</summary>
    public StaticFieldV2NullableLayoutFact? NullableLayout { get; }

    /// <summary>Gets the caller-owned capability probes this draft acquisition routes every call through.</summary>
    public ExpressionV2CapabilityProbeSet? CapabilityProbes { get; }

    /// <summary>Gets a defensive copy of the fixed-reference canonical draft request bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft request.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one complete counted raw draft read and value decoding request.</summary>
    /// <param name="storageStrategy">One exact classified storage strategy.</param>
    /// <param name="slot">One exact acquired static-slot outcome.</param>
    /// <param name="declaredType">The exact closed type of the static field.</param>
    /// <param name="enumUnderlyingKind">The exact enum underlying primitive fact, or null when inapplicable.</param>
    /// <param name="nullableLayout">The exact nullable physical layout fact, or null when inapplicable.</param>
    /// <param name="capabilityProbes">Caller-owned probes whose counters become the retained draft ledger.</param>
    /// <returns>A sealed immutable draft request.</returns>
    /// <exception cref="ArgumentNullException">A required reference argument is null.</exception>
    /// <exception cref="ArgumentException">The strategy or the slot outcome is not exact.</exception>
    public static StaticFieldV2RuntimeValueAcquisitionRequest Create(
        StaticFieldV2StorageStrategyOutcome storageStrategy,
        StaticFieldV2StaticSlotOutcome slot,
        MetadataClosedTypeIdentity declaredType,
        MetadataPrimitiveTypeKind? enumUnderlyingKind = null,
        StaticFieldV2NullableLayoutFact? nullableLayout = null,
        ExpressionV2CapabilityProbeSet? capabilityProbes = null)
    {
        ArgumentNullException.ThrowIfNull(storageStrategy);
        ArgumentNullException.ThrowIfNull(slot);
        ArgumentNullException.ThrowIfNull(declaredType);
        if (storageStrategy.ResultKind != StaticFieldV2StorageStrategyResultKind.Exact ||
            storageStrategy.Strategy is null)
        {
            throw new ArgumentException(
                "A counted raw read requires one exact classified storage strategy.",
                nameof(storageStrategy));
        }
        if (slot.ResultKind != StaticFieldV2StaticSlotResultKind.Exact || slot.Slot is null)
        {
            throw new ArgumentException(
                "A counted raw read requires one exact acquired static slot.",
                nameof(slot));
        }
        if (enumUnderlyingKind is { } kind)
        {
            ExpressionV2ContractEncoding.RequireDefined(kind, nameof(enumUnderlyingKind));
        }
        return new StaticFieldV2RuntimeValueAcquisitionRequest(
            storageStrategy,
            slot,
            declaredType,
            enumUnderlyingKind,
            nullableLayout,
            capabilityProbes);
    }

    /// <summary>Tests canonical equality between two counted raw draft read requests.</summary>
    /// <param name="other">The other draft request.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical draft content.</returns>
    public bool Equals(StaticFieldV2RuntimeValueAcquisitionRequest? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests counted raw draft read request equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for a request with identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as StaticFieldV2RuntimeValueAcquisitionRequest);

    /// <summary>Computes a deterministic hash code from immutable canonical draft request content.</summary>
    /// <returns>A hash code for this canonical draft request.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
}

/// <summary>Freezes the complete draft outcome of one counted raw read and value decoding.</summary>
/// <remarks>
/// An exact answer retains the copied bytes, their digest, and the landed pure decoding over exactly those bytes. A
/// short read is a typed stop that exposes no partially observed value and never pads the missing suffix.
/// </remarks>
public sealed class StaticFieldV2RuntimeValueAcquisitionOutcome :
    IEquatable<StaticFieldV2RuntimeValueAcquisitionOutcome>
{
    /// <summary>Gets the frozen diagnostic code retained by a capability-not-required draft answer.</summary>
    public const string MemoryReadNotRequiredCode = "W8_RUNTIME_ACQUISITION_MEMORY_READ_NOT_REQUIRED";

    /// <summary>Gets the frozen diagnostic code retained by an incomplete counted raw draft read.</summary>
    public const string CountedReadIncompleteCode = "W8_RUNTIME_ACQUISITION_COUNTED_READ_INCOMPLETE";

    private const string CanonicalDomain = "static-field-v2-runtime-value-acquisition-outcome";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<byte> rawBytes;
    private readonly ImmutableArray<StaticFieldV2RuntimeAcquisitionBoundary> declaredBoundaries;
    private readonly ImmutableArray<byte> canonicalBytes;

    private StaticFieldV2RuntimeValueAcquisitionOutcome(
        StaticFieldV2RuntimeAcquisitionResultKind resultKind,
        StaticFieldV2RuntimeAcquisitionStage stage,
        StaticFieldV2RuntimeAcquisitionIssue issue,
        StaticFieldV2RuntimeValueAcquisitionRequest request,
        StaticFieldV2RuntimeSnapshotEvidence evidence,
        ImmutableArray<byte> rawBytes,
        StaticFieldV2RuntimeValueOutcome? valueOutcome,
        StaticFieldV2CapabilityCallLedger capabilityCallLedger,
        ImmutableArray<StaticFieldV2RuntimeAcquisitionBoundary> declaredBoundaries,
        int declaredWidth,
        int observedByteCount,
        string? diagnosticCode)
    {
        ResultKind = resultKind;
        Stage = stage;
        Issue = issue;
        Request = request;
        Evidence = evidence;
        this.rawBytes = rawBytes;
        ValueOutcome = valueOutcome;
        CapabilityCallLedger = capabilityCallLedger;
        this.declaredBoundaries = declaredBoundaries;
        DeclaredWidth = declaredWidth;
        ObservedByteCount = observedByteCount;
        DiagnosticCode = diagnosticCode;
        RawBytesSha256 = CanonicalReplayEncoding.ComputeSha256(rawBytes.AsSpan());

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteInt32((int)resultKind);
        writer.WriteInt32((int)stage);
        writer.WriteInt32((int)issue);
        writer.WriteSha256(request.Sha256, nameof(request));
        writer.WriteSha256(evidence.Sha256, nameof(evidence));
        writer.WriteLengthPrefixedBytes(rawBytes.AsSpan());
        ExpressionV2ContractEncoding.WriteOptionalDigest(writer, valueOutcome?.Sha256);
        writer.WriteSha256(capabilityCallLedger.Sha256, nameof(capabilityCallLedger));
        writer.WriteInt32(declaredBoundaries.Length);
        foreach (var boundary in declaredBoundaries)
        {
            writer.WriteInt32((int)boundary);
        }
        writer.WriteInt32(declaredWidth);
        writer.WriteInt32(observedByteCount);
        ExpressionV2ContractEncoding.WriteOptionalString(writer, diagnosticCode);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets whether this draft acquisition is exact, non-exact, invalid, or a typed non-admission.</summary>
    public StaticFieldV2RuntimeAcquisitionResultKind ResultKind { get; }

    /// <summary>Gets the exact draft acquisition step that stopped, or none for an exact answer.</summary>
    public StaticFieldV2RuntimeAcquisitionStage Stage { get; }

    /// <summary>Gets the typed draft acquisition issue, or none for an exact answer.</summary>
    public StaticFieldV2RuntimeAcquisitionIssue Issue { get; }

    /// <summary>Gets the complete draft request that produced this outcome.</summary>
    public StaticFieldV2RuntimeValueAcquisitionRequest Request { get; }

    /// <summary>Gets the retained physical draft evidence of the pinned snapshot.</summary>
    public StaticFieldV2RuntimeSnapshotEvidence Evidence { get; }

    /// <summary>Gets a defensive copy of the bytes copied out of the pinned snapshot.</summary>
    public ImmutableArray<byte> RawBytes => ExpressionV2ContractEncoding.Copy(rawBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the copied raw draft bytes.</summary>
    public string RawBytesSha256 { get; }

    /// <summary>Gets the landed pure draft decoding over the copied bytes, or null when the read stopped.</summary>
    public StaticFieldV2RuntimeValueOutcome? ValueOutcome { get; }

    /// <summary>Gets the capability-call draft ledger proving which acquisitions this outcome performed.</summary>
    public StaticFieldV2CapabilityCallLedger CapabilityCallLedger { get; }

    /// <summary>Gets a defensive ascending copy of every declared coverage boundary of this draft answer.</summary>
    public ImmutableArray<StaticFieldV2RuntimeAcquisitionBoundary> DeclaredBoundaries =>
        ExpressionV2ContractEncoding.Copy(declaredBoundaries);

    /// <summary>Gets the declared draft width in bytes the acquired slot required.</summary>
    public int DeclaredWidth { get; }

    /// <summary>Gets the byte count the pinned snapshot actually supplied for the counted draft read.</summary>
    public int ObservedByteCount { get; }

    /// <summary>Gets the frozen diagnostic code of a typed draft stop, otherwise null.</summary>
    public string? DiagnosticCode { get; }

    /// <summary>Gets a defensive copy of the bounded fixed-reference canonical draft outcome bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft outcome.</summary>
    public string Sha256 { get; }

    /// <summary>Tests canonical equality between two counted raw draft read outcomes.</summary>
    /// <param name="other">The other draft outcome.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical draft content.</returns>
    public bool Equals(StaticFieldV2RuntimeValueAcquisitionOutcome? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests counted raw draft read outcome equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for an outcome with identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as StaticFieldV2RuntimeValueAcquisitionOutcome);

    /// <summary>Computes a deterministic hash code from immutable canonical draft outcome content.</summary>
    /// <returns>A hash code for this canonical draft outcome.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal static StaticFieldV2RuntimeValueAcquisitionOutcome IssueOutcome(
        StaticFieldV2RuntimeAcquisitionResultKind resultKind,
        StaticFieldV2RuntimeAcquisitionStage stage,
        StaticFieldV2RuntimeAcquisitionIssue issue,
        StaticFieldV2RuntimeValueAcquisitionRequest request,
        StaticFieldV2RuntimeSnapshotEvidence evidence,
        ImmutableArray<byte> rawBytes,
        StaticFieldV2RuntimeValueOutcome? valueOutcome,
        ImmutableArray<StaticFieldV2RuntimeAcquisitionBoundary> declaredBoundaries,
        int declaredWidth,
        int observedByteCount,
        string? diagnosticCode)
    {
        if (diagnosticCode is not null)
        {
            ExpressionV2ContractEncoding.RequireDiagnosticCode(diagnosticCode, nameof(diagnosticCode));
        }
        return new StaticFieldV2RuntimeValueAcquisitionOutcome(
            resultKind,
            stage,
            issue,
            request,
            evidence,
            rawBytes,
            valueOutcome,
            StaticFieldV2LiteralValueOutcome.IssueLedger(request.CapabilityProbes),
            declaredBoundaries,
            declaredWidth,
            observedByteCount,
            diagnosticCode);
    }
}

/// <summary>Freezes one caller-supplied Portable-PDB local-scope draft row of the selected frame's method.</summary>
/// <remarks>
/// The row is the exact Portable-PDB fact a caller reads out of the method's <c>LocalScope</c> and <c>LocalVariable</c>
/// tables: one declared local name, the zero-based local-signature slot it occupies, and the half-open IL range its
/// declaring scope covers. The physical Portable-PDB image is not modeled by this draft slice, so these rows are the
/// only lexical evidence the frame acquisition owns and it never invents one.
/// </remarks>
public sealed class StaticFieldV2FramePortablePdbLocalRow : IEquatable<StaticFieldV2FramePortablePdbLocalRow>
{
    /// <summary>Gets the maximum admitted declared local-name character count of one draft row.</summary>
    public const int MaximumNameLength = 256;

    /// <summary>Gets the maximum admitted zero-based local-signature slot draft index of one row.</summary>
    public const int MaximumSlotIndex = ExpressionV2ContractLimits.MaximumLocalCount - 1;

    private const string CanonicalDomain = "static-field-v2-frame-portable-pdb-local-row";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<byte> canonicalBytes;

    private StaticFieldV2FramePortablePdbLocalRow(
        string name,
        int slotIndex,
        int scopeStartOffset,
        int scopeEndOffset)
    {
        Name = name;
        SlotIndex = slotIndex;
        ScopeStartOffset = scopeStartOffset;
        ScopeEndOffset = scopeEndOffset;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteString(name);
        writer.WriteInt32(slotIndex);
        writer.WriteInt32(scopeStartOffset);
        writer.WriteInt32(scopeEndOffset);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the exact declared local draft name this row carries.</summary>
    public string Name { get; }

    /// <summary>Gets the zero-based local-signature slot draft index this row names.</summary>
    public int SlotIndex { get; }

    /// <summary>Gets the inclusive IL offset at which this row's declaring draft scope begins.</summary>
    public int ScopeStartOffset { get; }

    /// <summary>Gets the exclusive IL offset at which this row's declaring draft scope ends.</summary>
    public int ScopeEndOffset { get; }

    /// <summary>Gets a defensive copy of the fixed-reference canonical draft row bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft row.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one caller-supplied Portable-PDB local-scope draft row.</summary>
    /// <param name="name">The exact declared local name of the row.</param>
    /// <param name="slotIndex">The zero-based local-signature slot index the name occupies.</param>
    /// <param name="scopeStartOffset">The inclusive IL offset at which the declaring scope begins.</param>
    /// <param name="scopeEndOffset">The exclusive IL offset at which the declaring scope ends.</param>
    /// <returns>A sealed immutable draft row.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is null.</exception>
    /// <exception cref="ArgumentException">The name is empty, too long, or carries a NUL.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An index or offset is outside its admitted range.</exception>
    public static StaticFieldV2FramePortablePdbLocalRow Create(
        string name,
        int slotIndex,
        int scopeStartOffset,
        int scopeEndOffset)
    {
        ExpressionV2ContractEncoding.RequireText(name, nameof(name), MaximumNameLength);
        if (slotIndex is < 0 or > MaximumSlotIndex)
        {
            throw new ArgumentOutOfRangeException(
                nameof(slotIndex),
                $"A zero-based local slot index of zero through {MaximumSlotIndex} is required.");
        }
        ArgumentOutOfRangeException.ThrowIfNegative(scopeStartOffset);
        if (scopeEndOffset <= scopeStartOffset)
        {
            throw new ArgumentOutOfRangeException(
                nameof(scopeEndOffset),
                "A local scope must end strictly after it begins.");
        }
        return new StaticFieldV2FramePortablePdbLocalRow(name, slotIndex, scopeStartOffset, scopeEndOffset);
    }

    /// <summary>Tests canonical equality between two Portable-PDB local-scope draft rows.</summary>
    /// <param name="other">The other draft row.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical draft content.</returns>
    public bool Equals(StaticFieldV2FramePortablePdbLocalRow? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests Portable-PDB local-scope draft row equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for a row with identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as StaticFieldV2FramePortablePdbLocalRow);

    /// <summary>Computes a deterministic hash code from immutable canonical draft row content.</summary>
    /// <returns>A hash code for this canonical draft row.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
}

/// <summary>Freezes one complete frame-value root draft acquisition request.</summary>
/// <remarks>
/// The request names the token-keyed selected thread, the zero-based ordinal of one selector-matching managed frame on
/// that thread, and one root family. A local additionally names its declared local name plus the caller-supplied
/// Portable-PDB local-scope rows of the frame's method; the name is a Portable-PDB local-scope fact and never a type
/// name, a global lookup, or an enumeration order.
/// </remarks>
public sealed class StaticFieldV2FrameValueRootRequest : IEquatable<StaticFieldV2FrameValueRootRequest>
{
    /// <summary>Gets the maximum admitted zero-based managed frame draft ordinal.</summary>
    public const int MaximumFrameOrdinal = ExpressionV2ContractLimits.MaximumFrameCountPerThread - 1;

    /// <summary>Gets the maximum admitted caller-supplied Portable-PDB local-scope draft row count.</summary>
    public const int MaximumLocalScopeRowCount = ExpressionV2ContractLimits.MaximumLocalCount;

    private const string CanonicalDomain = "static-field-v2-frame-value-root-request";
    private const int CanonicalSchemaVersion = 2;
    private readonly ImmutableArray<StaticFieldV2FramePortablePdbLocalRow> localScopeRows;
    private readonly ImmutableArray<byte> canonicalBytes;

    private StaticFieldV2FrameValueRootRequest(
        StaticFieldV2RuntimeThreadSelector threadSelector,
        int frameOrdinal,
        StaticFieldV2FrameValueRootKind rootKind,
        int rootOrdinal,
        string? localName,
        ImmutableArray<StaticFieldV2FramePortablePdbLocalRow> localScopeRows,
        ExpressionV2CapabilityProbeSet? capabilityProbes)
    {
        ThreadSelector = threadSelector;
        FrameOrdinal = frameOrdinal;
        RootKind = rootKind;
        RootOrdinal = rootOrdinal;
        LocalName = localName;
        this.localScopeRows = localScopeRows;
        CapabilityProbes = capabilityProbes;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteSha256(threadSelector.Sha256, nameof(threadSelector));
        writer.WriteInt32(frameOrdinal);
        writer.WriteInt32((int)rootKind);
        writer.WriteInt32(rootOrdinal);
        ExpressionV2ContractEncoding.WriteOptionalString(writer, localName);
        ExpressionV2ContractEncoding.WriteCanonicalArray(
            writer,
            localScopeRows,
            static row => row.CanonicalBytes);
        writer.WriteBoolean(capabilityProbes is not null);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the token-keyed selected-thread draft predicate of this frame-root request.</summary>
    public StaticFieldV2RuntimeThreadSelector ThreadSelector { get; }

    /// <summary>Gets the zero-based managed frame draft ordinal within the selected thread.</summary>
    public int FrameOrdinal { get; }

    /// <summary>Gets the admitted frame-value draft root family this request names.</summary>
    public StaticFieldV2FrameValueRootKind RootKind { get; }

    /// <summary>Gets the zero-based declared parameter or local draft ordinal, or zero for the receiver.</summary>
    public int RootOrdinal { get; }

    /// <summary>Gets the declared local draft name for a local root, otherwise null.</summary>
    public string? LocalName { get; }

    /// <summary>Gets a defensive copy of every caller-supplied Portable-PDB local-scope draft row.</summary>
    public ImmutableArray<StaticFieldV2FramePortablePdbLocalRow> LocalScopeRows =>
        ExpressionV2ContractEncoding.Copy(localScopeRows);

    /// <summary>Gets the caller-owned capability probes this draft acquisition routes every call through.</summary>
    public ExpressionV2CapabilityProbeSet? CapabilityProbes { get; }

    /// <summary>Gets a defensive copy of the fixed-reference canonical draft request bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft request.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one complete frame-value root draft acquisition request.</summary>
    /// <param name="threadSelector">The token-keyed selected-thread predicate.</param>
    /// <param name="frameOrdinal">The zero-based ordinal of one selector-matching managed frame.</param>
    /// <param name="rootKind">The frame-value root family.</param>
    /// <param name="rootOrdinal">The zero-based declared parameter ordinal, or zero for every other family.</param>
    /// <param name="localName">The declared local name for a local root, otherwise null.</param>
    /// <param name="localScopeRows">The Portable-PDB local-scope rows of the frame's method for a local root.</param>
    /// <param name="capabilityProbes">Caller-owned probes whose counters become the retained draft ledger.</param>
    /// <returns>A sealed immutable draft request with a defensively copied row set.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="threadSelector"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An ordinal is outside its admitted range.</exception>
    /// <exception cref="ArgumentException">The root family and the supplied local facts disagree.</exception>
    public static StaticFieldV2FrameValueRootRequest Create(
        StaticFieldV2RuntimeThreadSelector threadSelector,
        int frameOrdinal,
        StaticFieldV2FrameValueRootKind rootKind,
        int rootOrdinal = 0,
        string? localName = null,
        ImmutableArray<StaticFieldV2FramePortablePdbLocalRow> localScopeRows = default,
        ExpressionV2CapabilityProbeSet? capabilityProbes = null)
    {
        ArgumentNullException.ThrowIfNull(threadSelector);
        ExpressionV2ContractEncoding.RequireDefined(rootKind, nameof(rootKind));
        if (frameOrdinal is < 0 or > MaximumFrameOrdinal)
        {
            throw new ArgumentOutOfRangeException(
                nameof(frameOrdinal),
                $"A zero-based frame ordinal of zero through {MaximumFrameOrdinal} is required.");
        }
        ArgumentOutOfRangeException.ThrowIfNegative(rootOrdinal);
        var rows = ExpressionV2ContractEncoding.CopyRequired(
            localScopeRows.IsDefault
                ? ImmutableArray<StaticFieldV2FramePortablePdbLocalRow>.Empty
                : localScopeRows,
            nameof(localScopeRows),
            MaximumLocalScopeRowCount);
        if (rootKind == StaticFieldV2FrameValueRootKind.Local)
        {
            ExpressionV2ContractEncoding.RequireIdentifier(localName!, nameof(localName));
        }
        else
        {
            if (localName is not null)
            {
                throw new ArgumentException(
                    "Only a local frame-value root may carry a declared local name.",
                    nameof(localName));
            }
            if (rows.Length != 0)
            {
                throw new ArgumentException(
                    "Only a local frame-value root may carry Portable-PDB local-scope rows.",
                    nameof(localScopeRows));
            }
        }
        if (rootKind != StaticFieldV2FrameValueRootKind.Parameter && rootOrdinal != 0)
        {
            throw new ArgumentException(
                "Only a declared-parameter frame-value root carries an ordinal.",
                nameof(rootOrdinal));
        }
        return new StaticFieldV2FrameValueRootRequest(
            threadSelector,
            frameOrdinal,
            rootKind,
            rootOrdinal,
            localName,
            rows,
            capabilityProbes);
    }

    internal ImmutableArray<StaticFieldV2FramePortablePdbLocalRow> LocalScopeRowsCore => localScopeRows;

    /// <summary>Tests canonical equality between two frame-value root draft requests.</summary>
    /// <param name="other">The other draft request.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical draft content.</returns>
    public bool Equals(StaticFieldV2FrameValueRootRequest? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests frame-value root draft request equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for a request with identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as StaticFieldV2FrameValueRootRequest);

    /// <summary>Computes a deterministic hash code from immutable canonical draft request content.</summary>
    /// <returns>A hash code for this canonical draft request.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
}

/// <summary>Freezes the complete draft outcome of one frame-value root acquisition.</summary>
/// <remarks>
/// An exact answer attributes the named root to one exact memory home: its address, its counted payload width, its
/// liveness at the selected instruction offset, and exactly the bytes copied out of the pinned snapshot through the
/// project-owned counted reader. Every other answer is a prefix-free typed stop that exposes no address, no width, and
/// no copied bytes. The two frozen W8.1 non-admissions, a register home and a selected frame's own generic arguments,
/// keep their own issues and their own diagnostic codes so no consumer can mistake either for a physical gap.
/// </remarks>
public sealed class StaticFieldV2FrameValueRootOutcome : IEquatable<StaticFieldV2FrameValueRootOutcome>
{
    /// <summary>Gets the frozen diagnostic code retained by a register-home frame-root draft non-admission.</summary>
    public const string FrameRegisterHomeNotAdmittedCode = "W8_FRAME_ROOT_REGISTER_HOME_NOT_ADMITTED";

    /// <summary>Gets the frozen diagnostic code retained by a selected-frame generic-argument non-admission.</summary>
    public const string FrameGenericArgumentNotAdmittedCode = "W8_FRAME_ROOT_GENERIC_ARGUMENT_NOT_ADMITTED";

    /// <summary>Gets the frozen diagnostic code retained by an absent selected-thread frame-root draft stop.</summary>
    public const string SelectedThreadAbsentCode = "W8_FRAME_ROOT_THREAD_ABSENT";

    /// <summary>Gets the frozen diagnostic code retained by an ambiguous selected-thread frame-root draft stop.</summary>
    public const string SelectedThreadAmbiguousCode = "W8_FRAME_ROOT_THREAD_AMBIGUOUS";

    /// <summary>Gets the frozen diagnostic code retained by an absent selected-frame draft stop.</summary>
    public const string SelectedFrameAbsentCode = "W8_FRAME_ROOT_FRAME_ABSENT";

    /// <summary>Gets the frozen diagnostic code retained by an unavailable instruction-offset draft stop.</summary>
    public const string InstructionOffsetUnavailableCode = "W8_FRAME_ROOT_INSTRUCTION_OFFSET_UNAVAILABLE";

    /// <summary>Gets the frozen diagnostic code retained by an absent physical root-ordinal draft stop.</summary>
    public const string RootOrdinalAbsentCode = "W8_FRAME_ROOT_ORDINAL_ABSENT";

    /// <summary>Gets the frozen diagnostic code retained by an entirely undeclared local-name draft stop.</summary>
    public const string LocalNameAbsentCode = "W8_FRAME_ROOT_LOCAL_NAME_ABSENT";

    /// <summary>Gets the frozen diagnostic code retained by a lexically inactive local draft stop.</summary>
    public const string LocalLexicallyInactiveCode = "W8_FRAME_ROOT_LOCAL_LEXICALLY_INACTIVE";

    /// <summary>Gets the frozen diagnostic code retained by a duplicate live local-name draft stop.</summary>
    public const string LocalNameAmbiguousCode = "W8_FRAME_ROOT_LOCAL_NAME_AMBIGUOUS";

    /// <summary>Gets the frozen diagnostic code retained by a zero-location frame-root draft stop.</summary>
    public const string LocationAbsentCode = "W8_FRAME_ROOT_LOCATION_ABSENT";

    /// <summary>Gets the frozen diagnostic code retained by a split-location frame-root draft stop.</summary>
    public const string LocationSplitCode = "W8_FRAME_ROOT_LOCATION_SPLIT";

    /// <summary>Gets the frozen diagnostic code retained by an unadmitted-flag frame-root draft stop.</summary>
    public const string LocationUnknownCode = "W8_FRAME_ROOT_LOCATION_UNKNOWN";

    /// <summary>Gets the frozen diagnostic code retained by an inadmissible payload-width draft stop.</summary>
    public const string RootWidthNotAdmittedCode = "W8_FRAME_ROOT_WIDTH_NOT_ADMITTED";

    /// <summary>Gets the frozen diagnostic code retained by an unreachable legacy stack-walk draft stop.</summary>
    public const string InteropUnavailableCode = "W8_FRAME_ROOT_INTEROP_UNAVAILABLE";

    /// <summary>Gets the frozen diagnostic code retained by a short counted frame-root draft read.</summary>
    public const string CountedReadIncompleteCode = "W8_FRAME_ROOT_COUNTED_READ_INCOMPLETE";

    private const string CanonicalDomain = "static-field-v2-frame-value-root-outcome";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<byte> rootBytes;
    private readonly ImmutableArray<StaticFieldV2RuntimeAcquisitionBoundary> declaredBoundaries;
    private readonly ImmutableArray<byte> canonicalBytes;

    private StaticFieldV2FrameValueRootOutcome(
        StaticFieldV2RuntimeAcquisitionResultKind resultKind,
        StaticFieldV2RuntimeAcquisitionStage stage,
        StaticFieldV2RuntimeAcquisitionIssue issue,
        StaticFieldV2FrameValueRootRequest request,
        StaticFieldV2RuntimeSnapshotEvidence evidence,
        ulong? rootAddress,
        int? rootWidth,
        bool? isLive,
        ImmutableArray<byte> rootBytes,
        StaticFieldV2CapabilityCallLedger capabilityCallLedger,
        ImmutableArray<StaticFieldV2RuntimeAcquisitionBoundary> declaredBoundaries,
        string? diagnosticCode)
    {
        ResultKind = resultKind;
        Stage = stage;
        Issue = issue;
        Request = request;
        Evidence = evidence;
        RootAddress = rootAddress;
        RootWidth = rootWidth;
        IsLive = isLive;
        this.rootBytes = rootBytes;
        CapabilityCallLedger = capabilityCallLedger;
        this.declaredBoundaries = declaredBoundaries;
        DiagnosticCode = diagnosticCode;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteInt32((int)resultKind);
        writer.WriteInt32((int)stage);
        writer.WriteInt32((int)issue);
        writer.WriteSha256(request.Sha256, nameof(request));
        writer.WriteSha256(evidence.Sha256, nameof(evidence));
        ExpressionV2ContractEncoding.WriteOptionalUInt64(writer, rootAddress);
        ExpressionV2ContractEncoding.WriteOptionalInt32(writer, rootWidth);
        writer.WriteBoolean(isLive is not null);
        writer.WriteBoolean(isLive ?? false);
        writer.WriteLengthPrefixedBytes(rootBytes.AsSpan());
        writer.WriteSha256(capabilityCallLedger.Sha256, nameof(capabilityCallLedger));
        writer.WriteInt32(declaredBoundaries.Length);
        foreach (var boundary in declaredBoundaries)
        {
            writer.WriteInt32((int)boundary);
        }
        ExpressionV2ContractEncoding.WriteOptionalString(writer, diagnosticCode);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets whether this draft acquisition is exact, non-exact, invalid, or a typed non-admission.</summary>
    public StaticFieldV2RuntimeAcquisitionResultKind ResultKind { get; }

    /// <summary>Gets the exact draft acquisition step that stopped, or none for an exact answer.</summary>
    public StaticFieldV2RuntimeAcquisitionStage Stage { get; }

    /// <summary>Gets the typed draft acquisition issue, or none for an exact answer.</summary>
    public StaticFieldV2RuntimeAcquisitionIssue Issue { get; }

    /// <summary>Gets the complete draft request that produced this outcome.</summary>
    public StaticFieldV2FrameValueRootRequest Request { get; }

    /// <summary>Gets the retained physical draft evidence of the pinned snapshot.</summary>
    public StaticFieldV2RuntimeSnapshotEvidence Evidence { get; }

    /// <summary>Gets the exact attributed memory-home address of the draft root, otherwise null.</summary>
    public ulong? RootAddress { get; }

    /// <summary>Gets the exact attributed draft root width in bytes, otherwise null.</summary>
    public int? RootWidth { get; }

    /// <summary>Gets whether the draft root is live at the selected instruction offset, otherwise null.</summary>
    public bool? IsLive { get; }

    /// <summary>Gets a defensive copy of the bytes copied from the attributed draft root, otherwise empty.</summary>
    public ImmutableArray<byte> RootBytes => ExpressionV2ContractEncoding.Copy(rootBytes);

    /// <summary>Gets the capability-call draft ledger proving which acquisitions this outcome performed.</summary>
    public StaticFieldV2CapabilityCallLedger CapabilityCallLedger { get; }

    /// <summary>Gets a defensive ascending copy of every declared coverage boundary of this draft answer.</summary>
    public ImmutableArray<StaticFieldV2RuntimeAcquisitionBoundary> DeclaredBoundaries =>
        ExpressionV2ContractEncoding.Copy(declaredBoundaries);

    /// <summary>Gets the frozen diagnostic code of a typed draft stop, otherwise null.</summary>
    public string? DiagnosticCode { get; }

    /// <summary>Gets a defensive copy of the bounded fixed-reference canonical draft outcome bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of the canonical draft outcome.</summary>
    public string Sha256 { get; }

    /// <summary>Tests canonical equality between two frame-value root draft outcomes.</summary>
    /// <param name="other">The other draft outcome.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical draft content.</returns>
    public bool Equals(StaticFieldV2FrameValueRootOutcome? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <summary>Tests frame-value root draft outcome equality against an arbitrary object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> only for an outcome with identical canonical draft content.</returns>
    public override bool Equals(object? obj) => Equals(obj as StaticFieldV2FrameValueRootOutcome);

    /// <summary>Computes a deterministic hash code from immutable canonical draft outcome content.</summary>
    /// <returns>A hash code for this canonical draft outcome.</returns>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    internal static StaticFieldV2FrameValueRootOutcome IssueStop(
        StaticFieldV2RuntimeAcquisitionResultKind resultKind,
        StaticFieldV2RuntimeAcquisitionStage stage,
        StaticFieldV2RuntimeAcquisitionIssue issue,
        StaticFieldV2FrameValueRootRequest request,
        StaticFieldV2RuntimeSnapshotEvidence evidence,
        bool? isLive,
        ImmutableArray<StaticFieldV2RuntimeAcquisitionBoundary> declaredBoundaries,
        string diagnosticCode)
    {
        ExpressionV2ContractEncoding.RequireDiagnosticCode(diagnosticCode, nameof(diagnosticCode));
        return new StaticFieldV2FrameValueRootOutcome(
            resultKind,
            stage,
            issue,
            request,
            evidence,
            null,
            null,
            isLive,
            ImmutableArray<byte>.Empty,
            StaticFieldV2LiteralValueOutcome.IssueLedger(request.CapabilityProbes),
            declaredBoundaries,
            diagnosticCode);
    }

    internal static StaticFieldV2FrameValueRootOutcome IssueExact(
        StaticFieldV2FrameValueRootRequest request,
        StaticFieldV2RuntimeSnapshotEvidence evidence,
        ulong rootAddress,
        int rootWidth,
        ImmutableArray<byte> rootBytes,
        ImmutableArray<StaticFieldV2RuntimeAcquisitionBoundary> declaredBoundaries) =>
        new(
            StaticFieldV2RuntimeAcquisitionResultKind.Exact,
            StaticFieldV2RuntimeAcquisitionStage.None,
            StaticFieldV2RuntimeAcquisitionIssue.None,
            request,
            evidence,
            rootAddress,
            rootWidth,
            true,
            rootBytes,
            StaticFieldV2LiteralValueOutcome.IssueLedger(request.CapabilityProbes),
            declaredBoundaries,
            null);
}

/// <summary>Acquires exact physical runtime draft facts for one static-field expression from a pinned full dump.</summary>
/// <remarks>
/// This draft adapter is the physical counterpart of the landed W8.1 runtime contracts. It opens one dump, reads the
/// pinned runtime's own contract descriptor for every structure offset, sweeps every loaded module's available-type
/// table, and emits complete <see cref="StaticFieldV2RuntimeConstructionCandidate"/> rows whose ordered closed
/// arguments are projected through the landed metadata authority.
/// <para>
/// Nothing here parses a runtime display name, performs a global name lookup, or selects by enumeration order. A
/// candidate is keyed only by definition module, TypeDef token, and its ordered projected arguments; a thread is keyed
/// only by a TypeDef plus MethodDef frame predicate; a slot is keyed only by a method table plus FieldDef token.
/// </para>
/// <para>
/// Every acquisition first consults the frozen capability-requirement vector of the classified strategy. A capability
/// marked <see cref="StaticFieldV2CapabilityRequirement.NotRequired"/> is never acquired and never routed through the
/// caller's probe set, so a poisoned probe proves non-invocation executably rather than by documentation.
/// </para>
/// </remarks>
public sealed class StaticFieldV2RuntimeAcquisitionSession : IDisposable
{
    /// <summary>Gets the maximum admitted enumerated runtime module count of one pinned draft snapshot.</summary>
    public const int MaximumRuntimeModuleCount = 1_024;

    /// <summary>Gets the maximum admitted available-type bucket count of one loader module draft table.</summary>
    public const int MaximumHashBucketCount = 65_536;

    /// <summary>Gets the maximum admitted available-type entry count of one loader module draft table.</summary>
    public const int MaximumHashEntryCount = 65_536;

    /// <summary>Gets the maximum admitted complete metadata image byte count of one draft module.</summary>
    public const int MaximumModuleMetadataByteCount = 64 * 1_024 * 1_024;

    /// <summary>Gets the maximum admitted contract-descriptor JSON byte count.</summary>
    public const int MaximumDescriptorByteCount = 4 * 1_024 * 1_024;

    /// <summary>Gets the maximum admitted contract-descriptor pointer-data slot count.</summary>
    public const int MaximumPointerDataSlotCount = 65_536;

    /// <summary>Gets the maximum admitted closed-argument projection depth of one draft candidate.</summary>
    public const int MaximumProjectionDepth = StaticFieldV2Limits.MaximumClosedTypeTopologyDepth;

    /// <summary>Gets the maximum admitted enumerated runtime thread count of one pinned draft snapshot.</summary>
    public const int MaximumRuntimeThreadCount = ExpressionV2ContractLimits.MaximumFrameThreadCount;

    /// <summary>Gets the maximum admitted managed frame count examined per draft thread.</summary>
    public const int MaximumFrameCountPerThread = ExpressionV2ContractLimits.MaximumFrameCountPerThread;

    /// <summary>Gets the maximum admitted static-field count examined per draft construction.</summary>
    public const int MaximumFieldCountPerConstruction = StaticFieldV2Limits.MaximumFieldCountPerConstruction;

    private const string DescriptorExportName = "DotNetRuntimeContractDescriptor";
    private const ulong DescriptorMagic = 0x0043414443434E44UL;
    private const uint DescriptorPointerSizeFlag = 0x2;
    private const uint MethodTableGenericsMask = 0x30;
    private const uint MethodTableCategoryMask = 0x000F0000;
    private const uint MethodTableArrayCategory = 0x00080000;
    private const uint MethodTableSzArrayCategory = 0x000A0000;
    private const uint MethodTableArrayMask = 0x000C0000;
    private const int TypeDefinitionRowIdShift = 8;
    private const int HashBucketPrefixSlotCount = 3;
    private const ulong HashEndSentinelMask = 0x1;
    private const ulong TypeHandleDescriptorMask = 0x2;
    private const int MaximumDescriptorFieldOffset = 1 * 1_024 * 1_024;

    private static readonly (string TypeName, string FieldName)[] RequiredDescriptorFields =
    [
        ("Module", "AvailableTypeParams"),
        ("EETypeHashTable", "Buckets"),
        ("EETypeHashTable", "Count"),
        ("EETypeHashTable", "VolatileEntryValue"),
        ("EETypeHashTable", "VolatileEntryNextEntry"),
        ("MethodTable", "MTFlags"),
        ("MethodTable", "MTFlags2"),
        ("MethodTable", "Module"),
        ("MethodTable", "PerInstInfo"),
        ("MethodTable", "EEClassOrCanonMT"),
        ("GenericsDictInfo", "NumDicts"),
        ("GenericsDictInfo", "NumTypeArgs"),
        ("ArrayClass", "Rank"),
        ("TypeDesc", "TypeAndFlags"),
        ("ParamTypeDesc", "TypeArg"),
    ];

    private readonly DataTarget dataTarget;
    private readonly ClrRuntime runtime;
    private readonly ClrmdProcessMemoryReader memoryReader;
    private readonly ImmutableDictionary<string, ImmutableDictionary<string, int>> layout;
    private readonly ImmutableArray<ClrModule> runtimeModules;
    private bool disposed;

    private StaticFieldV2RuntimeAcquisitionSession(
        DataTarget dataTarget,
        ClrRuntime runtime,
        ClrmdProcessMemoryReader memoryReader,
        ImmutableDictionary<string, ImmutableDictionary<string, int>> layout,
        ImmutableArray<ClrModule> runtimeModules,
        ImmutableArray<StaticFieldV2RuntimeModuleObservation> modules,
        StaticFieldV2RuntimeSnapshotEvidence evidence)
    {
        this.dataTarget = dataTarget;
        this.runtime = runtime;
        this.memoryReader = memoryReader;
        this.layout = layout;
        this.runtimeModules = runtimeModules;
        Modules = modules;
        Evidence = evidence;
    }

    /// <summary>Gets the retained physical draft evidence of the opened pinned snapshot.</summary>
    public StaticFieldV2RuntimeSnapshotEvidence Evidence { get; }

    /// <summary>Gets a defensive address-ordered copy of every enumerated runtime module draft observation.</summary>
    public ImmutableArray<StaticFieldV2RuntimeModuleObservation> Modules { get; }

    /// <summary>Gets the exact target pointer width in bytes of the opened pinned draft snapshot.</summary>
    public int PointerWidth => Evidence.PointerWidth;

    /// <summary>Opens one pinned full dump and reads the runtime's own contract descriptor draft layout.</summary>
    /// <param name="dumpPath">The complete path of a full dump written from one pinned managed process.</param>
    /// <remarks>
    /// Acquisition is offline: every ClrMD file-locator request is refused, so no symbol server, no network, and no
    /// ambient path policy takes part. The descriptor address is read from the runtime module's own export table and
    /// cross-checked against the address the runtime reports.
    /// </remarks>
    /// <returns>An opened draft session whose evidence proves which runtime layout every later read used.</returns>
    /// <exception cref="ArgumentException"><paramref name="dumpPath"/> is empty or whitespace.</exception>
    /// <exception cref="StaticFieldV2RuntimeAcquisitionException">The pinned snapshot could not supply the layout.</exception>
    public static StaticFieldV2RuntimeAcquisitionSession Open(string dumpPath)
    {
        if (string.IsNullOrWhiteSpace(dumpPath))
        {
            throw new ArgumentException("A complete dump path is required.", nameof(dumpPath));
        }

        var options = new DataTargetOptions
        {
            CacheOptions = new CacheOptions
            {
                MaxDumpCacheSize = 256L * 1_024 * 1_024,
                CacheStackRoots = false,
                CacheStackTraces = false,
            },
            FileLocator = ClrmdOfflineFileLocator.Instance,
        };

        var dataTarget = DataTarget.LoadDump(dumpPath, options);
        try
        {
            var clrVersions = dataTarget.ClrVersions;
            if (clrVersions.Length != 1)
            {
                throw new StaticFieldV2RuntimeAcquisitionException(
                    StaticFieldV2RuntimeAcquisitionCodes.RuntimeCardinalityCode,
                    $"The pinned snapshot reports {clrVersions.Length} managed runtimes.");
            }

            var clrInfo = clrVersions[0];
            var runtime = clrInfo.CreateRuntime();
            try
            {
                var memoryReader = new ClrmdProcessMemoryReader(dataTarget.DataReader, dumpPath);
                var reader = new ExactSnapshotReader(dataTarget.DataReader);
                var descriptor = ReadContractDescriptor(dataTarget, clrInfo, reader);
                var modules = runtime
                    .EnumerateModules()
                    .Take(MaximumRuntimeModuleCount + 1)
                    .ToImmutableArray();
                if (modules.Length > MaximumRuntimeModuleCount)
                {
                    throw new StaticFieldV2RuntimeAcquisitionException(
                        StaticFieldV2RuntimeAcquisitionCodes.ModuleCountBoundCode,
                        $"The pinned snapshot exposes more than {MaximumRuntimeModuleCount} runtime modules.");
                }

                var ordered = modules
                    .Where(static module => module.Address != 0)
                    .OrderBy(static module => module.Address)
                    .ToImmutableArray();
                var observations = ordered
                    .Select(static module => new StaticFieldV2RuntimeModuleObservation(
                        module.Address,
                        module.AssemblyAddress,
                        module.AppDomain.Address,
                        module.ImageBase,
                        module.Size,
                        module.MetadataAddress,
                        module.MetadataLength,
                        module.LoaderAllocator))
                    .ToImmutableArray();
                var evidence = StaticFieldV2RuntimeSnapshotEvidence.Create(
                    ComputeSnapshotDigest(observations),
                    reader.PointerSize,
                    descriptor.Address,
                    descriptor.Flags,
                    descriptor.Size,
                    descriptor.JsonSha256,
                    descriptor.PointerDataSha256,
                    descriptor.LoaderContractVersion,
                    descriptor.RuntimeTypeSystemContractVersion,
                    observations.Length);
                return new StaticFieldV2RuntimeAcquisitionSession(
                    dataTarget,
                    runtime,
                    memoryReader,
                    descriptor.Layout,
                    ordered,
                    observations,
                    evidence);
            }
            catch
            {
                runtime.Dispose();
                throw;
            }
        }
        catch
        {
            dataTarget.Dispose();
            throw;
        }
    }

    /// <summary>Copies one runtime module's complete ECMA-335 metadata image out of the pinned draft snapshot.</summary>
    /// <param name="runtimeModuleAddress">The nonzero runtime module structure address.</param>
    /// <remarks>The copy lets a caller bind a runtime module to an on-disk artifact by exact content alone.</remarks>
    /// <returns>The counted complete metadata image bytes of that draft module.</returns>
    /// <exception cref="ObjectDisposedException">The draft session is already disposed.</exception>
    /// <exception cref="StaticFieldV2RuntimeAcquisitionException">The metadata image could not be copied exactly.</exception>
    public ImmutableArray<byte> ReadModuleMetadata(ulong runtimeModuleAddress)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        var module = runtimeModules.FirstOrDefault(candidate => candidate.Address == runtimeModuleAddress);
        if (module is null || module.MetadataAddress == 0 || module.MetadataLength == 0)
        {
            throw new StaticFieldV2RuntimeAcquisitionException(
                StaticFieldV2RuntimeAcquisitionCodes.ModuleMetadataUnavailableCode,
                $"Runtime module 0x{runtimeModuleAddress:x} exposes no complete metadata image.");
        }
        if (module.MetadataLength > (ulong)MaximumModuleMetadataByteCount)
        {
            throw new StaticFieldV2RuntimeAcquisitionException(
                StaticFieldV2RuntimeAcquisitionCodes.ModuleMetadataUnavailableCode,
                $"Runtime module 0x{runtimeModuleAddress:x} reports an inadmissible metadata length.");
        }

        var length = checked((int)module.MetadataLength);
        var result = memoryReader.Read(module.MetadataAddress, length);
        if (result.Status != MemoryReadStatus.Exact)
        {
            throw new StaticFieldV2RuntimeAcquisitionException(
                StaticFieldV2RuntimeAcquisitionCodes.ModuleMetadataUnavailableCode,
                $"The metadata image of runtime module 0x{runtimeModuleAddress:x} could not be copied exactly.");
        }
        return result.Bytes;
    }

    /// <summary>Creates one metadata module-instance draft identity anchored to this pinned snapshot.</summary>
    /// <param name="runtimeModuleAddress">The nonzero runtime module structure address to anchor.</param>
    /// <remarks>The snapshot digest and pointer width come from this session, so every produced module agrees.</remarks>
    /// <returns>A module-instance identity carrying this snapshot's digest and that module's geometry.</returns>
    /// <exception cref="ObjectDisposedException">The draft session is already disposed.</exception>
    /// <exception cref="StaticFieldV2RuntimeAcquisitionException">No enumerated module has that address.</exception>
    public StaticFieldModuleInstanceIdentity CreateModuleInstance(ulong runtimeModuleAddress)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        var observation = Modules.FirstOrDefault(candidate => candidate.ModuleAddress == runtimeModuleAddress)
            ?? throw new StaticFieldV2RuntimeAcquisitionException(
                StaticFieldV2RuntimeAcquisitionCodes.ModuleBindingAbsentCode,
                $"The pinned snapshot enumerates no runtime module at 0x{runtimeModuleAddress:x}.");
        return StaticFieldModuleInstanceIdentity.Create(
            Evidence.SnapshotSha256,
            Evidence.PointerWidth,
            observation.ApplicationDomainAddress,
            observation.ModuleAddress,
            observation.ImageBase,
            observation.ImageSize);
    }

    /// <summary>Acquires every complete same-definition draft candidate and runs the landed selection over them.</summary>
    /// <param name="request">The complete constructed-runtime candidate draft acquisition request.</param>
    /// <remarks>
    /// The sweep visits every loaded module's available-type table because the loader module owns the table of a
    /// construction over a type declared elsewhere. A visited entry contributes a candidate only when its method table
    /// names the bound definition module and TypeDef token; every such entry whose loader module, containing assembly,
    /// or ordered argument cannot be projected through the landed authority is counted and excluded.
    /// </remarks>
    /// <returns>A sealed immutable draft outcome carrying the emitted rows and the landed selection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="ObjectDisposedException">The draft session is already disposed.</exception>
    public StaticFieldV2RuntimeConstructionAcquisitionOutcome AcquireConstruction(
        StaticFieldV2RuntimeConstructionAcquisitionRequest request)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(request);

        var boundaries = ImmutableArray.Create(
            StaticFieldV2RuntimeAcquisitionBoundary.RuntimeLayoutSuppliedByContractDescriptor,
            StaticFieldV2RuntimeAcquisitionBoundary.UnprojectableConstructionArgumentsCountedAndExcluded,
            StaticFieldV2RuntimeAcquisitionBoundary.CorelibPrimitiveCollapseNotModeled);
        var strategy = request.StorageStrategy.Strategy!.Value;
        var probes = request.CapabilityProbes;
        if (request.StorageStrategy.CapabilityRequirements.RuntimeConstruction !=
            StaticFieldV2CapabilityRequirement.Required)
        {
            var refused = StaticFieldV2RuntimeConstructionBinder.SelectConstruction(
                StaticFieldV2RuntimeConstructionRequest.Create(
                    request.MetadataConstruction,
                    request.StorageStrategy,
                    ImmutableArray<StaticFieldV2RuntimeConstructionCandidate>.Empty,
                    probes));
            return StaticFieldV2RuntimeConstructionAcquisitionOutcome.IssueOutcome(
                StaticFieldV2RuntimeAcquisitionResultKind.Unsupported,
                StaticFieldV2RuntimeAcquisitionStage.CapabilityVector,
                strategy == StaticFieldV2StorageStrategy.MetadataLiteral
                    ? StaticFieldV2RuntimeAcquisitionIssue.MetadataLiteralHasNoRuntimeStorage
                    : StaticFieldV2RuntimeAcquisitionIssue.CapabilityNotRequiredForStrategy,
                request,
                Evidence,
                ImmutableArray<StaticFieldV2RuntimeConstructionCandidate>.Empty,
                refused,
                boundaries,
                null,
                0,
                0,
                0,
                strategy == StaticFieldV2StorageStrategy.MetadataLiteral
                    ? StaticFieldV2StaticSlotAcquisitionOutcome.MetadataLiteralHasNoRuntimeStorageCode
                    : StaticFieldV2RuntimeConstructionAcquisitionOutcome.CapabilityNotRequiredCode);
        }

        var bindings = BuildBindingMap(request.ModuleBindingsCore);
        var owner = request.MetadataConstruction.OwnerConstruction!;
        var definitionModule = owner.FinalClassification!.SourceModule;
        var typeDefinitionToken = owner.FinalClassification!.TypeDefinition.TypeDefinitionToken;
        var definitionBinding = request.ModuleBindingsCore
            .FirstOrDefault(binding => binding.MetadataModule.Equals(definitionModule));
        if (definitionBinding is null)
        {
            return StaticFieldV2RuntimeConstructionAcquisitionOutcome.IssueOutcome(
                StaticFieldV2RuntimeAcquisitionResultKind.NonExact,
                StaticFieldV2RuntimeAcquisitionStage.ModuleBinding,
                StaticFieldV2RuntimeAcquisitionIssue.RuntimeModuleBindingAbsent,
                request,
                Evidence,
                ImmutableArray<StaticFieldV2RuntimeConstructionCandidate>.Empty,
                null,
                boundaries,
                null,
                0,
                0,
                0,
                StaticFieldV2RuntimeConstructionAcquisitionOutcome.ModuleBindingAbsentCode);
        }

        var reader = new ExactSnapshotReader(dataTarget.DataReader);
        var examinedEntryCount = 0;
        var sameDefinitionEntryCount = 0;
        var unprojectableCandidateCount = 0;
        var accepted = new SortedDictionary<string, StaticFieldV2RuntimeConstructionCandidate>(StringComparer.Ordinal);
        try
        {
            foreach (var module in runtimeModules)
            {
                if (!bindings.TryGetValue(module.Address, out var loaderBinding))
                {
                    loaderBinding = null;
                }

                foreach (var typeHandle in ReadAvailableTypeHandles(reader, module.Address))
                {
                    examinedEntryCount = checked(examinedEntryCount + 1);
                    if (!TryReadMethodTableIdentity(reader, typeHandle, out var identity) ||
                        identity is null ||
                        identity.ModuleAddress != definitionBinding.RuntimeModuleAddress ||
                        identity.TypeDefinitionToken != typeDefinitionToken)
                    {
                        continue;
                    }

                    sameDefinitionEntryCount = checked(sameDefinitionEntryCount + 1);
                    var candidate = TryCreateCandidate(
                        reader,
                        request,
                        bindings,
                        definitionModule,
                        typeDefinitionToken,
                        loaderBinding,
                        identity);
                    if (candidate is null)
                    {
                        unprojectableCandidateCount = checked(unprojectableCandidateCount + 1);
                        continue;
                    }

                    accepted[candidate.Sha256] = candidate;
                    if (accepted.Count > StaticFieldV2Limits.MaximumRuntimeConstructionCount)
                    {
                        return StaticFieldV2RuntimeConstructionAcquisitionOutcome.IssueOutcome(
                            StaticFieldV2RuntimeAcquisitionResultKind.NonExact,
                            StaticFieldV2RuntimeAcquisitionStage.AvailableTypeTable,
                            StaticFieldV2RuntimeAcquisitionIssue.ConstructionCandidateCountBoundReached,
                            request,
                            Evidence,
                            ImmutableArray<StaticFieldV2RuntimeConstructionCandidate>.Empty,
                            null,
                            boundaries,
                            new EvaluationDeterministicBound(
                                ExpressionV2ContractLimits.RuntimeConstructionCountBoundName,
                                StaticFieldV2Limits.MaximumRuntimeConstructionCount),
                            examinedEntryCount,
                            sameDefinitionEntryCount,
                            unprojectableCandidateCount,
                            StaticFieldV2RuntimeConstructionAcquisitionOutcome.CandidateCountBoundReachedCode);
                    }
                }
            }
        }
        catch (StaticFieldV2RuntimeAcquisitionException)
        {
            return StaticFieldV2RuntimeConstructionAcquisitionOutcome.IssueOutcome(
                StaticFieldV2RuntimeAcquisitionResultKind.NonExact,
                StaticFieldV2RuntimeAcquisitionStage.AvailableTypeTable,
                StaticFieldV2RuntimeAcquisitionIssue.RuntimeLayoutEvidenceUnavailable,
                request,
                Evidence,
                ImmutableArray<StaticFieldV2RuntimeConstructionCandidate>.Empty,
                null,
                boundaries,
                null,
                examinedEntryCount,
                sameDefinitionEntryCount,
                unprojectableCandidateCount,
                StaticFieldV2RuntimeConstructionAcquisitionOutcome.RuntimeLayoutUnavailableCode);
        }

        var candidates = accepted.Values.ToImmutableArray();
        var selection = StaticFieldV2RuntimeConstructionBinder.SelectConstruction(
            StaticFieldV2RuntimeConstructionRequest.Create(
                request.MetadataConstruction,
                request.StorageStrategy,
                candidates,
                probes));
        var exact = selection.ResultKind == StaticFieldV2RuntimeConstructionSelectionKind.Exact;
        return StaticFieldV2RuntimeConstructionAcquisitionOutcome.IssueOutcome(
            exact
                ? StaticFieldV2RuntimeAcquisitionResultKind.Exact
                : StaticFieldV2RuntimeAcquisitionResultKind.NonExact,
            exact
                ? StaticFieldV2RuntimeAcquisitionStage.None
                : StaticFieldV2RuntimeAcquisitionStage.ConstructionSelection,
            exact
                ? StaticFieldV2RuntimeAcquisitionIssue.None
                : StaticFieldV2RuntimeAcquisitionIssue.ConstructionSelectionNonExact,
            request,
            Evidence,
            candidates,
            selection,
            boundaries,
            null,
            examinedEntryCount,
            sameDefinitionEntryCount,
            unprojectableCandidateCount,
            exact ? null : selection.DiagnosticCode);
    }

    /// <summary>Acquires one exact static storage slot and routes it through the landed slot acquisition.</summary>
    /// <param name="request">The complete static-slot draft acquisition request.</param>
    /// <remarks>
    /// A metadata literal short-circuits before any physical work, a module RVA acquires module content plus the exact
    /// FieldRVA row and mapped geometry, a constructed slot acquires the selected construction's slot address, and a
    /// thread-relative slot additionally selects one exact thread by its token-keyed frame predicate.
    /// </remarks>
    /// <returns>A sealed immutable draft outcome carrying the acquired facts and the landed slot answer.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="ObjectDisposedException">The draft session is already disposed.</exception>
    public StaticFieldV2StaticSlotAcquisitionOutcome AcquireStaticSlot(
        StaticFieldV2StaticSlotAcquisitionRequest request)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(request);

        var boundaries = ImmutableArray.Create(
            StaticFieldV2RuntimeAcquisitionBoundary.RuntimeLayoutSuppliedByContractDescriptor,
            StaticFieldV2RuntimeAcquisitionBoundary.CustomAttributeTableNotModeled);
        var strategy = request.StorageStrategy.Strategy!.Value;
        var probes = request.CapabilityProbes;
        if (strategy == StaticFieldV2StorageStrategy.MetadataLiteral)
        {
            var refused = StaticFieldV2RuntimeConstructionBinder.AcquireStaticSlot(
                StaticFieldV2StaticSlotRequest.Create(
                    request.StorageStrategy,
                    request.ReadWidth,
                    capabilityProbes: probes));
            return StaticFieldV2StaticSlotAcquisitionOutcome.IssueOutcome(
                StaticFieldV2RuntimeAcquisitionResultKind.Unsupported,
                StaticFieldV2RuntimeAcquisitionStage.CapabilityVector,
                StaticFieldV2RuntimeAcquisitionIssue.MetadataLiteralHasNoRuntimeStorage,
                request,
                Evidence,
                refused,
                null,
                null,
                null,
                null,
                null,
                boundaries,
                0,
                StaticFieldV2StaticSlotAcquisitionOutcome.MetadataLiteralHasNoRuntimeStorageCode);
        }

        if (strategy == StaticFieldV2StorageStrategy.ModuleRva)
        {
            return AcquireModuleRvaSlot(request, boundaries);
        }

        return AcquireConstructedSlot(request, strategy, boundaries);
    }

    /// <summary>Copies exactly the declared width out of the snapshot and decodes it with the landed decoder.</summary>
    /// <param name="request">The complete counted raw draft read and value decoding request.</param>
    /// <remarks>
    /// The counted read is the only capability this method owns. Exactly <c>ReadWidth</c> bytes are requested and a
    /// short read becomes a typed stop, so no partially observed value and no zero-filled suffix can ever be decoded.
    /// The decoding itself is the landed pure decoder over exactly the copied bytes.
    /// </remarks>
    /// <returns>A sealed immutable draft outcome carrying the copied bytes, their digest, and the landed decoding.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="ObjectDisposedException">The draft session is already disposed.</exception>
    public StaticFieldV2RuntimeValueAcquisitionOutcome AcquireValue(
        StaticFieldV2RuntimeValueAcquisitionRequest request)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(request);

        var boundaries = ImmutableArray.Create(
            StaticFieldV2RuntimeAcquisitionBoundary.RuntimeLayoutSuppliedByContractDescriptor);
        var probes = request.CapabilityProbes;
        var slot = request.Slot.Slot!;
        if (request.StorageStrategy.CapabilityRequirements.MemoryRead !=
            StaticFieldV2CapabilityRequirement.Required)
        {
            return StaticFieldV2RuntimeValueAcquisitionOutcome.IssueOutcome(
                StaticFieldV2RuntimeAcquisitionResultKind.Unsupported,
                StaticFieldV2RuntimeAcquisitionStage.CapabilityVector,
                StaticFieldV2RuntimeAcquisitionIssue.CapabilityNotRequiredForStrategy,
                request,
                Evidence,
                ImmutableArray<byte>.Empty,
                null,
                boundaries,
                slot.ReadWidth,
                0,
                StaticFieldV2RuntimeValueAcquisitionOutcome.MemoryReadNotRequiredCode);
        }

        probes?.Invoke(StaticFieldV2StorageCapability.MemoryRead);
        var read = memoryReader.Read(slot.EffectiveAddress, slot.ReadWidth);
        if (read.Status != MemoryReadStatus.Exact)
        {
            return StaticFieldV2RuntimeValueAcquisitionOutcome.IssueOutcome(
                StaticFieldV2RuntimeAcquisitionResultKind.NonExact,
                StaticFieldV2RuntimeAcquisitionStage.CountedRead,
                StaticFieldV2RuntimeAcquisitionIssue.CountedReadIncomplete,
                request,
                Evidence,
                ImmutableArray<byte>.Empty,
                null,
                boundaries,
                slot.ReadWidth,
                read.BytesRead,
                StaticFieldV2RuntimeValueAcquisitionOutcome.CountedReadIncompleteCode);
        }

        var decoded = StaticFieldV2ValueDecoder.DecodeValue(
            StaticFieldV2RuntimeValueRequest.Create(
                request.DeclaredType,
                read.Bytes,
                PointerWidth,
                request.EnumUnderlyingKind,
                request.NullableLayout,
                probes));
        return StaticFieldV2RuntimeValueAcquisitionOutcome.IssueOutcome(
            StaticFieldV2RuntimeAcquisitionResultKind.Exact,
            StaticFieldV2RuntimeAcquisitionStage.None,
            StaticFieldV2RuntimeAcquisitionIssue.None,
            request,
            Evidence,
            read.Bytes,
            decoded,
            boundaries,
            slot.ReadWidth,
            read.BytesRead,
            null);
    }

    /// <summary>Attributes one named frame-value root of one selected frame to its exact memory home.</summary>
    /// <param name="request">The complete frame-value root draft acquisition request.</param>
    /// <remarks>
    /// The acquisition selects exactly one thread by the token-keyed frame predicate, takes the named selector-matching
    /// managed frame, and resolves the physical variable ordinal before touching the pinned runtime's legacy stack-walk
    /// data interface. A named local is resolved by correlating the caller-supplied Portable-PDB rows against the exact
    /// IL offset of the frame's instruction pointer, so an out-of-scope local stops as lexically inactive before any
    /// read. Only after one exact single memory location is observed is the counted read performed, and it is the only
    /// capability call this path routes. A register home and a selected frame's own generic arguments keep their frozen
    /// W8.1 non-admissions: no register is ever read and no substitution service exists here at all.
    /// </remarks>
    /// <returns>A sealed immutable draft outcome carrying the exact home or one prefix-free typed stop.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="ObjectDisposedException">The draft session is already disposed.</exception>
    public StaticFieldV2FrameValueRootOutcome AcquireFrameValueRoot(StaticFieldV2FrameValueRootRequest request)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(request);

        var boundaries = ImmutableArray.Create(
            StaticFieldV2RuntimeAcquisitionBoundary.FrameRegisterHomeNotAdmitted,
            StaticFieldV2RuntimeAcquisitionBoundary.FrameGenericArgumentSubstitutionNotModeled,
            StaticFieldV2RuntimeAcquisitionBoundary.FrameLocalScopesSuppliedByCaller,
            StaticFieldV2RuntimeAcquisitionBoundary.FrameHomeSuppliedByLegacyStackWalkInterface,
            StaticFieldV2RuntimeAcquisitionBoundary.FrameReceiverIdentifiedByLegacyArgumentName);
        if (request.RootKind is StaticFieldV2FrameValueRootKind.DeclaringTypeGenericArgument or
            StaticFieldV2FrameValueRootKind.MethodGenericArgument)
        {
            return FrameStop(
                StaticFieldV2RuntimeAcquisitionResultKind.Unsupported,
                StaticFieldV2RuntimeAcquisitionIssue.FrameGenericArgumentNotAdmitted,
                request,
                boundaries,
                StaticFieldV2FrameValueRootOutcome.FrameGenericArgumentNotAdmittedCode);
        }

        var threads = SelectThreads(request.ThreadSelector);
        if (threads.Length == 0)
        {
            return FrameStop(
                StaticFieldV2RuntimeAcquisitionResultKind.NonExact,
                StaticFieldV2RuntimeAcquisitionIssue.SelectedThreadAbsent,
                request,
                boundaries,
                StaticFieldV2FrameValueRootOutcome.SelectedThreadAbsentCode,
                stage: StaticFieldV2RuntimeAcquisitionStage.ThreadSelection);
        }
        if (threads.Length > 1)
        {
            return FrameStop(
                StaticFieldV2RuntimeAcquisitionResultKind.NonExact,
                StaticFieldV2RuntimeAcquisitionIssue.SelectedThreadAmbiguous,
                request,
                boundaries,
                StaticFieldV2FrameValueRootOutcome.SelectedThreadAmbiguousCode,
                stage: StaticFieldV2RuntimeAcquisitionStage.ThreadSelection);
        }

        var thread = threads[0];
        var selected = SelectMatchingFrames(thread, request.ThreadSelector);
        if (request.FrameOrdinal >= selected.Length)
        {
            return FrameStop(
                StaticFieldV2RuntimeAcquisitionResultKind.NonExact,
                StaticFieldV2RuntimeAcquisitionIssue.SelectedFrameAbsent,
                request,
                boundaries,
                StaticFieldV2FrameValueRootOutcome.SelectedFrameAbsentCode);
        }

        var frame = selected[request.FrameOrdinal];
        var instructionOffset = frame.Method!.GetILOffset(frame.InstructionPointer);
        if (instructionOffset < 0)
        {
            return FrameStop(
                StaticFieldV2RuntimeAcquisitionResultKind.NonExact,
                StaticFieldV2RuntimeAcquisitionIssue.FrameInstructionOffsetUnavailable,
                request,
                boundaries,
                StaticFieldV2FrameValueRootOutcome.InstructionOffsetUnavailableCode);
        }

        var slot = 0;
        if (request.RootKind == StaticFieldV2FrameValueRootKind.Local)
        {
            var declared = request.LocalScopeRowsCore
                .Where(row => string.Equals(row.Name, request.LocalName, StringComparison.Ordinal))
                .ToImmutableArray();
            if (declared.Length == 0)
            {
                return FrameStop(
                    StaticFieldV2RuntimeAcquisitionResultKind.NonExact,
                    StaticFieldV2RuntimeAcquisitionIssue.FrameLocalNameAbsent,
                    request,
                    boundaries,
                    StaticFieldV2FrameValueRootOutcome.LocalNameAbsentCode);
            }

            var live = declared
                .Where(row => row.ScopeStartOffset <= instructionOffset && instructionOffset < row.ScopeEndOffset)
                .ToImmutableArray();
            if (live.Length == 0)
            {
                return FrameStop(
                    StaticFieldV2RuntimeAcquisitionResultKind.NonExact,
                    StaticFieldV2RuntimeAcquisitionIssue.FrameLocalLexicallyInactive,
                    request,
                    boundaries,
                    StaticFieldV2FrameValueRootOutcome.LocalLexicallyInactiveCode,
                    isLive: false);
            }
            if (live.Length > 1)
            {
                return FrameStop(
                    StaticFieldV2RuntimeAcquisitionResultKind.NonExact,
                    StaticFieldV2RuntimeAcquisitionIssue.FrameLocalNameAmbiguous,
                    request,
                    boundaries,
                    StaticFieldV2FrameValueRootOutcome.LocalNameAmbiguousCode);
            }

            slot = live[0].SlotIndex;
        }

        var located = StaticFieldV2FrameValueInterop.Locate(
            runtime,
            thread.OSThreadId,
            frame.InstructionPointer,
            frame.StackPointer,
            dataTarget.DataReader.Architecture,
            request.RootKind,
            request.RootOrdinal,
            slot);
        if (located.Disposition != StaticFieldV2FrameLocationDisposition.Exact)
        {
            return FrameStop(
                located.Disposition == StaticFieldV2FrameLocationDisposition.RegisterHome
                    ? StaticFieldV2RuntimeAcquisitionResultKind.Unsupported
                    : StaticFieldV2RuntimeAcquisitionResultKind.NonExact,
                LocationIssue(located.Disposition),
                request,
                boundaries,
                LocationCode(located.Disposition));
        }

        request.CapabilityProbes?.Invoke(StaticFieldV2StorageCapability.MemoryRead);
        var read = memoryReader.Read(located.Address, located.Width);
        if (read.Status != MemoryReadStatus.Exact)
        {
            return FrameStop(
                StaticFieldV2RuntimeAcquisitionResultKind.NonExact,
                StaticFieldV2RuntimeAcquisitionIssue.CountedReadIncomplete,
                request,
                boundaries,
                StaticFieldV2FrameValueRootOutcome.CountedReadIncompleteCode,
                stage: StaticFieldV2RuntimeAcquisitionStage.CountedRead);
        }

        return StaticFieldV2FrameValueRootOutcome.IssueExact(
            request,
            Evidence,
            located.Address,
            located.Width,
            read.Bytes,
            boundaries);
    }

    /// <summary>Releases the pinned dump session and every ClrMD resource it owns.</summary>
    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        memoryReader.Dispose();
        runtime.Dispose();
        dataTarget.Dispose();
    }

    private static ImmutableDictionary<ulong, StaticFieldV2RuntimeModuleBinding> BuildBindingMap(
        ImmutableArray<StaticFieldV2RuntimeModuleBinding> bindings)
    {
        var builder = ImmutableDictionary.CreateBuilder<ulong, StaticFieldV2RuntimeModuleBinding>();
        foreach (var binding in bindings)
        {
            builder[binding.RuntimeModuleAddress] = binding;
        }
        return builder.ToImmutable();
    }

    private static string ComputeSnapshotDigest(ImmutableArray<StaticFieldV2RuntimeModuleObservation> observations)
    {
        var writer = new CanonicalReplayEncoding.Writer("static-field-v2-runtime-snapshot-geometry", 1);
        writer.WriteInt32(observations.Length);
        foreach (var observation in observations)
        {
            writer.WriteUInt64(observation.ModuleAddress);
            writer.WriteUInt64(observation.AssemblyAddress);
            writer.WriteUInt64(observation.ApplicationDomainAddress);
            writer.WriteUInt64(observation.ImageBase);
            writer.WriteUInt64(observation.ImageSize);
            writer.WriteUInt64(observation.MetadataAddress);
            writer.WriteUInt64(observation.MetadataLength);
            writer.WriteUInt64(observation.LoaderAllocatorAddress);
        }
        return CanonicalReplayEncoding.ComputeSha256(writer.ToImmutableArray().AsSpan());
    }

    private static ParsedContractDescriptor ReadContractDescriptor(
        DataTarget dataTarget,
        ClrInfo clrInfo,
        ExactSnapshotReader reader)
    {
        var runtimeModules = dataTarget.DataReader
            .EnumerateModules()
            .Where(candidate => candidate.ImageBase == clrInfo.ModuleInfo.ImageBase)
            .Take(2)
            .ToArray();
        if (runtimeModules.Length != 1)
        {
            throw new StaticFieldV2RuntimeAcquisitionException(
                StaticFieldV2RuntimeAcquisitionCodes.RuntimeCardinalityCode,
                $"The pinned snapshot exposes {runtimeModules.Length} runtime modules at the reported image base.");
        }

        var descriptorAddress = runtimeModules[0].GetExportSymbolAddress(DescriptorExportName);
        if (descriptorAddress == 0)
        {
            throw new StaticFieldV2RuntimeAcquisitionException(
                StaticFieldV2RuntimeAcquisitionCodes.DescriptorUnavailableCode,
                $"The pinned runtime module does not export {DescriptorExportName}.");
        }
        if (clrInfo.ContractDescriptorAddress != 0 && clrInfo.ContractDescriptorAddress != descriptorAddress)
        {
            throw new StaticFieldV2RuntimeAcquisitionException(
                StaticFieldV2RuntimeAcquisitionCodes.DescriptorUnavailableCode,
                "The runtime module export and the reported descriptor addresses disagree.");
        }

        var pointerDataOffset = checked(24 + reader.PointerSize);
        var header = reader.ReadExact(descriptorAddress, checked(pointerDataOffset + reader.PointerSize));
        if (BinaryPrimitives.ReadUInt64LittleEndian(header) != DescriptorMagic)
        {
            throw new StaticFieldV2RuntimeAcquisitionException(
                StaticFieldV2RuntimeAcquisitionCodes.DescriptorUnavailableCode,
                $"Descriptor address 0x{descriptorAddress:x} does not carry the little-endian descriptor magic.");
        }

        var flags = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(8, sizeof(uint)));
        var descriptorPointerSize = (flags & DescriptorPointerSizeFlag) == 0 ? sizeof(ulong) : sizeof(uint);
        if (descriptorPointerSize != reader.PointerSize)
        {
            throw new StaticFieldV2RuntimeAcquisitionException(
                StaticFieldV2RuntimeAcquisitionCodes.DescriptorUnavailableCode,
                "The descriptor pointer size and the snapshot pointer size disagree.");
        }

        var descriptorSize = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(12, sizeof(uint)));
        if (descriptorSize == 0 || descriptorSize > MaximumDescriptorByteCount)
        {
            throw new StaticFieldV2RuntimeAcquisitionException(
                StaticFieldV2RuntimeAcquisitionCodes.DescriptorUnavailableCode,
                $"The descriptor byte count {descriptorSize} is outside the admitted range.");
        }

        var jsonAddress = ReadPointerValue(header.AsSpan(16, reader.PointerSize), reader.PointerSize);
        var pointerDataCount = BinaryPrimitives.ReadUInt32LittleEndian(
            header.AsSpan(checked(16 + reader.PointerSize), sizeof(uint)));
        if (pointerDataCount > MaximumPointerDataSlotCount)
        {
            throw new StaticFieldV2RuntimeAcquisitionException(
                StaticFieldV2RuntimeAcquisitionCodes.DescriptorUnavailableCode,
                $"The descriptor pointer-data slot count {pointerDataCount} is outside the admitted range.");
        }

        var pointerDataAddress = ReadPointerValue(header.AsSpan(pointerDataOffset, reader.PointerSize), reader.PointerSize);
        if (jsonAddress == 0 || (pointerDataCount != 0 && pointerDataAddress == 0))
        {
            throw new StaticFieldV2RuntimeAcquisitionException(
                StaticFieldV2RuntimeAcquisitionCodes.DescriptorUnavailableCode,
                "The descriptor contains a null address for required data.");
        }

        var jsonBytes = reader.ReadExact(jsonAddress, checked((int)descriptorSize));
        var pointerDataBytes = pointerDataCount == 0
            ? []
            : reader.ReadExact(pointerDataAddress, checked((int)pointerDataCount * reader.PointerSize));
        var parsed = ParseDescriptorJson(jsonBytes);
        return new ParsedContractDescriptor(
            descriptorAddress,
            flags,
            descriptorSize,
            Convert.ToHexString(SHA256.HashData(jsonBytes)).ToLowerInvariant(),
            Convert.ToHexString(SHA256.HashData(pointerDataBytes)).ToLowerInvariant(),
            parsed.LoaderContractVersion,
            parsed.RuntimeTypeSystemContractVersion,
            parsed.Layout);
    }

    private static ParsedDescriptorJson ParseDescriptorJson(byte[] jsonBytes)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(jsonBytes);
        }
        catch (JsonException exception)
        {
            throw new StaticFieldV2RuntimeAcquisitionException(
                StaticFieldV2RuntimeAcquisitionCodes.DescriptorUnavailableCode,
                "The pinned runtime descriptor JSON could not be parsed.",
                exception);
        }

        using (document)
        {
            var root = document.RootElement;
            var descriptorVersion = ReadDescriptorVersion(root, "version");
            var contracts = root.GetProperty("contracts");
            var loaderVersion = ReadDescriptorVersion(contracts, "Loader");
            var runtimeTypeSystemVersion = ReadDescriptorVersion(contracts, "RuntimeTypeSystem");
            if (descriptorVersion != 0 || loaderVersion != 1 || runtimeTypeSystemVersion != 1)
            {
                throw new StaticFieldV2RuntimeAcquisitionException(
                    StaticFieldV2RuntimeAcquisitionCodes.DescriptorUnavailableCode,
                    $"Observed descriptor/Loader/RuntimeTypeSystem versions " +
                    $"{descriptorVersion}/{loaderVersion}/{runtimeTypeSystemVersion}.");
            }

            var typeElements = root.GetProperty("types");
            var typeBuilder = ImmutableDictionary.CreateBuilder<string, ImmutableDictionary<string, int>>(
                StringComparer.Ordinal);
            foreach (var typeName in RequiredDescriptorFields.Select(static field => field.TypeName).Distinct())
            {
                if (!typeElements.TryGetProperty(typeName, out var typeElement) ||
                    typeElement.ValueKind != JsonValueKind.Object)
                {
                    throw new StaticFieldV2RuntimeAcquisitionException(
                        StaticFieldV2RuntimeAcquisitionCodes.DescriptorUnavailableCode,
                        $"The pinned runtime descriptor does not define type {typeName}.");
                }

                var fieldBuilder = ImmutableDictionary.CreateBuilder<string, int>(StringComparer.Ordinal);
                foreach (var field in RequiredDescriptorFields.Where(candidate => candidate.TypeName == typeName))
                {
                    if (!typeElement.TryGetProperty(field.FieldName, out var fieldElement))
                    {
                        throw new StaticFieldV2RuntimeAcquisitionException(
                            StaticFieldV2RuntimeAcquisitionCodes.DescriptorUnavailableCode,
                            $"The pinned runtime descriptor does not define {typeName}.{field.FieldName}.");
                    }

                    fieldBuilder.Add(field.FieldName, ReadDescriptorFieldOffset(fieldElement, typeName, field.FieldName));
                }

                typeBuilder.Add(typeName, fieldBuilder.ToImmutable());
            }

            return new ParsedDescriptorJson(loaderVersion, runtimeTypeSystemVersion, typeBuilder.ToImmutable());
        }
    }

    private static int ReadDescriptorVersion(JsonElement parent, string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out var element))
        {
            throw new StaticFieldV2RuntimeAcquisitionException(
                StaticFieldV2RuntimeAcquisitionCodes.DescriptorUnavailableCode,
                $"The pinned runtime descriptor does not declare {propertyName}.");
        }
        if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var numeric))
        {
            return numeric;
        }
        if (element.ValueKind == JsonValueKind.String &&
            int.TryParse(element.GetString(), NumberStyles.None, CultureInfo.InvariantCulture, out numeric))
        {
            return numeric;
        }
        throw new StaticFieldV2RuntimeAcquisitionException(
            StaticFieldV2RuntimeAcquisitionCodes.DescriptorUnavailableCode,
            $"The pinned runtime descriptor value {propertyName} is not an integer version.");
    }

    private static int ReadDescriptorFieldOffset(JsonElement element, string typeName, string fieldName)
    {
        var offsetElement = element;
        if (element.ValueKind == JsonValueKind.Array)
        {
            var enumerator = element.EnumerateArray();
            if (!enumerator.MoveNext())
            {
                throw new StaticFieldV2RuntimeAcquisitionException(
                    StaticFieldV2RuntimeAcquisitionCodes.DescriptorUnavailableCode,
                    $"The pinned runtime descriptor field {typeName}.{fieldName} has an empty offset tuple.");
            }
            offsetElement = enumerator.Current;
        }
        if (offsetElement.ValueKind != JsonValueKind.Number ||
            !offsetElement.TryGetInt32(out var offset) ||
            offset < 0 ||
            offset > MaximumDescriptorFieldOffset)
        {
            throw new StaticFieldV2RuntimeAcquisitionException(
                StaticFieldV2RuntimeAcquisitionCodes.DescriptorUnavailableCode,
                $"The pinned runtime descriptor field {typeName}.{fieldName} has an inadmissible offset.");
        }
        return offset;
    }

    private static ulong ReadPointerValue(ReadOnlySpan<byte> bytes, int pointerSize) =>
        pointerSize == sizeof(uint)
            ? BinaryPrimitives.ReadUInt32LittleEndian(bytes)
            : BinaryPrimitives.ReadUInt64LittleEndian(bytes);

    private static ulong Add(ulong address, int offset) => checked(address + checked((ulong)offset));

    private int FieldOffset(string typeName, string fieldName)
    {
        if (!layout.TryGetValue(typeName, out var fields) || !fields.TryGetValue(fieldName, out var offset))
        {
            throw new StaticFieldV2RuntimeAcquisitionException(
                StaticFieldV2RuntimeAcquisitionCodes.DescriptorUnavailableCode,
                $"The pinned runtime descriptor does not define {typeName}.{fieldName}.");
        }
        return offset;
    }

    private ImmutableArray<ulong> ReadAvailableTypeHandles(ExactSnapshotReader reader, ulong moduleAddress)
    {
        var table = reader.ReadPointer(Add(moduleAddress, FieldOffset("Module", "AvailableTypeParams")));
        if (table == 0)
        {
            return ImmutableArray<ulong>.Empty;
        }

        var buckets = reader.ReadPointer(Add(table, FieldOffset("EETypeHashTable", "Buckets")));
        var declaredCount = reader.ReadUInt32(Add(table, FieldOffset("EETypeHashTable", "Count")));
        if (buckets == 0)
        {
            throw new StaticFieldV2RuntimeAcquisitionException(
                StaticFieldV2RuntimeAcquisitionCodes.AvailableTypeTableUnreadableCode,
                "The non-null available-type table has a null bucket vector.");
        }

        var bucketCountValue = reader.ReadPointer(buckets);
        if (bucketCountValue > MaximumHashBucketCount)
        {
            throw new StaticFieldV2RuntimeAcquisitionException(
                StaticFieldV2RuntimeAcquisitionCodes.AvailableTypeTableUnreadableCode,
                $"The available-type table declares more than {MaximumHashBucketCount} buckets.");
        }

        var bucketCount = checked((int)bucketCountValue);
        var handles = ImmutableArray.CreateBuilder<ulong>();
        var visited = new HashSet<ulong>();
        for (var bucketIndex = 0; bucketIndex < bucketCount; bucketIndex++)
        {
            var slot = checked((ulong)(bucketIndex + HashBucketPrefixSlotCount) * (ulong)reader.PointerSize);
            var chain = reader.ReadPointer(checked(buckets + slot));
            while ((chain & HashEndSentinelMask) != HashEndSentinelMask)
            {
                if (chain == 0 || !visited.Add(chain) || handles.Count == MaximumHashEntryCount)
                {
                    throw new StaticFieldV2RuntimeAcquisitionException(
                        StaticFieldV2RuntimeAcquisitionCodes.AvailableTypeTableUnreadableCode,
                        $"The available-type table chain of bucket {bucketIndex} is not a bounded acyclic list.");
                }

                var tagged = reader.ReadPointer(Add(chain, FieldOffset("EETypeHashTable", "VolatileEntryValue")));
                var typeHandle = tagged & ~HashEndSentinelMask;
                if (typeHandle == 0)
                {
                    throw new StaticFieldV2RuntimeAcquisitionException(
                        StaticFieldV2RuntimeAcquisitionCodes.AvailableTypeTableUnreadableCode,
                        $"Available-type chain node 0x{chain:x} contains a null type handle.");
                }

                handles.Add(typeHandle);
                chain = reader.ReadPointer(Add(chain, FieldOffset("EETypeHashTable", "VolatileEntryNextEntry")));
            }
        }

        if (handles.Count != declaredCount)
        {
            throw new StaticFieldV2RuntimeAcquisitionException(
                StaticFieldV2RuntimeAcquisitionCodes.AvailableTypeTableUnreadableCode,
                $"The available-type table declares {declaredCount} entries but traversal observed {handles.Count}.");
        }

        return handles.ToImmutable();
    }

    private bool TryReadMethodTableIdentity(
        ExactSnapshotReader reader,
        ulong typeHandle,
        out RuntimeMethodTableIdentity? identity)
    {
        identity = null;
        if (typeHandle == 0 ||
            (typeHandle & TypeHandleDescriptorMask) != 0 ||
            (typeHandle & checked((ulong)reader.PointerSize - 1UL)) != 0)
        {
            return false;
        }

        var methodTableFlags = reader.ReadUInt32(Add(typeHandle, FieldOffset("MethodTable", "MTFlags")));
        var methodTableFlags2 = reader.ReadUInt32(Add(typeHandle, FieldOffset("MethodTable", "MTFlags2")));
        var moduleAddress = reader.ReadPointer(Add(typeHandle, FieldOffset("MethodTable", "Module")));
        var typeDefinitionRowId = methodTableFlags2 >> TypeDefinitionRowIdShift;
        var typeDefinitionToken = typeDefinitionRowId == 0
            ? 0
            : checked((int)(0x0200_0000U | typeDefinitionRowId));

        if ((methodTableFlags & MethodTableArrayMask) == MethodTableArrayCategory)
        {
            identity = new RuntimeMethodTableIdentity(
                typeHandle,
                moduleAddress,
                typeDefinitionToken,
                methodTableFlags,
                ImmutableArray<ulong>.Empty,
                reader.ReadPointer(Add(typeHandle, FieldOffset("MethodTable", "PerInstInfo"))));
            return true;
        }

        if ((methodTableFlags & MethodTableGenericsMask) == 0)
        {
            identity = new RuntimeMethodTableIdentity(
                typeHandle,
                moduleAddress,
                typeDefinitionToken,
                methodTableFlags,
                ImmutableArray<ulong>.Empty,
                0);
            return true;
        }

        var perInstInfo = reader.ReadPointer(Add(typeHandle, FieldOffset("MethodTable", "PerInstInfo")));
        if (perInstInfo < (ulong)reader.PointerSize)
        {
            throw new StaticFieldV2RuntimeAcquisitionException(
                StaticFieldV2RuntimeAcquisitionCodes.AvailableTypeTableUnreadableCode,
                $"Generic method table 0x{typeHandle:x} exposes no readable per-instantiation vector.");
        }

        var dictionaryInfoAddress = perInstInfo - (ulong)reader.PointerSize;
        var dictionaryCount = reader.ReadUInt16(
            Add(dictionaryInfoAddress, FieldOffset("GenericsDictInfo", "NumDicts")));
        var typeArgumentCount = reader.ReadUInt16(
            Add(dictionaryInfoAddress, FieldOffset("GenericsDictInfo", "NumTypeArgs")));
        if (dictionaryCount == 0 ||
            typeArgumentCount == 0 ||
            typeArgumentCount > StaticFieldV2Limits.MaximumTypeSpecificationArgumentCount)
        {
            throw new StaticFieldV2RuntimeAcquisitionException(
                StaticFieldV2RuntimeAcquisitionCodes.AvailableTypeTableUnreadableCode,
                $"Method table 0x{typeHandle:x} reports an inadmissible generic dictionary shape.");
        }

        var finalDictionary = reader.ReadPointer(checked(
            perInstInfo + checked((ulong)(dictionaryCount - 1) * (ulong)reader.PointerSize)));
        if (finalDictionary == 0)
        {
            throw new StaticFieldV2RuntimeAcquisitionException(
                StaticFieldV2RuntimeAcquisitionCodes.AvailableTypeTableUnreadableCode,
                $"Method table 0x{typeHandle:x} has a null final generic dictionary.");
        }

        var arguments = ImmutableArray.CreateBuilder<ulong>(typeArgumentCount);
        for (var index = 0; index < typeArgumentCount; index++)
        {
            var argument = reader.ReadPointer(checked(
                finalDictionary + checked((ulong)index * (ulong)reader.PointerSize)));
            if (argument == 0)
            {
                throw new StaticFieldV2RuntimeAcquisitionException(
                    StaticFieldV2RuntimeAcquisitionCodes.AvailableTypeTableUnreadableCode,
                    $"Method table 0x{typeHandle:x} has a null ordered argument at index {index}.");
            }
            arguments.Add(argument);
        }

        identity = new RuntimeMethodTableIdentity(
            typeHandle,
            moduleAddress,
            typeDefinitionToken,
            methodTableFlags,
            arguments.MoveToImmutable(),
            perInstInfo);
        return true;
    }

    private StaticFieldV2RuntimeConstructionCandidate? TryCreateCandidate(
        ExactSnapshotReader reader,
        StaticFieldV2RuntimeConstructionAcquisitionRequest request,
        ImmutableDictionary<ulong, StaticFieldV2RuntimeModuleBinding> bindings,
        StaticFieldMetadataModuleIdentity definitionModule,
        int typeDefinitionToken,
        StaticFieldV2RuntimeModuleBinding? loaderBinding,
        RuntimeMethodTableIdentity identity)
    {
        if (loaderBinding is null)
        {
            return null;
        }

        var arguments = ImmutableArray.CreateBuilder<MetadataClosedTypeIdentity>(
            identity.TypeArgumentHandles.Length);
        foreach (var argumentHandle in identity.TypeArgumentHandles)
        {
            var projected = TryProjectTypeHandle(reader, request, bindings, argumentHandle, depth: 1);
            if (projected is null)
            {
                return null;
            }
            arguments.Add(projected);
        }

        if (runtime.GetTypeByMethodTable(identity.TypeHandle) is not { } runtimeType ||
            runtimeType.MethodTable != identity.TypeHandle)
        {
            return null;
        }

        try
        {
            return StaticFieldV2RuntimeConstructionCandidate.Create(
                identity.TypeHandle,
                runtimeType.MethodTable,
                definitionModule,
                typeDefinitionToken,
                loaderBinding.MetadataModule,
                loaderBinding.MetadataModule.ContainingAssembly,
                runtimeType.LoaderAllocatorHandle,
                runtimeType.AssemblyLoadContextAddress,
                arguments.ToImmutable());
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private MetadataClosedTypeIdentity? TryProjectTypeHandle(
        ExactSnapshotReader reader,
        StaticFieldV2RuntimeConstructionAcquisitionRequest request,
        ImmutableDictionary<ulong, StaticFieldV2RuntimeModuleBinding> bindings,
        ulong typeHandle,
        int depth)
    {
        if (depth > MaximumProjectionDepth || typeHandle == 0)
        {
            return null;
        }
        if ((typeHandle & TypeHandleDescriptorMask) != 0)
        {
            return null;
        }
        if (!TryReadMethodTableIdentity(reader, typeHandle, out var identity) || identity is null)
        {
            return null;
        }

        try
        {
            if ((identity.MethodTableFlags & MethodTableArrayMask) == MethodTableArrayCategory)
            {
                var element = TryProjectTypeHandle(
                    reader,
                    request,
                    bindings,
                    identity.ElementOrPerInstInfoAddress,
                    checked(depth + 1));
                if (element is null)
                {
                    return null;
                }
                if ((identity.MethodTableFlags & MethodTableCategoryMask) == MethodTableSzArrayCategory)
                {
                    return MetadataClosedTypeIdentity.SzArray(element);
                }

                var rank = ReadMultiDimensionalArrayRank(reader, typeHandle);
                return MetadataClosedTypeIdentity.MultidimensionalArray(
                    element,
                    rank,
                    ImmutableArray<int>.Empty,
                    ImmutableArray<int>.Empty);
            }

            if (!bindings.TryGetValue(identity.ModuleAddress, out var binding) ||
                identity.TypeDefinitionToken == 0)
            {
                return null;
            }

            var chain = request.ChainPortfolio.ExactChainOrDefault(
                binding.MetadataModule,
                identity.TypeDefinitionToken);
            if (chain is null || chain.IsModulePseudoType)
            {
                return null;
            }

            var classifications = ImmutableArray.CreateBuilder<MetadataTypeDefinitionSemanticClassificationIdentity>(
                chain.Segments.Length);
            foreach (var segment in chain.Segments)
            {
                var classification = request.AncestryPortfolio.ExactClassificationOrDefault(
                    binding.MetadataModule,
                    segment.TypeDefinitionToken);
                if (classification?.Role is null)
                {
                    return null;
                }
                classifications.Add(classification);
            }

            if (chain.FinalTypeDefinition.TotalGenericArity != identity.TypeArgumentHandles.Length)
            {
                return null;
            }

            var arguments = ImmutableArray.CreateBuilder<MetadataClosedTypeIdentity>(
                identity.TypeArgumentHandles.Length);
            foreach (var argumentHandle in identity.TypeArgumentHandles)
            {
                var projected = TryProjectTypeHandle(
                    reader,
                    request,
                    bindings,
                    argumentHandle,
                    checked(depth + 1));
                if (projected is null)
                {
                    return null;
                }
                arguments.Add(projected);
            }

            return MetadataClosedTypeIdentity.ConstructNamed(
                classifications.MoveToImmutable(),
                arguments.MoveToImmutable());
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (MetadataClosedTypeBoundException)
        {
            return null;
        }
    }

    private int ReadMultiDimensionalArrayRank(ExactSnapshotReader reader, ulong methodTable)
    {
        var classOrCanonical = reader.ReadPointer(Add(methodTable, FieldOffset("MethodTable", "EEClassOrCanonMT")));
        if (classOrCanonical == 0)
        {
            throw new StaticFieldV2RuntimeAcquisitionException(
                StaticFieldV2RuntimeAcquisitionCodes.AvailableTypeTableUnreadableCode,
                $"Array method table 0x{methodTable:x} exposes no class evidence.");
        }

        ulong classAddress;
        if ((classOrCanonical & 0x1) == 0)
        {
            classAddress = classOrCanonical;
        }
        else
        {
            var canonical = classOrCanonical & ~0x1UL;
            classAddress = reader.ReadPointer(Add(canonical, FieldOffset("MethodTable", "EEClassOrCanonMT")));
            if (classAddress == 0 || (classAddress & 0x1) != 0)
            {
                throw new StaticFieldV2RuntimeAcquisitionException(
                    StaticFieldV2RuntimeAcquisitionCodes.AvailableTypeTableUnreadableCode,
                    $"Canonical array method table 0x{canonical:x} exposes no direct class evidence.");
            }
        }

        var rank = reader.ReadByte(Add(classAddress, FieldOffset("ArrayClass", "Rank")));
        if (rank == 0 || rank > StaticFieldV2Limits.MaximumArrayRank)
        {
            throw new StaticFieldV2RuntimeAcquisitionException(
                StaticFieldV2RuntimeAcquisitionCodes.AvailableTypeTableUnreadableCode,
                $"Array method table 0x{methodTable:x} reports rank {rank}.");
        }
        return rank;
    }

    private StaticFieldV2StaticSlotAcquisitionOutcome AcquireModuleRvaSlot(
        StaticFieldV2StaticSlotAcquisitionRequest request,
        ImmutableArray<StaticFieldV2RuntimeAcquisitionBoundary> boundaries)
    {
        var binding = request.DeclaringModuleBinding;
        if (binding is null)
        {
            return StaticFieldV2StaticSlotAcquisitionOutcome.IssueOutcome(
                StaticFieldV2RuntimeAcquisitionResultKind.NonExact,
                StaticFieldV2RuntimeAcquisitionStage.ModuleBinding,
                StaticFieldV2RuntimeAcquisitionIssue.RuntimeModuleBindingAbsent,
                request,
                Evidence,
                null,
                null,
                null,
                null,
                null,
                null,
                boundaries,
                0,
                StaticFieldV2RuntimeConstructionAcquisitionOutcome.ModuleBindingAbsentCode);
        }

        ImmutableArray<byte> metadataBytes;
        try
        {
            metadataBytes = ReadModuleMetadata(binding.RuntimeModuleAddress);
        }
        catch (StaticFieldV2RuntimeAcquisitionException)
        {
            return ModuleRvaStop(
                request,
                boundaries,
                StaticFieldV2RuntimeAcquisitionResultKind.NonExact,
                StaticFieldV2RuntimeAcquisitionIssue.RuntimeModuleBindingAbsent,
                StaticFieldV2RuntimeConstructionAcquisitionOutcome.ModuleBindingAbsentCode);
        }

        int? fieldRvaRowId = null;
        uint relativeVirtualAddress = 0;
        using (var provider = MetadataReaderProvider.FromMetadataImage(metadataBytes))
        {
            var metadata = provider.GetMetadataReader();
            if (!ModuleContentIdentity.FromMetadata(
                    metadata.GetGuid(metadata.GetModuleDefinition().Mvid),
                    metadataBytes.AsSpan())
                .Equals(binding.MetadataModule.ModuleContent))
            {
                return ModuleRvaStop(
                    request,
                    boundaries,
                    StaticFieldV2RuntimeAcquisitionResultKind.Invalid,
                    StaticFieldV2RuntimeAcquisitionIssue.RuntimeModuleBindingAbsent,
                    StaticFieldV2RuntimeConstructionAcquisitionOutcome.ModuleBindingAbsentCode);
            }

            var rowId = 0;
            foreach (var handle in metadata.FieldDefinitions)
            {
                var definition = metadata.GetFieldDefinition(handle);
                var address = definition.GetRelativeVirtualAddress();
                if (address == 0)
                {
                    continue;
                }

                rowId = checked(rowId + 1);
                if (MetadataTokens.GetToken(handle) == request.FieldDefinitionToken)
                {
                    fieldRvaRowId = rowId;
                    relativeVirtualAddress = checked((uint)address);
                }
            }

            if (rowId != metadata.GetTableRowCount(TableIndex.FieldRva))
            {
                return ModuleRvaStop(
                    request,
                    boundaries,
                    StaticFieldV2RuntimeAcquisitionResultKind.Invalid,
                    StaticFieldV2RuntimeAcquisitionIssue.ModuleRvaRowAbsent,
                    StaticFieldV2StaticSlotAcquisitionOutcome.ModuleRvaGeometryUnavailableCode);
            }
        }

        if (fieldRvaRowId is not { } rvaRowId)
        {
            return ModuleRvaStop(
                request,
                boundaries,
                StaticFieldV2RuntimeAcquisitionResultKind.NonExact,
                StaticFieldV2RuntimeAcquisitionIssue.ModuleRvaRowAbsent,
                StaticFieldV2StaticSlotAcquisitionOutcome.ModuleRvaGeometryUnavailableCode);
        }

        if (binding.ImageBase == 0 ||
            binding.ImageSize == 0 ||
            relativeVirtualAddress == 0 ||
            (ulong)relativeVirtualAddress + (ulong)request.ReadWidth > binding.ImageSize)
        {
            return ModuleRvaStop(
                request,
                boundaries,
                StaticFieldV2RuntimeAcquisitionResultKind.Invalid,
                StaticFieldV2RuntimeAcquisitionIssue.ModuleRvaGeometryOutsideImage,
                StaticFieldV2StaticSlotAcquisitionOutcome.ModuleRvaGeometryUnavailableCode);
        }

        var mappedAddress = checked(binding.ImageBase + relativeVirtualAddress);
        var slotOutcome = StaticFieldV2RuntimeConstructionBinder.AcquireStaticSlot(
            StaticFieldV2StaticSlotRequest.Create(
                request.StorageStrategy,
                request.ReadWidth,
                moduleContent: binding.MetadataModule.ModuleContent,
                fieldRvaRowToken: 0x1D00_0000 | rvaRowId,
                mappedRelativeVirtualAddress: relativeVirtualAddress,
                mappedAddress: mappedAddress,
                capabilityProbes: request.CapabilityProbes));
        var exact = slotOutcome.ResultKind == StaticFieldV2StaticSlotResultKind.Exact;
        return StaticFieldV2StaticSlotAcquisitionOutcome.IssueOutcome(
            exact
                ? StaticFieldV2RuntimeAcquisitionResultKind.Exact
                : StaticFieldV2RuntimeAcquisitionResultKind.Invalid,
            exact
                ? StaticFieldV2RuntimeAcquisitionStage.None
                : StaticFieldV2RuntimeAcquisitionStage.ModuleGeometry,
            exact
                ? StaticFieldV2RuntimeAcquisitionIssue.None
                : StaticFieldV2RuntimeAcquisitionIssue.StaticSlotAcquisitionNonExact,
            request,
            Evidence,
            slotOutcome,
            null,
            null,
            0x1D00_0000 | rvaRowId,
            relativeVirtualAddress,
            mappedAddress,
            boundaries,
            0,
            exact ? null : slotOutcome.DiagnosticCode);
    }

    private StaticFieldV2StaticSlotAcquisitionOutcome ModuleRvaStop(
        StaticFieldV2StaticSlotAcquisitionRequest request,
        ImmutableArray<StaticFieldV2RuntimeAcquisitionBoundary> boundaries,
        StaticFieldV2RuntimeAcquisitionResultKind resultKind,
        StaticFieldV2RuntimeAcquisitionIssue issue,
        string diagnosticCode) =>
        StaticFieldV2StaticSlotAcquisitionOutcome.IssueOutcome(
            resultKind,
            StaticFieldV2RuntimeAcquisitionStage.ModuleGeometry,
            issue,
            request,
            Evidence,
            null,
            null,
            null,
            null,
            null,
            null,
            boundaries,
            0,
            diagnosticCode);

    private StaticFieldV2StaticSlotAcquisitionOutcome AcquireConstructedSlot(
        StaticFieldV2StaticSlotAcquisitionRequest request,
        StaticFieldV2StorageStrategy strategy,
        ImmutableArray<StaticFieldV2RuntimeAcquisitionBoundary> boundaries)
    {
        var selection = request.ConstructionSelection;
        if (selection is null ||
            selection.ResultKind != StaticFieldV2RuntimeConstructionSelectionKind.Exact ||
            selection.SelectedCandidate is not { } candidate)
        {
            var refused = StaticFieldV2RuntimeConstructionBinder.AcquireStaticSlot(
                StaticFieldV2StaticSlotRequest.Create(
                    request.StorageStrategy,
                    request.ReadWidth,
                    constructionSelection: selection,
                    capabilityProbes: request.CapabilityProbes));
            return StaticFieldV2StaticSlotAcquisitionOutcome.IssueOutcome(
                StaticFieldV2RuntimeAcquisitionResultKind.Invalid,
                StaticFieldV2RuntimeAcquisitionStage.StaticSlotAddress,
                StaticFieldV2RuntimeAcquisitionIssue.StaticSlotAcquisitionNonExact,
                request,
                Evidence,
                refused,
                null,
                null,
                null,
                null,
                null,
                boundaries,
                0,
                refused.DiagnosticCode ?? StaticFieldV2StaticSlotAcquisitionOutcome.SlotAddressUnavailableCode);
        }

        if (runtime.GetTypeByMethodTable(candidate.MethodTableAddress) is not { } runtimeType)
        {
            return SlotAddressStop(request, boundaries, 0);
        }

        StaticFieldV2SelectedThreadIdentity? selectedThread = null;
        var candidateThreadCount = 0;
        ulong slotAddress;
        if (strategy == StaticFieldV2StorageStrategy.ThreadRelativeSlot)
        {
            if (request.ThreadSelector is not { } selector)
            {
                return StaticFieldV2StaticSlotAcquisitionOutcome.IssueOutcome(
                    StaticFieldV2RuntimeAcquisitionResultKind.NonExact,
                    StaticFieldV2RuntimeAcquisitionStage.ThreadSelection,
                    StaticFieldV2RuntimeAcquisitionIssue.SelectedThreadAbsent,
                    request,
                    Evidence,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    boundaries,
                    0,
                    StaticFieldV2StaticSlotAcquisitionOutcome.SelectedThreadAbsentCode);
            }

            var threads = SelectThreads(selector);
            candidateThreadCount = threads.Length;
            if (threads.Length != 1)
            {
                return StaticFieldV2StaticSlotAcquisitionOutcome.IssueOutcome(
                    StaticFieldV2RuntimeAcquisitionResultKind.NonExact,
                    StaticFieldV2RuntimeAcquisitionStage.ThreadSelection,
                    threads.Length == 0
                        ? StaticFieldV2RuntimeAcquisitionIssue.SelectedThreadAbsent
                        : StaticFieldV2RuntimeAcquisitionIssue.SelectedThreadAmbiguous,
                    request,
                    Evidence,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    boundaries,
                    candidateThreadCount,
                    threads.Length == 0
                        ? StaticFieldV2StaticSlotAcquisitionOutcome.SelectedThreadAbsentCode
                        : StaticFieldV2StaticSlotAcquisitionOutcome.SelectedThreadAmbiguousCode);
            }

            var thread = threads[0];
            var threadField = runtimeType.ThreadStaticFields
                .Take(MaximumFieldCountPerConstruction + 1)
                .SingleOrDefault(field => field.Token == request.FieldDefinitionToken);
            if (threadField is null || !threadField.IsInitialized(thread))
            {
                return SlotAddressStop(request, boundaries, candidateThreadCount);
            }

            slotAddress = threadField.GetAddress(thread);
            if (slotAddress == 0 ||
                thread.Address == 0 ||
                thread.ManagedThreadId <= 0 ||
                thread.OSThreadId == 0)
            {
                return SlotAddressStop(request, boundaries, candidateThreadCount);
            }

            selectedThread = StaticFieldV2SelectedThreadIdentity.Create(
                PointerWidth,
                thread.Address,
                checked((int)thread.OSThreadId),
                thread.ManagedThreadId);
        }
        else
        {
            var staticField = runtimeType.StaticFields
                .Take(MaximumFieldCountPerConstruction + 1)
                .SingleOrDefault(field => field.Token == request.FieldDefinitionToken);
            if (staticField is null || !staticField.IsInitialized(runtimeType.Module.AppDomain))
            {
                return SlotAddressStop(request, boundaries, 0);
            }

            slotAddress = staticField.GetAddress(runtimeType.Module.AppDomain);
            if (slotAddress == 0)
            {
                return SlotAddressStop(request, boundaries, 0);
            }
        }

        var slotOutcome = StaticFieldV2RuntimeConstructionBinder.AcquireStaticSlot(
            StaticFieldV2StaticSlotRequest.Create(
                request.StorageStrategy,
                request.ReadWidth,
                constructionSelection: selection,
                slotAddress: slotAddress,
                selectedThread: selectedThread,
                capabilityProbes: request.CapabilityProbes));
        var exact = slotOutcome.ResultKind == StaticFieldV2StaticSlotResultKind.Exact;
        return StaticFieldV2StaticSlotAcquisitionOutcome.IssueOutcome(
            exact
                ? StaticFieldV2RuntimeAcquisitionResultKind.Exact
                : StaticFieldV2RuntimeAcquisitionResultKind.Invalid,
            exact
                ? StaticFieldV2RuntimeAcquisitionStage.None
                : StaticFieldV2RuntimeAcquisitionStage.StaticSlotAddress,
            exact
                ? StaticFieldV2RuntimeAcquisitionIssue.None
                : StaticFieldV2RuntimeAcquisitionIssue.StaticSlotAcquisitionNonExact,
            request,
            Evidence,
            slotOutcome,
            selectedThread,
            slotAddress,
            null,
            null,
            null,
            boundaries,
            candidateThreadCount,
            exact ? null : slotOutcome.DiagnosticCode);
    }

    private StaticFieldV2StaticSlotAcquisitionOutcome SlotAddressStop(
        StaticFieldV2StaticSlotAcquisitionRequest request,
        ImmutableArray<StaticFieldV2RuntimeAcquisitionBoundary> boundaries,
        int candidateThreadCount) =>
        StaticFieldV2StaticSlotAcquisitionOutcome.IssueOutcome(
            StaticFieldV2RuntimeAcquisitionResultKind.NonExact,
            StaticFieldV2RuntimeAcquisitionStage.StaticSlotAddress,
            StaticFieldV2RuntimeAcquisitionIssue.StaticSlotAddressUnavailable,
            request,
            Evidence,
            null,
            null,
            null,
            null,
            null,
            null,
            boundaries,
            candidateThreadCount,
            StaticFieldV2StaticSlotAcquisitionOutcome.SlotAddressUnavailableCode);

    private static ImmutableArray<ClrStackFrame> SelectMatchingFrames(
        ClrThread thread,
        StaticFieldV2RuntimeThreadSelector selector)
    {
        var matches = ImmutableArray.CreateBuilder<ClrStackFrame>();
        foreach (var frame in thread
            .EnumerateStackTrace(includeContext: false, maxFrames: MaximumFrameCountPerThread)
            .Take(MaximumFrameCountPerThread))
        {
            if (frame.Kind == ClrStackFrameKind.ManagedMethod &&
                frame.Method is { } method &&
                method.MetadataToken == selector.MethodDefinitionToken &&
                method.Type is { } declaringType &&
                declaringType.MetadataToken == selector.TypeDefinitionToken &&
                declaringType.Module.Address == selector.RuntimeModuleAddress)
            {
                matches.Add(frame);
            }
        }
        return matches.ToImmutable();
    }

    private static StaticFieldV2RuntimeAcquisitionIssue LocationIssue(
        StaticFieldV2FrameLocationDisposition disposition) => disposition switch
        {
            StaticFieldV2FrameLocationDisposition.RegisterHome =>
                StaticFieldV2RuntimeAcquisitionIssue.FrameRegisterHomeNotAdmitted,
            StaticFieldV2FrameLocationDisposition.RootOrdinalAbsent =>
                StaticFieldV2RuntimeAcquisitionIssue.FrameRootOrdinalAbsent,
            StaticFieldV2FrameLocationDisposition.LocationAbsent =>
                StaticFieldV2RuntimeAcquisitionIssue.FrameRootLocationAbsent,
            StaticFieldV2FrameLocationDisposition.LocationSplit =>
                StaticFieldV2RuntimeAcquisitionIssue.FrameRootLocationSplit,
            StaticFieldV2FrameLocationDisposition.LocationUnknown =>
                StaticFieldV2RuntimeAcquisitionIssue.FrameRootLocationUnknown,
            StaticFieldV2FrameLocationDisposition.WidthNotAdmitted =>
                StaticFieldV2RuntimeAcquisitionIssue.FrameRootWidthNotAdmitted,
            _ => StaticFieldV2RuntimeAcquisitionIssue.FrameInteropUnavailable,
        };

    private static string LocationCode(StaticFieldV2FrameLocationDisposition disposition) => disposition switch
    {
        StaticFieldV2FrameLocationDisposition.RegisterHome =>
            StaticFieldV2FrameValueRootOutcome.FrameRegisterHomeNotAdmittedCode,
        StaticFieldV2FrameLocationDisposition.RootOrdinalAbsent =>
            StaticFieldV2FrameValueRootOutcome.RootOrdinalAbsentCode,
        StaticFieldV2FrameLocationDisposition.LocationAbsent =>
            StaticFieldV2FrameValueRootOutcome.LocationAbsentCode,
        StaticFieldV2FrameLocationDisposition.LocationSplit =>
            StaticFieldV2FrameValueRootOutcome.LocationSplitCode,
        StaticFieldV2FrameLocationDisposition.LocationUnknown =>
            StaticFieldV2FrameValueRootOutcome.LocationUnknownCode,
        StaticFieldV2FrameLocationDisposition.WidthNotAdmitted =>
            StaticFieldV2FrameValueRootOutcome.RootWidthNotAdmittedCode,
        _ => StaticFieldV2FrameValueRootOutcome.InteropUnavailableCode,
    };

    private StaticFieldV2FrameValueRootOutcome FrameStop(
        StaticFieldV2RuntimeAcquisitionResultKind resultKind,
        StaticFieldV2RuntimeAcquisitionIssue issue,
        StaticFieldV2FrameValueRootRequest request,
        ImmutableArray<StaticFieldV2RuntimeAcquisitionBoundary> boundaries,
        string diagnosticCode,
        bool? isLive = null,
        StaticFieldV2RuntimeAcquisitionStage stage = StaticFieldV2RuntimeAcquisitionStage.FrameRoot) =>
        StaticFieldV2FrameValueRootOutcome.IssueStop(
            resultKind,
            stage,
            issue,
            request,
            Evidence,
            isLive,
            boundaries,
            diagnosticCode);

    private ImmutableArray<ClrThread> SelectThreads(StaticFieldV2RuntimeThreadSelector selector)
    {
        var matches = ImmutableArray.CreateBuilder<ClrThread>();
        foreach (var thread in runtime.Threads.Take(MaximumRuntimeThreadCount))
        {
            var frames = thread
                .EnumerateStackTrace(includeContext: false, maxFrames: MaximumFrameCountPerThread)
                .Take(MaximumFrameCountPerThread);
            foreach (var frame in frames)
            {
                if (frame.Kind == ClrStackFrameKind.ManagedMethod &&
                    frame.Method is { } method &&
                    method.MetadataToken == selector.MethodDefinitionToken &&
                    method.Type is { } declaringType &&
                    declaringType.MetadataToken == selector.TypeDefinitionToken &&
                    declaringType.Module.Address == selector.RuntimeModuleAddress)
                {
                    matches.Add(thread);
                    break;
                }
            }
        }
        return matches.ToImmutable();
    }

    private sealed record RuntimeMethodTableIdentity(
        ulong TypeHandle,
        ulong ModuleAddress,
        int TypeDefinitionToken,
        uint MethodTableFlags,
        ImmutableArray<ulong> TypeArgumentHandles,
        ulong ElementOrPerInstInfoAddress);

    private sealed record ParsedDescriptorJson(
        int LoaderContractVersion,
        int RuntimeTypeSystemContractVersion,
        ImmutableDictionary<string, ImmutableDictionary<string, int>> Layout);

    private sealed record ParsedContractDescriptor(
        ulong Address,
        uint Flags,
        uint Size,
        string JsonSha256,
        string PointerDataSha256,
        int LoaderContractVersion,
        int RuntimeTypeSystemContractVersion,
        ImmutableDictionary<string, ImmutableDictionary<string, int>> Layout);

    private sealed class ExactSnapshotReader
    {
        private readonly IMemoryReader reader;

        internal ExactSnapshotReader(IMemoryReader reader)
        {
            this.reader = reader;
            if (reader.PointerSize is not (sizeof(uint) or sizeof(ulong)))
            {
                throw new StaticFieldV2RuntimeAcquisitionException(
                    StaticFieldV2RuntimeAcquisitionCodes.DescriptorUnavailableCode,
                    $"A pointer size of {reader.PointerSize} bytes is not admitted.");
            }
        }

        internal int PointerSize => reader.PointerSize;

        internal byte[] ReadExact(ulong address, int length)
        {
            if (address == 0 || length <= 0)
            {
                throw new StaticFieldV2RuntimeAcquisitionException(
                    StaticFieldV2RuntimeAcquisitionCodes.SnapshotReadFailedCode,
                    "A required snapshot read names a null address or an empty range.");
            }

            var bytes = new byte[length];
            int observed;
            try
            {
                observed = reader.Read(address, bytes);
            }
            catch (Exception exception) when (exception is not StaticFieldV2RuntimeAcquisitionException)
            {
                throw new StaticFieldV2RuntimeAcquisitionException(
                    StaticFieldV2RuntimeAcquisitionCodes.SnapshotReadFailedCode,
                    $"The snapshot read at 0x{address:x} failed.",
                    exception);
            }

            if (observed != length)
            {
                throw new StaticFieldV2RuntimeAcquisitionException(
                    StaticFieldV2RuntimeAcquisitionCodes.SnapshotReadFailedCode,
                    $"The snapshot read at 0x{address:x} requested {length} bytes and returned {observed}.");
            }
            return bytes;
        }

        internal byte ReadByte(ulong address) => ReadExact(address, sizeof(byte))[0];

        internal ushort ReadUInt16(ulong address) =>
            BinaryPrimitives.ReadUInt16LittleEndian(ReadExact(address, sizeof(ushort)));

        internal uint ReadUInt32(ulong address) =>
            BinaryPrimitives.ReadUInt32LittleEndian(ReadExact(address, sizeof(uint)));

        internal ulong ReadPointer(ulong address) => PointerSize == sizeof(uint)
            ? BinaryPrimitives.ReadUInt32LittleEndian(ReadExact(address, sizeof(uint)))
            : BinaryPrimitives.ReadUInt64LittleEndian(ReadExact(address, sizeof(ulong)));
    }
}

/// <summary>Classifies the physical home one selected frame-value root reports at the selected instruction.</summary>
internal enum StaticFieldV2FrameLocationDisposition
{
    /// <summary>Exactly one memory home with a nonzero address and an admitted payload width was observed.</summary>
    Exact = 1,

    /// <summary>The pinned runtime's legacy stack-walk data interface could not be reached exactly.</summary>
    InteropUnavailable = 2,

    /// <summary>The selected frame declares no argument or local at the requested physical ordinal.</summary>
    RootOrdinalAbsent = 3,

    /// <summary>The root reports no location at all at the selected instruction.</summary>
    LocationAbsent = 4,

    /// <summary>The root is split across more than one reported location.</summary>
    LocationSplit = 5,

    /// <summary>The root reports one location whose flag word or argument is not admitted.</summary>
    LocationUnknown = 6,

    /// <summary>The root is homed in a register, which this draft slice never reads.</summary>
    RegisterHome = 7,

    /// <summary>The root reports a payload width outside the admitted draft range.</summary>
    WidthNotAdmitted = 8,
}

/// <summary>Carries one classified frame-value root home plus its exact address and counted payload width.</summary>
/// <param name="Disposition">The classified physical home of the selected root.</param>
/// <param name="Address">The exact memory-home address, or zero for every non-exact disposition.</param>
/// <param name="Width">The counted payload width in bytes, or zero for every non-exact disposition.</param>
internal readonly record struct StaticFieldV2FrameLocation(
    StaticFieldV2FrameLocationDisposition Disposition,
    ulong Address,
    int Width);

/// <summary>Contains every reflection and raw vtable call the frame-value root draft acquisition performs.</summary>
/// <remarks>
/// The pinned runtime's managed object model exposes no frame-value home, so the home is read through the runtime's own
/// legacy stack-walk data interface reached by reflection over the loaded diagnostics assembly. Every member lookup,
/// every vtable shape, and every HRESULT is checked, and every fault becomes one classified non-exact disposition, so
/// no exception escapes this adapter and no location is ever fabricated.
/// </remarks>
internal static class StaticFieldV2FrameValueInterop
{
    /// <summary>Gets the maximum admitted counted payload width in bytes of one frame-value draft root.</summary>
    internal const int MaximumRootWidth = 64;

    private const int OperationSucceeded = 0;
    private const uint StackWalkFlags = 0x0f;
    private const int MaximumWalkFrames = 256;
    private const int MaximumVariableCount = 256;
    private const int MaximumLocationCount = 16;
    private const int MaximumNameCharacters = 512;
    private const uint MemoryLocationFlag = 0;
    private const uint RegisterLocationFlag = 1;
    private const string ReceiverArgumentName = "this";
    private const string DacLibraryTypeName = "Microsoft.Diagnostics.Runtime.DacLibrary";

    private static readonly StaticFieldV2FrameLocation Unavailable =
        new(StaticFieldV2FrameLocationDisposition.InteropUnavailable, 0, 0);

    private static readonly StaticFieldV2FrameLocation OrdinalAbsent =
        new(StaticFieldV2FrameLocationDisposition.RootOrdinalAbsent, 0, 0);

    /// <summary>Locates one named frame-value root of one selected frame through the legacy data interface.</summary>
    /// <param name="runtime">The opened pinned runtime whose services expose the legacy data library.</param>
    /// <param name="operatingSystemThreadId">The exact operating-system thread identifier to walk.</param>
    /// <param name="instructionPointer">The exact native instruction pointer of the selected frame.</param>
    /// <param name="stackPointer">The exact native stack pointer of the selected frame.</param>
    /// <param name="architecture">The exact target architecture of the pinned snapshot.</param>
    /// <param name="rootKind">The admitted frame-value root family being located.</param>
    /// <param name="parameterOrdinal">The zero-based declared parameter ordinal for a parameter root.</param>
    /// <param name="localSlotIndex">The zero-based local-signature slot index for a local root.</param>
    /// <returns>One classified home carrying an exact address and width only for an exact memory home.</returns>
    internal static StaticFieldV2FrameLocation Locate(
        ClrRuntime runtime,
        uint operatingSystemThreadId,
        ulong instructionPointer,
        ulong stackPointer,
        Architecture architecture,
        StaticFieldV2FrameValueRootKind rootKind,
        int parameterOrdinal,
        int localSlotIndex)
    {
        object? process = null;
        object? stackWalk = null;
        try
        {
            var servicesField = typeof(ClrRuntime).GetField(
                "_services",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var dacLibraryType = typeof(ClrRuntime).Assembly.GetType(DacLibraryTypeName);
            if (servicesField?.GetValue(runtime) is not IServiceProvider services ||
                dacLibraryType is null ||
                services.GetService(dacLibraryType) is not { } library)
            {
                return Unavailable;
            }

            process = dacLibraryType
                .GetMethod("CreateClrDataProcess", BindingFlags.Instance | BindingFlags.Public)
                ?.Invoke(library, null);
            if (process is null)
            {
                return Unavailable;
            }

            stackWalk = process.GetType()
                .GetMethod(
                    "CreateStackWalk",
                    BindingFlags.Instance | BindingFlags.Public,
                    binder: null,
                    [typeof(uint), typeof(uint)],
                    modifiers: null)
                ?.Invoke(process, [operatingSystemThreadId, StackWalkFlags]);
            if (stackWalk is null)
            {
                return Unavailable;
            }

            var self = ReadSelf(stackWalk);
            return self == IntPtr.Zero
                ? Unavailable
                : WalkToFrame(
                    self,
                    instructionPointer,
                    stackPointer,
                    architecture,
                    rootKind,
                    parameterOrdinal,
                    localSlotIndex);
        }
        catch (Exception exception) when (
            exception is TargetInvocationException or InvalidOperationException or ArgumentException or
            MemberAccessException or MarshalDirectiveException or NotSupportedException or COMException or
            EntryPointNotFoundException or BadImageFormatException)
        {
            return Unavailable;
        }
        finally
        {
            (stackWalk as IDisposable)?.Dispose();
            (process as IDisposable)?.Dispose();
        }
    }

    private static StaticFieldV2FrameLocation WalkToFrame(
        IntPtr stackWalk,
        ulong instructionPointer,
        ulong stackPointer,
        Architecture architecture,
        StaticFieldV2FrameValueRootKind rootKind,
        int parameterOrdinal,
        int localSlotIndex)
    {
        var layout = ContextLayout(architecture);
        if (layout.Size == 0)
        {
            return Unavailable;
        }

        var getContext = VtableDelegate<GetContextDelegate>(stackWalk, 0);
        var next = VtableDelegate<NoArgumentDelegate>(stackWalk, 2);
        var getFrame = VtableDelegate<GetInterfaceDelegate>(stackWalk, 5);
        var context = new byte[layout.Size];
        for (var index = 0; index < MaximumWalkFrames; index++)
        {
            Array.Clear(context);
            if (getContext(stackWalk, layout.Flags, context.Length, out var observed, context) !=
                    OperationSucceeded ||
                observed < layout.Size)
            {
                return Unavailable;
            }

            if (ReadContextPointer(context, layout.InstructionPointerOffset, architecture) == instructionPointer &&
                ReadContextPointer(context, layout.StackPointerOffset, architecture) == stackPointer)
            {
                if (getFrame(stackWalk, out var frame) != OperationSucceeded || frame == IntPtr.Zero)
                {
                    return Unavailable;
                }

                try
                {
                    return LocateInFrame(frame, rootKind, parameterOrdinal, localSlotIndex);
                }
                finally
                {
                    Release(frame);
                }
            }

            if (next(stackWalk) != OperationSucceeded)
            {
                return Unavailable;
            }
        }

        return Unavailable;
    }

    private static StaticFieldV2FrameLocation LocateInFrame(
        IntPtr frame,
        StaticFieldV2FrameValueRootKind rootKind,
        int parameterOrdinal,
        int localSlotIndex)
    {
        if (rootKind == StaticFieldV2FrameValueRootKind.Local)
        {
            return !TryReadCount(frame, 5, out var localCount)
                ? Unavailable
                : localSlotIndex >= localCount
                    ? OrdinalAbsent
                    : ObserveVariable(frame, 6, localSlotIndex, readName: false, out _);
        }

        if (!TryReadCount(frame, 3, out var argumentCount))
        {
            return Unavailable;
        }
        if (argumentCount == 0)
        {
            return OrdinalAbsent;
        }

        var receiver = ObserveVariable(frame, 4, 0, readName: true, out var receiverName);
        var hasReceiver = string.Equals(receiverName, ReceiverArgumentName, StringComparison.Ordinal);
        if (rootKind == StaticFieldV2FrameValueRootKind.This)
        {
            return hasReceiver ? receiver : OrdinalAbsent;
        }

        var argumentIndex = checked(parameterOrdinal + (hasReceiver ? 1 : 0));
        return argumentIndex >= argumentCount
            ? OrdinalAbsent
            : argumentIndex == 0
                ? receiver
                : ObserveVariable(frame, 4, argumentIndex, readName: false, out _);
    }

    private static StaticFieldV2FrameLocation ObserveVariable(
        IntPtr frame,
        int methodIndex,
        int variableIndex,
        bool readName,
        out string? name)
    {
        name = null;
        var getVariable = VtableDelegate<GetVariableDelegate>(frame, methodIndex);
        var buffer = readName ? Marshal.AllocHGlobal(MaximumNameCharacters * sizeof(char)) : IntPtr.Zero;
        try
        {
            var hresult = getVariable(
                frame,
                checked((uint)variableIndex),
                out var value,
                readName ? MaximumNameCharacters : 0U,
                out var nameLength,
                buffer);
            if (hresult != OperationSucceeded || value == IntPtr.Zero)
            {
                return OrdinalAbsent;
            }

            try
            {
                if (readName && nameLength is > 1 and <= MaximumNameCharacters)
                {
                    name = Marshal.PtrToStringUni(buffer, checked((int)nameLength) - 1);
                }
                return ObserveValue(value);
            }
            finally
            {
                Release(value);
            }
        }
        finally
        {
            if (buffer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
    }

    private static StaticFieldV2FrameLocation ObserveValue(IntPtr value)
    {
        var getLocationCount = VtableDelegate<GetUInt32Delegate>(value, 25);
        if (getLocationCount(value, out var locationCount) != OperationSucceeded)
        {
            return Unavailable;
        }
        if (locationCount == 0)
        {
            return new StaticFieldV2FrameLocation(StaticFieldV2FrameLocationDisposition.LocationAbsent, 0, 0);
        }
        if (locationCount > 1)
        {
            return new StaticFieldV2FrameLocation(StaticFieldV2FrameLocationDisposition.LocationSplit, 0, 0);
        }

        var getLocation = VtableDelegate<GetLocationDelegate>(value, 26);
        if (getLocation(value, 0, out var flags, out var argument) != OperationSucceeded)
        {
            return Unavailable;
        }
        if (flags == RegisterLocationFlag)
        {
            return new StaticFieldV2FrameLocation(StaticFieldV2FrameLocationDisposition.RegisterHome, 0, 0);
        }
        if (flags != MemoryLocationFlag || argument == 0)
        {
            return new StaticFieldV2FrameLocation(StaticFieldV2FrameLocationDisposition.LocationUnknown, 0, 0);
        }

        var getSize = VtableDelegate<GetUInt64Delegate>(value, 2);
        if (getSize(value, out var size) != OperationSucceeded)
        {
            return Unavailable;
        }
        return size is 0 or > MaximumRootWidth
            ? new StaticFieldV2FrameLocation(StaticFieldV2FrameLocationDisposition.WidthNotAdmitted, 0, 0)
            : new StaticFieldV2FrameLocation(
                StaticFieldV2FrameLocationDisposition.Exact,
                argument,
                checked((int)size));
    }

    private static bool TryReadCount(IntPtr owner, int methodIndex, out int count)
    {
        var getCount = VtableDelegate<GetUInt32Delegate>(owner, methodIndex);
        count = 0;
        if (getCount(owner, out var observed) != OperationSucceeded || observed > MaximumVariableCount)
        {
            return false;
        }

        count = checked((int)observed);
        return true;
    }

    private static (int Size, uint Flags, int InstructionPointerOffset, int StackPointerOffset) ContextLayout(
        Architecture architecture) => architecture switch
        {
            Architecture.X64 => (1232, 0x0010_003fU, 248, 152),
            Architecture.X86 => (716, 0x0001_003fU, 184, 196),
            Architecture.Arm => (416, 0U, 64, 56),
            Architecture.Arm64 => (912, 0U, 264, 256),
            _ => default,
        };

    private static ulong ReadContextPointer(byte[] context, int offset, Architecture architecture) =>
        architecture is Architecture.X86 or Architecture.Arm
            ? BinaryPrimitives.ReadUInt32LittleEndian(context.AsSpan(offset))
            : BinaryPrimitives.ReadUInt64LittleEndian(context.AsSpan(offset));

    private static IntPtr ReadSelf(object wrapper)
    {
        for (var type = wrapper.GetType(); type is not null; type = type.BaseType)
        {
            var property = type.GetProperty(
                "Self",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly);
            if (property?.GetValue(wrapper) is IntPtr value)
            {
                return value;
            }
        }

        return IntPtr.Zero;
    }

    private static T VtableDelegate<T>(IntPtr self, int interfaceMethodIndex)
        where T : Delegate
    {
        if (self == IntPtr.Zero)
        {
            throw new InvalidOperationException("A nonzero legacy interface pointer is required.");
        }

        var vtable = Marshal.ReadIntPtr(self);
        if (vtable == IntPtr.Zero)
        {
            throw new InvalidOperationException("The legacy interface exposes no method table.");
        }

        var method = Marshal.ReadIntPtr(vtable, checked((3 + interfaceMethodIndex) * IntPtr.Size));
        return method == IntPtr.Zero
            ? throw new InvalidOperationException("The selected legacy interface method pointer was zero.")
            : Marshal.GetDelegateForFunctionPointer<T>(method);
    }

    private static void Release(IntPtr self)
    {
        if (self == IntPtr.Zero)
        {
            return;
        }

        var vtable = Marshal.ReadIntPtr(self);
        if (vtable == IntPtr.Zero)
        {
            return;
        }

        var method = Marshal.ReadIntPtr(vtable, checked(2 * IntPtr.Size));
        if (method != IntPtr.Zero)
        {
            _ = Marshal.GetDelegateForFunctionPointer<NoArgumentDelegate>(method)(self);
        }
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate int GetContextDelegate(
        IntPtr self,
        uint flags,
        int bufferSize,
        out int actualSize,
        [Out] byte[] buffer);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate int NoArgumentDelegate(IntPtr self);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate int GetInterfaceDelegate(IntPtr self, out IntPtr value);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate int GetVariableDelegate(
        IntPtr self,
        uint index,
        out IntPtr value,
        uint bufferLength,
        out uint nameLength,
        IntPtr nameBuffer);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate int GetUInt32Delegate(IntPtr self, out uint value);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate int GetUInt64Delegate(IntPtr self, out ulong value);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate int GetLocationDelegate(IntPtr self, uint index, out uint flags, out ulong argument);
}

/// <summary>Freezes every diagnostic code this runtime draft acquisition can retain outside a typed outcome.</summary>
/// <remarks>The codes are prefix-free and stable so a consumer can branch on them without parsing any message.</remarks>
public static class StaticFieldV2RuntimeAcquisitionCodes
{
    /// <summary>Gets the frozen code for a snapshot exposing other than exactly one managed runtime.</summary>
    public const string RuntimeCardinalityCode = "W8_RUNTIME_ACQUISITION_RUNTIME_CARDINALITY";

    /// <summary>Gets the frozen code for a snapshot exposing more modules than the declared draft cap admits.</summary>
    public const string ModuleCountBoundCode = "W8_RUNTIME_ACQUISITION_MODULE_COUNT_BOUND";

    /// <summary>Gets the frozen code for an unreadable or non-conforming contract descriptor.</summary>
    public const string DescriptorUnavailableCode = "W8_RUNTIME_ACQUISITION_DESCRIPTOR_UNAVAILABLE";

    /// <summary>Gets the frozen code for an unreadable available-type draft table.</summary>
    public const string AvailableTypeTableUnreadableCode = "W8_RUNTIME_ACQUISITION_AVAILABLE_TYPES_UNREADABLE";

    /// <summary>Gets the frozen code for a module whose complete metadata image cannot be copied exactly.</summary>
    public const string ModuleMetadataUnavailableCode = "W8_RUNTIME_ACQUISITION_MODULE_METADATA_UNAVAILABLE";

    /// <summary>Gets the frozen code for an absent runtime-module draft binding.</summary>
    public const string ModuleBindingAbsentCode = "W8_RUNTIME_ACQUISITION_MODULE_BINDING_ABSENT";

    /// <summary>Gets the frozen code for a counted snapshot read that could not be completed.</summary>
    public const string SnapshotReadFailedCode = "W8_RUNTIME_ACQUISITION_SNAPSHOT_READ_FAILED";
}

/// <summary>Reports one physical draft acquisition fault that prevents any typed outcome from being issued.</summary>
/// <remarks>
/// The exception is reserved for faults observed before a request exists, such as opening a snapshot whose runtime
/// contract descriptor is absent. Every fault observed while serving a request becomes a typed draft stop instead.
/// </remarks>
public sealed class StaticFieldV2RuntimeAcquisitionException : InvalidOperationException
{
    /// <summary>Initializes one physical draft acquisition fault.</summary>
    /// <param name="code">The frozen prefix-free diagnostic code of the fault.</param>
    /// <param name="message">The human-readable draft description of the fault.</param>
    public StaticFieldV2RuntimeAcquisitionException(string code, string message)
        : base(message) => Code = code;

    /// <summary>Initializes one physical draft acquisition fault wrapping an underlying fault.</summary>
    /// <param name="code">The frozen prefix-free diagnostic code of the fault.</param>
    /// <param name="message">The human-readable draft description of the fault.</param>
    /// <param name="innerException">The underlying fault observed by the reader seam.</param>
    public StaticFieldV2RuntimeAcquisitionException(string code, string message, Exception innerException)
        : base(message, innerException) => Code = code;

    /// <summary>Gets the frozen prefix-free diagnostic draft code of this fault.</summary>
    public string Code { get; }
}
