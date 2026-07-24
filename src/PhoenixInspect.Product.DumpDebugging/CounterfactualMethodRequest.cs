using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using PhoenixInspect.Core.Abstractions;
using PhoenixInspect.Core.Execution;

namespace PhoenixInspect.Product.DumpDebugging;

/// <summary>Classifies one immutable rooted counterfactual activation input.</summary>
public enum CounterfactualInputEvidenceKind
{
    /// <summary>An exact non-null reference selected as the receiver.</summary>
    ExactNonNullReceiver = 1,

    /// <summary>An exact CLI <see cref="int"/> argument.</summary>
    ExactInt32 = 2,

    /// <summary>An explicitly partial or unavailable CLI <see cref="int"/> argument.</summary>
    ExplainedUnknownInt32 = 3,
}

/// <summary>Freezes one typed, content-identified receiver or argument observation.</summary>
public sealed class CounterfactualInputEvidence
{
    private CounterfactualInputEvidence(
        CounterfactualInputEvidenceKind kind,
        TypeSig staticType,
        string evidenceId,
        string evidenceSha256,
        EvaluationEvidenceStatus status,
        int? exactInt32)
    {
        Kind = kind;
        StaticType = staticType;
        EvidenceId = CounterfactualCanonical.ValidateEvidenceIdentity(evidenceId, nameof(evidenceId));
        EvidenceSha256 = CounterfactualCanonical.ValidateSha256(evidenceSha256, nameof(evidenceSha256));
        EvidenceStatus = status;
        ExactInt32 = exactInt32;
    }

    /// <summary>Gets the closed receiver or argument evidence category.</summary>
    public CounterfactualInputEvidenceKind Kind { get; }

    /// <summary>Gets the exact structural static type.</summary>
    public TypeSig StaticType { get; }

    /// <summary>Gets the stable observation identity.</summary>
    public string EvidenceId { get; }

    /// <summary>Gets the complete lowercase content digest of the observation.</summary>
    public string EvidenceSha256 { get; }

    /// <summary>Gets the exact, partial, or unavailable evidence classification.</summary>
    public EvaluationEvidenceStatus EvidenceStatus { get; }

    /// <summary>Gets the exact integer payload when <see cref="Kind"/> is <see cref="CounterfactualInputEvidenceKind.ExactInt32"/>.</summary>
    public int? ExactInt32 { get; }

    /// <summary>Creates exact evidence for a non-null metadata-defined receiver.</summary>
    /// <param name="receiverType">The exact receiver TypeDef.</param>
    /// <param name="evidenceId">The stable observation identity.</param>
    /// <param name="evidenceSha256">The complete observation digest.</param>
    /// <returns>Evidence whose public vocabulary cannot represent a typed-null receiver.</returns>
    public static CounterfactualInputEvidence CreateExactNonNullReceiver(
        TypeSig receiverType,
        string evidenceId,
        string evidenceSha256)
    {
        ArgumentNullException.ThrowIfNull(receiverType);
        if (!receiverType.IsMetadataTypeDefinition)
        {
            throw new ArgumentException("A rooted receiver requires one exact metadata TypeDef.", nameof(receiverType));
        }

        return new(
            CounterfactualInputEvidenceKind.ExactNonNullReceiver,
            receiverType,
            evidenceId,
            evidenceSha256,
            EvaluationEvidenceStatus.Exact,
            null);
    }

    /// <summary>Creates one exact CLI <see cref="int"/> argument observation.</summary>
    /// <param name="evidenceId">The stable observation identity.</param>
    /// <param name="evidenceSha256">The complete observation digest.</param>
    /// <param name="value">The exact argument value.</param>
    /// <returns>One immutable exact argument.</returns>
    public static CounterfactualInputEvidence CreateExactInt32(
        string evidenceId,
        string evidenceSha256,
        int value) =>
        new(
            CounterfactualInputEvidenceKind.ExactInt32,
            TypeSig.Int32,
            evidenceId,
            evidenceSha256,
            EvaluationEvidenceStatus.Exact,
            value);

