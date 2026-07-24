using PhoenixInspect.Core.Abstractions;
using PhoenixInspect.Domain.Concrete;

namespace PhoenixInspect.Product.DumpDebugging;

/// <summary>Identifies the product boundary that issued a counterfactual execution result.</summary>
/// <remarks>
/// The origin is part of canonical identity. A rooted facade result carries request and plan facts, while a
/// standalone target-outcome projection deliberately does not manufacture those facts.
/// This W4 vocabulary requires a new schema version before members or meanings change.
/// </remarks>
public enum CounterfactualExecutionOriginKind
{
    /// <summary>The result was issued for one validated rooted request and frozen method plan.</summary>
    RootedFacade = 1,

    /// <summary>The result only wraps an independently certified standalone target-exception fragment.</summary>
    StandaloneTargetOutcome = 2,

    /// <summary>The facade rejected a null or foreign pre-authority input before any request or plan identity existed.</summary>
    FacadeRejection = 3,
}

/// <summary>Classifies the closed value projection carried by a counterfactual execution result.</summary>
/// <remarks>This W4 vocabulary requires a new schema version before members or meanings change.</remarks>
public enum CounterfactualExecutionValueKind
{
    /// <summary>A completed execution returned one exact structural <see cref="int"/>.</summary>
    ExactReturn = 1,

    /// <summary>A completed execution returned one explained unknown structural <see cref="int"/>.</summary>
    UnknownReturn = 2,

    /// <summary>A completed execution terminated at one certified target-exception boundary.</summary>
    TargetException = 3,

    /// <summary>An incomplete execution retains only the fact that a nonempty prefix ran.</summary>
    ExecutionPrefix = 4,
}

/// <summary>
/// Carries one immutable inspect-only value projection without exposing a domain value, machine state, or memory.
/// </summary>
/// <remarks>
/// This is a W4 product union. Exact and unknown returns are currently restricted to structural
/// <see cref="TypeSig.Int32"/>. Target exceptions nest the unchanged W4.7 fragment; an execution prefix has no
/// payload because partially executed machine state is intentionally not a product value.
/// The union remains unstable and requires a schema revision before its admitted shapes change.
/// </remarks>
public sealed class CounterfactualExecutionValue
{
    private CounterfactualExecutionValue(
        CounterfactualExecutionValueKind kind,
        TypeSig? staticType,
        int? exactInt32,
        ProvenanceLineageGraph? lineage,
        CounterfactualTargetOutcomeFragment? targetOutcome)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }

        var valid = kind switch
        {
            CounterfactualExecutionValueKind.ExactReturn =>
                staticType == TypeSig.Int32 && exactInt32.HasValue && lineage is null && targetOutcome is null,
            CounterfactualExecutionValueKind.UnknownReturn =>
                staticType == TypeSig.Int32 && exactInt32 is null && IsCanonicalLineage(lineage) &&
                targetOutcome is null,
            CounterfactualExecutionValueKind.TargetException =>
                staticType is null && exactInt32 is null && lineage is null && IsCanonicalTargetOutcome(targetOutcome),
            CounterfactualExecutionValueKind.ExecutionPrefix =>
                staticType is null && exactInt32 is null && lineage is null && targetOutcome is null,
            _ => false,
        };
        if (!valid)
        {
            throw new ArgumentException("The counterfactual execution value union is inconsistent.");
        }

        Kind = kind;
        StaticType = staticType;
        ExactInt32 = exactInt32;
        Lineage = lineage;
        TargetOutcome = targetOutcome;
    }

    /// <summary>Gets the closed value-union discriminator.</summary>
    public CounterfactualExecutionValueKind Kind { get; }

    /// <summary>Gets the structural return type for exact or unknown returns; otherwise <see langword="null"/>.</summary>
    public TypeSig? StaticType { get; }

    /// <summary>Gets the exact integer return only for <see cref="CounterfactualExecutionValueKind.ExactReturn"/>.</summary>
    public int? ExactInt32 { get; }

    /// <summary>
    /// Gets the canonical reachable explanation graph only for
    /// <see cref="CounterfactualExecutionValueKind.UnknownReturn"/>.
    /// </summary>
    public ProvenanceLineageGraph? Lineage { get; }

    /// <summary>
    /// Gets the unchanged certified fragment only for
    /// <see cref="CounterfactualExecutionValueKind.TargetException"/>.
    /// </summary>
    public CounterfactualTargetOutcomeFragment? TargetOutcome { get; }

    internal static CounterfactualExecutionValue CreateExactInt32(int value) =>
        new(CounterfactualExecutionValueKind.ExactReturn, TypeSig.Int32, value, null, null);

    internal static CounterfactualExecutionValue CreateUnknownInt32(ProvenanceLineageGraph lineage) =>
        new(CounterfactualExecutionValueKind.UnknownReturn, TypeSig.Int32, null, lineage, null);

    internal static CounterfactualExecutionValue CreateTargetOutcome(CounterfactualTargetOutcomeFragment fragment) =>
        new(CounterfactualExecutionValueKind.TargetException, null, null, null, fragment);

    internal static CounterfactualExecutionValue CreateExecutionPrefix() =>
        new(CounterfactualExecutionValueKind.ExecutionPrefix, null, null, null, null);

    internal static bool IsCanonicalLineage(ProvenanceLineageGraph? lineage)
    {
        if (lineage is null || lineage.Root.Sha256 is null || lineage.Nodes.IsDefaultOrEmpty ||
            !lineage.Nodes.Any(node => node is not null && node.Id == lineage.Root))
        {
            return false;
        }

        try
        {
            var replayDomain = new ProvenanceConcreteDomain();
            var replayedValue = replayDomain.ReplayLineage(lineage);
            var recaptured = replayDomain.CaptureLineage(replayedValue);
            var bytes = lineage.CanonicalBytes;
            return recaptured.TryGetNode(recaptured.Root, out var rootNode) &&
                rootNode is not null &&
                rootNode.StaticType == TypeSig.Int32 &&
                !bytes.IsDefaultOrEmpty &&
                bytes.AsSpan().SequenceEqual(recaptured.CanonicalBytes.AsSpan()) &&
                string.Equals(lineage.Sha256, recaptured.Sha256, StringComparison.Ordinal) &&
                string.Equals(CounterfactualCanonical.Hash(bytes.AsSpan()), lineage.Sha256, StringComparison.Ordinal);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    internal static bool IsCanonicalTargetOutcome(CounterfactualTargetOutcomeFragment? fragment)
    {
        if (fragment is null ||
            fragment.SchemaVersion != CounterfactualTargetOutcomeFragment.CanonicalSchemaVersion ||
            fragment.SemanticMode != EvaluationSemanticMode.CounterfactualExecution ||
            fragment.Completion != EvaluationCompletionStatus.Completed ||
            fragment.Completeness != EvaluationCompleteness.Complete ||
            fragment.Evidence != EvaluationEvidenceStatus.Exact ||
            fragment.Effects != EvaluationEffectStatus.None)
        {
            return false;
        }

        try
        {
            var encoded = CounterfactualTargetOutcomeCanonicalCodec.SerializeCanonical(fragment);
            return encoded.AsSpan().SequenceEqual(fragment.CanonicalBytes.AsSpan()) &&
                string.Equals(CounterfactualCanonical.Hash(encoded.AsSpan()), fragment.Sha256, StringComparison.Ordinal);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
