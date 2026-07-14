using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Collections.Immutable;
using Interpreter.Core.Abstractions;
using Interpreter.Host.Dump.ClrMD;

namespace Interpreter.Product.DumpQuery;

/// <summary>Classifies the already-bound field decoder selected by one immutable W2 query plan.</summary>
public enum DumpQueryPlanFieldKind
{
    /// <summary>A non-nullable signed 32-bit integer field.</summary>
    Int32,

    /// <summary>A nullable signed 32-bit integer field whose exact value may be null.</summary>
    NullableInt32,

    /// <summary>A managed string field whose exact reference may be null.</summary>
    String,
}

/// <summary>
/// Represents one immutable, object-specific W2 query whose root and field were selected exactly once before value
/// decoding begins.
/// </summary>
/// <remarks>
/// The plan remains deliberately narrow: one exact non-null root, one direct instance field, and an optional admitted
/// literal coalescing operation. It contains no executable user code. Field addresses and snapshot identity make it
/// specific to the bound dump object; evaluating it against another snapshot fails through adapter identity checks.
/// </remarks>
public sealed class DumpQueryPlan
{
    private const string CanonicalVersion = "dump-query-plan-v1";

    private readonly DumpQueryLiteral? _coalesceLiteral;
    private readonly string _canonicalProjection;

    internal DumpQueryPlan(
        DumpQueryRootBinding rootBinding,
        ClrmdInstanceFieldInfo field,
        DumpQueryPlanFieldKind fieldKind,
        DumpQueryLiteral? coalesceLiteral,
        DumpQueryParserBounds parserBounds,
        ImmutableArray<EvaluationDeterministicBound> fieldSelectionBounds)
    {
        ArgumentNullException.ThrowIfNull(rootBinding);
        ArgumentNullException.ThrowIfNull(field);
        if (rootBinding.Status != DumpQueryRootBindingStatus.ExactObject || rootBinding.Root is null)
        {
            throw new ArgumentException("A bound query plan requires one exact root object.", nameof(rootBinding));
        }

        if (!Enum.IsDefined(fieldKind))
        {
            throw new ArgumentOutOfRangeException(nameof(fieldKind));
        }

        RootBinding = rootBinding;
        Field = field;
        FieldKind = fieldKind;
        _coalesceLiteral = coalesceLiteral;
        ParserBounds = parserBounds;
        FieldSelectionBounds = fieldSelectionBounds.IsDefault
            ? ImmutableArray<EvaluationDeterministicBound>.Empty
            : fieldSelectionBounds;
        SemanticMode = EvaluationSemanticMode.DerivedQuery;
        _canonicalProjection = CreateCanonicalProjection();
    }

    /// <summary>Gets the exact named root binding captured by the plan.</summary>
    public DumpQueryRootBinding RootBinding { get; }

    /// <summary>Gets the exact field descriptor selected during preparation and reused during evaluation.</summary>
    public ClrmdInstanceFieldInfo Field { get; }

    /// <summary>Gets the value decoder admitted for <see cref="Field"/>.</summary>
    public DumpQueryPlanFieldKind FieldKind { get; }

    /// <summary>
    /// Gets the plan-level truth mode. Every product query is a derived query because it applies host root/member
    /// binding over adapter observations; the underlying adapter field reads remain independently classified as
    /// observations.
    /// </summary>
    public EvaluationSemanticMode SemanticMode { get; }

    /// <summary>Gets whether the plan includes an admitted null-coalescing literal.</summary>
    public bool HasCoalesce => _coalesceLiteral is not null;

    /// <summary>
    /// Produces the injective, versioned canonical representation used to identify this object-specific bound plan.
    /// </summary>
    /// <returns>
    /// A length-delimited representation of grammar version, root/snapshot/object identity, selected field descriptor,
    /// value decoder, and complete literal payload.
    /// </returns>
    /// <remarks>
    /// This projection includes target addresses and literal values. It is replay material, not telemetry-safe display
    /// text, and callers should normally compare its fingerprint rather than log the projection.
    /// </remarks>
    public string ToCanonicalReplayProjection() => _canonicalProjection;

    /// <summary>Computes the lowercase SHA-256 fingerprint of <see cref="ToCanonicalReplayProjection"/>.</summary>
    /// <returns>A 64-character lowercase hexadecimal digest.</returns>
    public string ComputeSha256() => Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes(_canonicalProjection))).ToLowerInvariant();

    internal DumpQueryLiteral? CoalesceLiteral => _coalesceLiteral;

    internal DumpQueryParserBounds ParserBounds { get; }

    internal ImmutableArray<EvaluationDeterministicBound> FieldSelectionBounds { get; }

    internal string ProvenanceId => $"dump-query-plan:sha256:{ComputeSha256()}";

    private string CreateCanonicalProjection()
    {
        var root = RootBinding.Root!;
        var builder = new StringBuilder();
        Append(builder, CanonicalVersion);
        Append(builder, RootBinding.Name ?? string.Empty);
        Append(builder, root.Snapshot.Sha256);
        Append(builder, root.Module.Identity.SourceId);
        Append(builder, root.Address.ToString("x16", CultureInfo.InvariantCulture));
        Append(builder, root.MethodTable.ToString("x16", CultureInfo.InvariantCulture));
        Append(builder, Field.Name);
        Append(builder, Field.MetadataToken.ToString(CultureInfo.InvariantCulture));
        Append(builder, Field.Address.ToString("x16", CultureInfo.InvariantCulture));
        Append(builder, Field.Size.ToString(CultureInfo.InvariantCulture));
        Append(builder, Field.IsObjectReference ? "1" : "0");
        Append(builder, Field.ElementType);
        Append(builder, Field.FieldTypeName ?? string.Empty);
        Append(builder, FieldKind.ToString());
        Append(builder, SemanticMode.ToString());
        Append(builder, ((int)ParserBounds).ToString(CultureInfo.InvariantCulture));
        Append(builder, RootBinding.AppliedBounds.Length.ToString(CultureInfo.InvariantCulture));
        foreach (var bound in RootBinding.AppliedBounds)
        {
            Append(builder, bound.Name);
            Append(builder, bound.Value.ToString(CultureInfo.InvariantCulture));
        }

        Append(builder, FieldSelectionBounds.Length.ToString(CultureInfo.InvariantCulture));
        foreach (var bound in FieldSelectionBounds)
        {
            Append(builder, bound.Name);
            Append(builder, bound.Value.ToString(CultureInfo.InvariantCulture));
        }

        if (_coalesceLiteral is null)
        {
            Append(builder, "none");
        }
        else
        {
            Append(builder, _coalesceLiteral.Kind.ToString());
            Append(builder, _coalesceLiteral.Kind switch
            {
                DumpQueryLiteralKind.Null => string.Empty,
                DumpQueryLiteralKind.Int32 => _coalesceLiteral.Int32Value.ToString(CultureInfo.InvariantCulture),
                DumpQueryLiteralKind.String => _coalesceLiteral.StringValue!,
                _ => throw new InvalidOperationException("The coalescing literal kind is invalid."),
            });
        }

        return builder.ToString();
    }

    private static void Append(StringBuilder builder, string value)
    {
        builder.Append(value.Length.ToString(CultureInfo.InvariantCulture));
        builder.Append(':');
        foreach (var character in value)
        {
            builder.Append(((int)character).ToString("X4", CultureInfo.InvariantCulture));
        }
    }
}