    /// <summary>Creates one explained partial or unavailable CLI <see cref="int"/> argument observation.</summary>
    /// <param name="evidenceId">The stable observation identity.</param>
    /// <param name="evidenceSha256">The complete observation digest.</param>
    /// <param name="status"><see cref="EvaluationEvidenceStatus.Partial"/> or <see cref="EvaluationEvidenceStatus.Unavailable"/>.</param>
    /// <returns>One immutable value-free argument observation.</returns>
    public static CounterfactualInputEvidence CreateUnknownInt32(
        string evidenceId,
        string evidenceSha256,
        EvaluationEvidenceStatus status)
    {
        if (status is not (EvaluationEvidenceStatus.Partial or EvaluationEvidenceStatus.Unavailable))
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }

        return new(
            CounterfactualInputEvidenceKind.ExplainedUnknownInt32,
            TypeSig.Int32,
            evidenceId,
            evidenceSha256,
            status,
            null);
    }

    internal void WriteCanonical(CounterfactualCanonicalWriter writer)
    {
        writer.WriteInt32(CounterfactualCanonicalTags.Tag(Kind));
        writer.WriteType(StaticType);
        writer.WriteString(EvidenceId);
        writer.WriteDigest(EvidenceSha256);
        writer.WriteInt32(CounterfactualCanonicalTags.Tag(EvidenceStatus));
        writer.WriteBoolean(ExactInt32.HasValue);
        if (ExactInt32 is { } value)
        {
            writer.WriteInt32(value);
        }
    }
}

/// <summary>
/// Freezes one bounded W4.8 rooted-method request independently of graph preparation or execution.
/// </summary>
/// <remarks>
/// Public W4.8 issuance is synthetic-only and cannot express typed null, caller-authored frame shape, or local state.
/// The canonical evidence envelope nevertheless distinguishes synthetic and dump-snapshot evidence so adding the
/// validated dump boundary does not require a request-schema redesign.
/// </remarks>
public sealed class CounterfactualMethodRequest
{
    /// <summary>Gets the canonical request schema version.</summary>
    public const int CanonicalSchemaVersion = 1;

    /// <summary>Gets the hard logical-depth ceiling accepted by the implementation request.</summary>
    public const int MaximumLogicalCallDepth = 64;

    /// <summary>
    /// Gets the largest explicit root-argument vector admitted alongside the required receiver. The combined vector
    /// therefore fits the current W3/W4 machine's 1,024-slot activation ceiling.
    /// </summary>
    public const int MaximumExplicitRootArguments = 1_023;

    /// <summary>Gets the hard preparation-traversal ceiling accepted by the implementation request.</summary>
    public const int MaximumTraversalUnits = MethodGraphPlanner.MaximumConfigurableTraversalUnits;

    private readonly ImmutableArray<CounterfactualInputEvidence> arguments;
    private readonly ImmutableArray<string> assumptions;
    private readonly ImmutableArray<byte> canonicalBytes;

    private CounterfactualMethodRequest(
        EvaluationEvidenceSourceKind evidenceSource,
        string? syntheticEvidenceId,
        EvaluationEvidenceIdentity snapshotIdentity,
        EvaluationEvidenceIdentity moduleIdentity,
        string rootSelectionId,
        string rootEvidenceSha256,
        MethodHandle rootMethod,
        CounterfactualInputEvidence receiver,
        ImmutableArray<CounterfactualInputEvidence> arguments,
        string policyId,
        PureCallModelVersion policyVersion,
        long instructionLimit,
        long lineageNodeCeiling,
        int logicalDepthLimit,
        int traversalLimit,
        string modelCatalogId,
        PureCallModelVersion modelCatalogVersion,
        MethodHandle? requiredModelTarget,
        ImmutableArray<string> assumptions)
    {
        SchemaVersion = CanonicalSchemaVersion;
        EvidenceSource = evidenceSource;
        SyntheticEvidenceId = syntheticEvidenceId;
        SnapshotIdentity = snapshotIdentity;
        ModuleIdentity = moduleIdentity;
        RootSelectionId = rootSelectionId;
        RootEvidenceSha256 = rootEvidenceSha256;
        RootMethod = rootMethod;
        Receiver = receiver;
        this.arguments = arguments;
        PolicyId = policyId;
        PolicyVersion = policyVersion;
        InstructionLimit = instructionLimit;
        LineageNodeCeiling = lineageNodeCeiling;
        LogicalDepthLimit = logicalDepthLimit;
        TraversalLimit = traversalLimit;
        ModelCatalogId = modelCatalogId;
        ModelCatalogVersion = modelCatalogVersion;
        RequiredModelTarget = requiredModelTarget;
        this.assumptions = assumptions;
        canonicalBytes = EncodeCanonical();
        Sha256 = CounterfactualCanonical.Hash(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the request canonical schema version.</summary>
    public int SchemaVersion { get; }

    /// <summary>Gets the explicit top-level evidence-source discriminator.</summary>
    public EvaluationEvidenceSourceKind EvidenceSource { get; }

    /// <summary>
    /// Gets the stable synthetic fixture identity, or <see langword="null"/> when the evidence source is not synthetic.
    /// </summary>
    public string? SyntheticEvidenceId { get; }

    /// <summary>Gets the explicit snapshot identity outcome for the evidence source.</summary>
    public EvaluationEvidenceIdentity SnapshotIdentity { get; }

    /// <summary>Gets the explicit module identity outcome for the evidence source.</summary>
    public EvaluationEvidenceIdentity ModuleIdentity { get; }

    /// <summary>Gets the exact non-null rooted-object selection identity.</summary>
    public string RootSelectionId { get; }

    /// <summary>Gets the complete rooted-object evidence digest.</summary>
    public string RootEvidenceSha256 { get; }

    /// <summary>Gets the exact root MethodDef and module identity.</summary>
    public MethodHandle RootMethod { get; }

    /// <summary>Gets the exact non-null receiver evidence.</summary>
    public CounterfactualInputEvidence Receiver { get; }

    /// <summary>Gets a defensive copy of explicit arguments in metadata order.</summary>
    public ImmutableArray<CounterfactualInputEvidence> Arguments => CounterfactualCanonical.Copy(arguments);

    /// <summary>Gets the stable policy identity.</summary>
    public string PolicyId { get; }

    /// <summary>Gets the exact policy semantic version.</summary>
    public PureCallModelVersion PolicyVersion { get; }

    /// <summary>Gets the applied instruction-unit limit; zero is valid.</summary>
    public long InstructionLimit { get; }

    /// <summary>
    /// Gets the derived maximum lineage-node count: traversal units plus rooted inputs plus three transforms per
    /// instruction. This is not an independently configurable budget.
    /// </summary>
    public long LineageNodeCeiling { get; }

    /// <summary>Gets the applied maximum logical depth; zero is valid and prevents preparation.</summary>
    public int LogicalDepthLimit { get; }

    /// <summary>Gets the applied preparation-traversal limit; zero is valid.</summary>
    public int TraversalLimit { get; }

    /// <summary>Gets the stable model-catalog identity.</summary>
    public string ModelCatalogId { get; }

    /// <summary>Gets the exact model-catalog semantic version.</summary>
    public PureCallModelVersion ModelCatalogVersion { get; }

    /// <summary>Gets the structural target that must use a pure model, or null for interpreted preparation.</summary>
    public MethodHandle? RequiredModelTarget { get; }

    /// <summary>Gets a defensive copy of ordered explicit assumptions.</summary>
    public ImmutableArray<string> Assumptions => CounterfactualCanonical.Copy(assumptions);

    /// <summary>Gets a defensive copy of the canonical schema-v1 bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => CounterfactualCanonical.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 fingerprint of <see cref="CanonicalBytes"/>.</summary>
    public string Sha256 { get; }

    /// <summary>Creates the only W4.8 request source currently admitted: a deterministic synthetic rooted fixture.</summary>
    /// <param name="syntheticEvidenceId">Stable synthetic evidence identity.</param>
    /// <param name="rootSelectionId">Stable exact non-null root-selection identity.</param>
    /// <param name="rootEvidenceSha256">Complete root evidence digest.</param>
    /// <param name="rootMethod">Exact root MethodDef.</param>
    /// <param name="receiver">Exact non-null receiver evidence.</param>
    /// <param name="arguments">Ordered explicit argument evidence.</param>
    /// <param name="policyId">Stable immutable policy identity.</param>
    /// <param name="policyVersion">Exact policy version.</param>
    /// <param name="instructionLimit">Instruction-unit limit, including zero.</param>
    /// <param name="logicalDepthLimit">Maximum logical call depth, including zero.</param>
    /// <param name="traversalLimit">Preparation traversal-unit limit, including zero.</param>
    /// <param name="modelCatalogId">Stable model-catalog identity.</param>
    /// <param name="modelCatalogVersion">Exact catalog version.</param>
    /// <param name="requiredModelTarget">Optional same-module target that must be modeled.</param>
    /// <param name="assumptions">Ordered stable assumption identities.</param>
    /// <returns>A validated immutable request with canonical identity.</returns>
    public static CounterfactualMethodRequest CreateSynthetic(
        string syntheticEvidenceId,
        string rootSelectionId,
        string rootEvidenceSha256,
        MethodHandle rootMethod,
        CounterfactualInputEvidence receiver,
        ImmutableArray<CounterfactualInputEvidence> arguments,
        string policyId,
        PureCallModelVersion policyVersion,
        long instructionLimit,
        int logicalDepthLimit,
        int traversalLimit,
        string modelCatalogId,
        PureCallModelVersion modelCatalogVersion,
        MethodHandle? requiredModelTarget,
        ImmutableArray<string> assumptions)
    {
        return CreateValidated(
            EvaluationEvidenceSourceKind.Synthetic,
            syntheticEvidenceId,
            EvaluationEvidenceIdentity.NotApplicable,
            EvaluationEvidenceIdentity.NotApplicable,
            rootSelectionId,
            rootEvidenceSha256,
            rootMethod,
            receiver,
            arguments,
            policyId,
            policyVersion,
            instructionLimit,
            logicalDepthLimit,
            traversalLimit,
            modelCatalogId,
            modelCatalogVersion,
            requiredModelTarget,
            assumptions);
    }

    internal static CounterfactualMethodRequest CreateValidated(
        EvaluationEvidenceSourceKind evidenceSource,
        string? syntheticEvidenceId,
        EvaluationEvidenceIdentity snapshotIdentity,
        EvaluationEvidenceIdentity moduleIdentity,
        string rootSelectionId,
        string rootEvidenceSha256,
        MethodHandle rootMethod,
        CounterfactualInputEvidence receiver,
        ImmutableArray<CounterfactualInputEvidence> arguments,
        string policyId,
        PureCallModelVersion policyVersion,
        long instructionLimit,
        int logicalDepthLimit,
        int traversalLimit,
        string modelCatalogId,
        PureCallModelVersion modelCatalogVersion,
        MethodHandle? requiredModelTarget,
        ImmutableArray<string> assumptions)
    {
        ArgumentNullException.ThrowIfNull(snapshotIdentity);
        ArgumentNullException.ThrowIfNull(moduleIdentity);
        var synthetic = ValidateEvidenceEnvelope(
            evidenceSource,
            syntheticEvidenceId,
            snapshotIdentity,
            moduleIdentity);
        var selection = CounterfactualCanonical.ValidateEvidenceIdentity(rootSelectionId, nameof(rootSelectionId));
        var digest = CounterfactualCanonical.ValidateSha256(rootEvidenceSha256, nameof(rootEvidenceSha256));
        if (rootMethod == default)
        {
            throw new ArgumentException("A request requires an exact non-default root MethodDef.", nameof(rootMethod));
        }

        ArgumentNullException.ThrowIfNull(receiver);
        if (receiver.Kind != CounterfactualInputEvidenceKind.ExactNonNullReceiver ||
            receiver.StaticType.Module != rootMethod.Module)
        {
            throw new ArgumentException("The root requires exact non-null same-module receiver evidence.", nameof(receiver));
        }

        var copiedArguments = ValidateInputs(arguments, nameof(arguments));
        CounterfactualCanonical.ValidateNonNegative(instructionLimit, nameof(instructionLimit));
        CounterfactualCanonical.ValidateBound(logicalDepthLimit, MaximumLogicalCallDepth, nameof(logicalDepthLimit));
        CounterfactualCanonical.ValidateBound(traversalLimit, MaximumTraversalUnits, nameof(traversalLimit));
        if (requiredModelTarget is { } target &&
            (target == default || target == rootMethod || target.Module != rootMethod.Module))
        {
            throw new ArgumentException(
                "A required model target must be a non-default, non-root, same-module MethodDef.",
                nameof(requiredModelTarget));
        }

        var lineageNodeCeiling = CalculateLineageNodeCeiling(
            instructionLimit,
            traversalLimit,
            copiedArguments.Length + 1);

        var copiedAssumptions = assumptions.IsDefault
            ? throw new ArgumentException("Assumptions must be initialized.", nameof(assumptions))
            : CounterfactualCanonical.Copy(assumptions);
        if (copiedAssumptions.Length > 32)
        {
            throw new ArgumentOutOfRangeException(nameof(assumptions));
        }

        var distinctAssumptions = new HashSet<string>(StringComparer.Ordinal);
        foreach (var assumption in copiedAssumptions)
        {
            CounterfactualCanonical.ValidateName(assumption, nameof(assumptions));
            if (!distinctAssumptions.Add(assumption))
            {
                throw new ArgumentException("Assumption identities must be unique.", nameof(assumptions));
            }
        }

        return new(
            evidenceSource,
            synthetic,
            snapshotIdentity,
            moduleIdentity,
            selection,
            digest,
            rootMethod,
            receiver,
            copiedArguments,
            CounterfactualCanonical.ValidateName(policyId, nameof(policyId)),
            policyVersion,
            instructionLimit,
            lineageNodeCeiling,
            logicalDepthLimit,
            traversalLimit,
            CounterfactualCanonical.ValidateName(modelCatalogId, nameof(modelCatalogId)),
            modelCatalogVersion,
            requiredModelTarget,
            copiedAssumptions);
    }

    private static string? ValidateEvidenceEnvelope(
        EvaluationEvidenceSourceKind evidenceSource,
        string? syntheticEvidenceId,
        EvaluationEvidenceIdentity snapshotIdentity,
        EvaluationEvidenceIdentity moduleIdentity)
    {
        if (evidenceSource == EvaluationEvidenceSourceKind.Synthetic)
        {
            if (snapshotIdentity.Availability != EvaluationIdentityAvailability.NotApplicable ||
                moduleIdentity.Availability != EvaluationIdentityAvailability.NotApplicable)
            {
                throw new ArgumentException(
                    "Synthetic evidence requires inapplicable snapshot and module identities.",
                    nameof(evidenceSource));
            }

            return CounterfactualCanonical.ValidateEvidenceIdentity(syntheticEvidenceId!, nameof(syntheticEvidenceId));
        }

        if (evidenceSource == EvaluationEvidenceSourceKind.DumpSnapshot)
        {
            if (syntheticEvidenceId is not null ||
                snapshotIdentity.Availability != EvaluationIdentityAvailability.Available ||
                moduleIdentity.Availability != EvaluationIdentityAvailability.Available)
            {
                throw new ArgumentException(
                    "Dump evidence requires available snapshot and module identities and no synthetic identity.",
                    nameof(evidenceSource));
            }

            return null;
        }

        throw new ArgumentOutOfRangeException(nameof(evidenceSource));
    }

    private static long CalculateLineageNodeCeiling(
        long instructionLimit,
        int traversalLimit,
        int rootedInputCount)
    {
        try
        {
            return checked(traversalLimit + rootedInputCount + (3 * instructionLimit));
        }
        catch (OverflowException)
        {
            throw new ArgumentOutOfRangeException(
                nameof(instructionLimit),
                instructionLimit,
                "The derived lineage-node ceiling must fit a signed 64-bit count.");
        }
    }

    private static ImmutableArray<CounterfactualInputEvidence> ValidateInputs(
        ImmutableArray<CounterfactualInputEvidence> values,
        string parameterName)
    {
        if (values.IsDefault || values.Length > MaximumExplicitRootArguments || values.Any(static value => value is null))
        {
            throw new ArgumentException("Arguments require an initialized bounded non-null vector.", parameterName);
        }

        if (values.Any(static value => value.Kind == CounterfactualInputEvidenceKind.ExactNonNullReceiver || value.StaticType != TypeSig.Int32))
        {
            throw new ArgumentException("Explicit arguments must be exact or explained-unknown Int32 evidence.", parameterName);
        }

        return CounterfactualCanonical.Copy(values);
    }

    private ImmutableArray<byte> EncodeCanonical()
    {
        var writer = new CounterfactualCanonicalWriter();
        writer.WriteString("PhoenixInspect.CounterfactualMethodRequest");
        writer.WriteInt32(SchemaVersion);
        writer.WriteInt32(CounterfactualCanonicalTags.Tag(EvidenceSource));
        writer.WriteBoolean(SyntheticEvidenceId is not null);
        if (SyntheticEvidenceId is { } syntheticEvidenceId)
        {
            writer.WriteString(syntheticEvidenceId);
        }

        writer.WriteEvidenceIdentity(SnapshotIdentity);
        writer.WriteEvidenceIdentity(ModuleIdentity);
        writer.WriteString(RootSelectionId);
        writer.WriteDigest(RootEvidenceSha256);
        writer.WriteMethod(RootMethod);
        Receiver.WriteCanonical(writer);
        writer.WriteInt32(arguments.Length);
        foreach (var argument in arguments)
        {
            argument.WriteCanonical(writer);
        }

        writer.WriteString(PolicyId);
        writer.WriteVersion(PolicyVersion);
        writer.WriteInt64(InstructionLimit);
        writer.WriteInt64(LineageNodeCeiling);
        writer.WriteInt32(LogicalDepthLimit);
        writer.WriteInt32(TraversalLimit);
        writer.WriteString(ModelCatalogId);
        writer.WriteVersion(ModelCatalogVersion);
        writer.WriteBoolean(RequiredModelTarget.HasValue);
        if (RequiredModelTarget is { } target)
        {
            writer.WriteMethod(target);
        }

        writer.WriteInt32(assumptions.Length);
        foreach (var assumption in assumptions)
        {
            writer.WriteString(assumption);
        }

        return writer.ToImmutableArray();
    }
}

internal static class CounterfactualCanonical
{
    internal static ImmutableArray<T> Copy<T>(ImmutableArray<T> values) =>
        values.IsDefaultOrEmpty ? ImmutableArray<T>.Empty : ImmutableArray.CreateRange(values.AsSpan().ToArray());

    internal static string Hash(ReadOnlySpan<byte> value) =>
        Convert.ToHexString(SHA256.HashData(value)).ToLowerInvariant();

    internal static string ValidateName(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 128 ||
            value[0] is < 'a' or > 'z' ||
            value.Any(static c => c is not (>= 'a' and <= 'z') and not (>= '0' and <= '9') and not '.' and not '-' and not '_'))
        {
            throw new ArgumentException("A bounded canonical lowercase identity is required.", parameterName);
        }

        return value;
    }

    internal static string ValidateEvidenceIdentity(string value, string parameterName)
    {
        try
        {
            return EvaluationEvidenceIdentity.CreateAvailable(value).SourceId!;
        }
        catch (ArgumentException exception)
        {
            throw new ArgumentException("A bounded path-independent evidence identity is required.", parameterName, exception);
        }
    }

    internal static string ValidateSha256(string value, string parameterName)
    {
        if (value?.Length != 64 || !value.All(Uri.IsHexDigit))
        {
            throw new ArgumentException("A complete SHA-256 digest is required.", parameterName);
        }

        return value.ToLowerInvariant();
    }

    internal static void ValidateBound(long value, long maximum, string parameterName)
    {
        if (value < 0 || value > maximum)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }

    internal static void ValidateNonNegative(long value, string parameterName)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }
}

internal sealed class CounterfactualCanonicalWriter
{
    private readonly ArrayBufferWriter<byte> buffer = new();

    internal void WriteBoolean(bool value) => WriteInt32(value ? 1 : 0);

    internal void WriteInt32(int value)
    {
        var span = buffer.GetSpan(sizeof(int));
        BinaryPrimitives.WriteInt32BigEndian(span, value);
        buffer.Advance(sizeof(int));
    }

    internal void WriteInt64(long value)
    {
        var span = buffer.GetSpan(sizeof(long));
        BinaryPrimitives.WriteInt64BigEndian(span, value);
        buffer.Advance(sizeof(long));
    }

    internal void WriteUInt64(ulong value)
    {
        var span = buffer.GetSpan(sizeof(ulong));
        BinaryPrimitives.WriteUInt64BigEndian(span, value);
        buffer.Advance(sizeof(ulong));
    }

    internal void WriteString(string value) => WriteBytes(Encoding.UTF8.GetBytes(value));

    internal void WriteDigest(string value) => WriteRaw(Convert.FromHexString(value));

    internal void WriteBytes(ReadOnlySpan<byte> value)
    {
        WriteInt32(value.Length);
        WriteRaw(value);
    }

    internal void WriteMethod(MethodHandle value)
    {
        WriteUInt64(value.Module.High);
        WriteUInt64(value.Module.Low);
        WriteInt32(value.MetadataToken);
    }

    internal void WriteField(FieldHandle value)
    {
        WriteUInt64(value.Module.High);
        WriteUInt64(value.Module.Low);
        WriteInt32(value.MetadataToken);
    }

    internal void WriteVersion(PureCallModelVersion value)
    {
        WriteInt32(value.Major);
        WriteInt32(value.Minor);
        WriteInt32(value.Patch);
    }

    internal void WriteEvidenceIdentity(EvaluationEvidenceIdentity value)
    {
        WriteInt32(CounterfactualCanonicalTags.Tag(value.Availability));
        WriteBoolean(value.SourceId is not null);
        if (value.SourceId is { } sourceId)
        {
            WriteString(sourceId);
        }
    }

    internal void WriteType(TypeSig value)
    {
        WriteInt32(CounterfactualCanonicalTags.Tag(value.Kind));
        switch (value.Kind)
        {
            case TypeSigKind.Intrinsic:
                WriteInt32(CounterfactualCanonicalTags.Tag(value.IntrinsicKind!.Value));
                break;
            case TypeSigKind.TypeDefinition:
                WriteUInt64(value.Module!.Value.High);
                WriteUInt64(value.Module.Value.Low);
                WriteInt32(value.MetadataToken);
                break;
            case TypeSigKind.Synthetic:
                WriteString(value.DisplayName);
                break;
            case TypeSigKind.SzArray:
                WriteType(value.ElementType!);
                break;
        }
    }

    internal ImmutableArray<byte> ToImmutableArray() =>
        ImmutableArray.CreateRange(buffer.WrittenSpan.ToArray());

    private void WriteRaw(ReadOnlySpan<byte> value)
    {
        value.CopyTo(buffer.GetSpan(value.Length));
        buffer.Advance(value.Length);
    }
}

internal static class CounterfactualCanonicalTags
{
    internal static int Tag(CounterfactualInputEvidenceKind value) => value switch
    {
        CounterfactualInputEvidenceKind.ExactNonNullReceiver => 1,
        CounterfactualInputEvidenceKind.ExactInt32 => 2,
        CounterfactualInputEvidenceKind.ExplainedUnknownInt32 => 3,
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    internal static int Tag(EvaluationEvidenceStatus value) => value switch
    {
        EvaluationEvidenceStatus.Exact => 1,
        EvaluationEvidenceStatus.Partial => 2,
        EvaluationEvidenceStatus.Unavailable => 3,
        EvaluationEvidenceStatus.Conflict => 4,
        EvaluationEvidenceStatus.Invalid => 5,
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    internal static int Tag(EvaluationEvidenceSourceKind value) => value switch
    {
        EvaluationEvidenceSourceKind.Synthetic => 1,
        EvaluationEvidenceSourceKind.DumpSnapshot => 2,
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    internal static int Tag(EvaluationIdentityAvailability value) => value switch
    {
        EvaluationIdentityAvailability.NotApplicable => 1,
        EvaluationIdentityAvailability.Available => 2,
        EvaluationIdentityAvailability.Unavailable => 3,
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    internal static int Tag(TypeSigKind value) => value switch
    {
        TypeSigKind.Void => 1,
        TypeSigKind.Intrinsic => 2,
        TypeSigKind.TypeDefinition => 3,
        TypeSigKind.Synthetic => 4,
        TypeSigKind.SzArray => 5,
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    internal static int Tag(IntrinsicTypeKind value) => value switch
    {
        IntrinsicTypeKind.Boolean => 1,
        IntrinsicTypeKind.Int32 => 2,
        IntrinsicTypeKind.Int64 => 3,
        IntrinsicTypeKind.String => 4,
        IntrinsicTypeKind.Object => 5,
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    internal static int Tag(MethodCallingConventionKind value) => value switch
    {
        MethodCallingConventionKind.Default => 1,
        MethodCallingConventionKind.VarArgs => 2,
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    internal static int Tag(MachineRunStatus value) => value switch
    {
        MachineRunStatus.Ready => 1,
        MachineRunStatus.Completed => 2,
        MachineRunStatus.BudgetExhausted => 3,
        MachineRunStatus.Blocked => 4,
        MachineRunStatus.InvalidProgram => 5,
        MachineRunStatus.TargetException => 6,
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    internal static int Tag(ExecutionFailureKind value) => value switch
    {
        ExecutionFailureKind.DependencyResolution => 1,
        ExecutionFailureKind.InvalidInstruction => 2,
        ExecutionFailureKind.UnsupportedInstruction => 3,
        ExecutionFailureKind.InvalidStack => 4,
        ExecutionFailureKind.InvalidSlot => 5,
        ExecutionFailureKind.DomainFailure => 6,
        ExecutionFailureKind.MemoryFailure => 7,
        ExecutionFailureKind.ResourceLimit => 8,
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    internal static int Tag(FrozenMethodCallDisposition value) => value switch
    {
        FrozenMethodCallDisposition.Interpreted => 1,
        FrozenMethodCallDisposition.PureModel => 2,
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    internal static int Tag(EvaluationEffectStatus value) => value switch
    {
        EvaluationEffectStatus.None => 1,
        EvaluationEffectStatus.VirtualOnly => 2,
        EvaluationEffectStatus.Modeled => 3,
        EvaluationEffectStatus.Unsupported => 4,
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    internal static int Tag(PureCallModelConfidence value) => value switch
    {
        PureCallModelConfidence.Exact => 1,
        PureCallModelConfidence.BestEffort => 2,
        PureCallModelConfidence.Partial => 3,
        PureCallModelConfidence.UnsupportedLayout => 4,
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    internal static int Tag(MethodGraphTraversalChargeKind value) => value switch
    {
        MethodGraphTraversalChargeKind.InterpretedMethod => 1,
        MethodGraphTraversalChargeKind.FieldDependency => 2,
        MethodGraphTraversalChargeKind.DirectCallEdge => 3,
        MethodGraphTraversalChargeKind.ModeledLeaf => 4,
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };
}
