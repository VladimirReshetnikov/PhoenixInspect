using System.Collections.Immutable;
using PhoenixInspect.Core.Abstractions;

namespace PhoenixInspect.Product.DumpQuery;

/// <summary>Classifies route-specific context acquisition independently from later V2 stages.</summary>
/// <remarks>This draft-phase axis may change as route contracts are refined.</remarks>
public enum DumpExpressionContextOutcome
{
    /// <summary>The selected route requires no frame, PDB, or import context.</summary>
    NotRequired = 1,
    /// <summary>All required context facts are exact.</summary>
    Exact = 2,
    /// <summary>Some required context facts are present but the set is incomplete.</summary>
    Partial = 3,
    /// <summary>A required context artifact is unavailable.</summary>
    Unavailable = 4,
    /// <summary>Multiple distinct context candidates remain.</summary>
    Ambiguous = 5,
    /// <summary>Required context sources disagree.</summary>
    Conflict = 6,
    /// <summary>A required context artifact is malformed.</summary>
    Invalid = 7,
    /// <summary>The context shape is outside the selected profile.</summary>
    Unsupported = 8,
    /// <summary>An earlier stage prevented context acquisition.</summary>
    NotReached = 9,
}

/// <summary>Classifies frame-root or bare-root attribution independently from type binding.</summary>
/// <remarks>This draft-phase axis may change as attribution contracts are refined.</remarks>
public enum DumpExpressionRootAttributionOutcome
{
    /// <summary>The selected route has no separate root-attribution operation.</summary>
    NotRequired = 1,
    /// <summary>One exact root was attributed.</summary>
    Exact = 2,
    /// <summary>A higher-precedence lexical declaration blocks the requested root.</summary>
    Shadowed = 3,
    /// <summary>Root evidence is present but incomplete.</summary>
    Partial = 4,
    /// <summary>A required root artifact is unavailable.</summary>
    Unavailable = 5,
    /// <summary>Multiple distinct root candidates remain.</summary>
    Ambiguous = 6,
    /// <summary>Root sources disagree.</summary>
    Conflict = 7,
    /// <summary>Root evidence is malformed.</summary>
    Invalid = 8,
    /// <summary>The root shape is outside the selected profile.</summary>
    Unsupported = 9,
    /// <summary>An earlier stage prevented root attribution.</summary>
    NotReached = 10,
}

/// <summary>Classifies completeness of the lexical-blocker catalog for a bare root.</summary>
/// <remarks>This draft-phase axis may change as lexical contracts are refined.</remarks>
public enum DumpExpressionLexicalCompletenessOutcome
{
    /// <summary>The selected route does not consult a bare-root lexical catalog.</summary>
    NotRequired = 1,
    /// <summary>The relevant lexical catalog is complete.</summary>
    Complete = 2,
    /// <summary>One exact higher-precedence declaration owns the requested spelling.</summary>
    Shadowed = 3,
    /// <summary>The relevant lexical catalog is incomplete.</summary>
    Partial = 4,
    /// <summary>A lexical construct is outside the selected profile.</summary>
    Unsupported = 5,
    /// <summary>An earlier stage prevented lexical catalog evaluation.</summary>
    NotReached = 6,
}

/// <summary>Classifies namespace, type, and alias binding independently from closed construction.</summary>
/// <remarks>This draft-phase axis may change as binding contracts are refined.</remarks>
public enum DumpExpressionTypeBindingOutcome
{
    /// <summary>The selected frame route needs no namespace or type-name binding.</summary>
    NotRequired = 1,
    /// <summary>One exact metadata name route was bound.</summary>
    Exact = 2,
    /// <summary>No declaration exists at the winning lookup level.</summary>
    Absent = 3,
    /// <summary>The complete candidate universe is not known.</summary>
    Partial = 4,
    /// <summary>A required metadata artifact is unavailable.</summary>
    Unavailable = 5,
    /// <summary>Multiple distinct candidates survive the same winning level.</summary>
    Ambiguous = 6,
    /// <summary>Authoritative name sources disagree.</summary>
    Conflict = 7,
    /// <summary>A metadata name fact is malformed.</summary>
    Invalid = 8,
    /// <summary>The name route is outside the selected profile.</summary>
    Unsupported = 9,
    /// <summary>An earlier stage prevented type-name binding.</summary>
    NotReached = 10,
}

/// <summary>Classifies recursive closed metadata construction independently from member lookup.</summary>
/// <remarks>This draft-phase axis may change as construction contracts are refined.</remarks>
public enum DumpExpressionTypeConstructionOutcome
{
    /// <summary>The selected frame route already owns an exact root type.</summary>
    NotRequired = 1,
    /// <summary>The metadata construction is fully closed and exact.</summary>
    Exact = 2,
    /// <summary>At least one owner or method type parameter remains open.</summary>
    Open = 3,
    /// <summary>Construction evidence is incomplete.</summary>
    Partial = 4,
    /// <summary>A required construction artifact is unavailable.</summary>
    Unavailable = 5,
    /// <summary>Multiple distinct constructions remain.</summary>
    Ambiguous = 6,
    /// <summary>Construction sources disagree.</summary>
    Conflict = 7,
    /// <summary>The construction or a hard constraint is invalid.</summary>
    Invalid = 8,
    /// <summary>The construction topology or constraint is outside the selected profile.</summary>
    Unsupported = 9,
    /// <summary>An earlier stage prevented construction.</summary>
    NotReached = 10,
}

/// <summary>Classifies definition-kind-specific field lookup independently from runtime mapping.</summary>
/// <remarks>This draft-phase axis may change as member contracts are refined.</remarks>
public enum DumpExpressionMemberLookupOutcome
{
    /// <summary>A frame value has no static member-lookup operation.</summary>
    NotRequired = 1,
    /// <summary>One exact field declaration was selected.</summary>
    Exact = 2,
    /// <summary>No matching declaration exists under the fixed hiding rule.</summary>
    Absent = 3,
    /// <summary>The complete member universe is not known.</summary>
    Partial = 4,
    /// <summary>A required member artifact is unavailable.</summary>
    Unavailable = 5,
    /// <summary>A higher-precedence non-field or unsupported field shape owns the spelling.</summary>
    HiddenByUnsupportedMember = 6,
    /// <summary>Multiple distinct declarations survive the same winning level.</summary>
    Ambiguous = 7,
    /// <summary>Member sources disagree.</summary>
    Conflict = 8,
    /// <summary>A selected member fact is malformed.</summary>
    Invalid = 9,
    /// <summary>The selected member shape is outside the profile.</summary>
    Unsupported = 10,
    /// <summary>An earlier stage prevented member lookup.</summary>
    NotReached = 11,
}

/// <summary>Classifies mapping to an exact loaded closed runtime construction.</summary>
/// <remarks>This draft-phase axis may change as runtime contracts are refined.</remarks>
public enum DumpExpressionRuntimeConstructionOutcome
{
    /// <summary>The selected literal, module-RVA, or frame strategy needs no runtime construction.</summary>
    NotRequired = 1,
    /// <summary>One exact loaded closed construction was mapped.</summary>
    Exact = 2,
    /// <summary>No matching loaded construction exists.</summary>
    Absent = 3,
    /// <summary>The complete runtime candidate universe is not known.</summary>
    Partial = 4,
    /// <summary>A required runtime artifact is unavailable.</summary>
    Unavailable = 5,
    /// <summary>Multiple distinct runtime candidates match.</summary>
    Ambiguous = 6,
    /// <summary>Runtime and metadata construction sources disagree.</summary>
    Conflict = 7,
    /// <summary>A runtime construction fact is malformed.</summary>
    Invalid = 8,
    /// <summary>The runtime construction shape is outside the profile.</summary>
    Unsupported = 9,
    /// <summary>An earlier stage prevented runtime mapping.</summary>
    NotReached = 10,
}

/// <summary>Classifies strategy-specific storage acquisition independently from value decoding.</summary>
/// <remarks>This draft-phase axis may change as storage contracts are refined.</remarks>
public enum DumpExpressionStorageOutcome
{
    /// <summary>A metadata literal requires no storage acquisition.</summary>
    NotRequired = 1,
    /// <summary>One exact address-backed or frame-location strategy is frozen.</summary>
    Exact = 2,
    /// <summary>Storage evidence is present but incomplete.</summary>
    Partial = 3,
    /// <summary>A required storage artifact is unavailable.</summary>
    Unavailable = 4,
    /// <summary>Multiple distinct storage locations remain.</summary>
    Ambiguous = 5,
    /// <summary>Storage sources disagree.</summary>
    Conflict = 6,
    /// <summary>A storage descriptor is malformed.</summary>
    Invalid = 7,
    /// <summary>The storage form is outside the selected profile.</summary>
    Unsupported = 8,
    /// <summary>An earlier stage prevented storage selection.</summary>
    NotReached = 9,
}

/// <summary>Classifies direct root-value projection independently from suffix evaluation.</summary>
/// <remarks>This draft-phase axis may change as projection contracts are refined.</remarks>
public enum DumpExpressionValueOutcome
{
    /// <summary>An exact non-null value was projected.</summary>
    ExactValue = 1,
    /// <summary>An exact null value was projected.</summary>
    ExactNull = 2,
    /// <summary>Some required bytes or value facts are present but incomplete.</summary>
    Partial = 3,
    /// <summary>A required value source is unavailable.</summary>
    Unavailable = 4,
    /// <summary>Value sources disagree.</summary>
    Conflict = 5,
    /// <summary>The value bytes or shape are malformed.</summary>
    Invalid = 6,
    /// <summary>The value shape is outside the selected profile.</summary>
    Unsupported = 7,
    /// <summary>An earlier stage prevented value projection.</summary>
    NotReached = 8,
}

/// <summary>Classifies the unchanged detached suffix independently from root-value precision.</summary>
/// <remarks>This draft-phase axis may change as suffix contracts are refined.</remarks>
public enum DumpExpressionSuffixOutcome
{
    /// <summary>No suffix was requested.</summary>
    NotRequested = 1,
    /// <summary>The requested suffix completed.</summary>
    Completed = 2,
    /// <summary>A null root/intermediate or non-exact value prevented suffix completion.</summary>
    Blocked = 3,
    /// <summary>Suffix evidence conflicts.</summary>
    Conflict = 4,
    /// <summary>The suffix result is invalid.</summary>
    Invalid = 5,
    /// <summary>The suffix cannot consume the selected root value.</summary>
    Unsupported = 6,
    /// <summary>An earlier stage or a no-suffix terminal stop prevented suffix evaluation.</summary>
    NotReached = 7,
}

/// <summary>Classifies overall answer completeness without collapsing the independent stage axes.</summary>
/// <remarks>This draft-phase axis may change as completeness contracts are refined.</remarks>
public enum DumpExpressionCompletenessOutcome
{
    /// <summary>The selected request produced a complete exact answer.</summary>
    Complete = 1,
    /// <summary>The selected request produced bounded incomplete operation evidence; value projection may be unreached.</summary>
    Partial = 2,
    /// <summary>The selected request produced no answer value.</summary>
    NoAnswer = 3,
}

/// <summary>Freezes all independent W8 outcome axes and rejects impossible stage progression.</summary>
/// <remarks>
/// This sealed draft aggregate preserves each typed stop instead of deriving it from a diagnostic message. Its factory
/// enforces the fixed operation order but permits both static and frame routes through explicit NotRequired values.
/// </remarks>
public sealed class DumpExpressionV2OutcomeAxes : IEquatable<DumpExpressionV2OutcomeAxes>
{
    private const string CanonicalDomain = "expression-v2-outcome-axes";
    private const int CanonicalSchemaVersion = 1;
    private readonly ImmutableArray<byte> canonicalBytes;

    private DumpExpressionV2OutcomeAxes(
        DumpExpressionSyntaxStatus syntax,
        DumpExpressionContextOutcome context,
        DumpExpressionRootAttributionOutcome rootAttribution,
        DumpExpressionLexicalCompletenessOutcome lexicalCompleteness,
        DumpExpressionTypeBindingOutcome typeBinding,
        DumpExpressionTypeConstructionOutcome typeConstruction,
        DumpExpressionMemberLookupOutcome memberLookup,
        DumpExpressionRuntimeConstructionOutcome runtimeConstruction,
        DumpExpressionStorageOutcome storage,
        DumpExpressionValueOutcome value,
        DumpExpressionSuffixOutcome suffix,
        DumpExpressionCompletenessOutcome completeness)
    {
        Syntax = syntax;
        Context = context;
        RootAttribution = rootAttribution;
        LexicalCompleteness = lexicalCompleteness;
        TypeBinding = typeBinding;
        TypeConstruction = typeConstruction;
        MemberLookup = memberLookup;
        RuntimeConstruction = runtimeConstruction;
        Storage = storage;
        Value = value;
        Suffix = suffix;
        Completeness = completeness;

        var writer = new CanonicalReplayEncoding.Writer(CanonicalDomain, CanonicalSchemaVersion);
        writer.WriteInt32((int)syntax);
        writer.WriteInt32((int)context);
        writer.WriteInt32((int)rootAttribution);
        writer.WriteInt32((int)lexicalCompleteness);
        writer.WriteInt32((int)typeBinding);
        writer.WriteInt32((int)typeConstruction);
        writer.WriteInt32((int)memberLookup);
        writer.WriteInt32((int)runtimeConstruction);
        writer.WriteInt32((int)storage);
        writer.WriteInt32((int)value);
        writer.WriteInt32((int)suffix);
        writer.WriteInt32((int)completeness);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
    }

    /// <summary>Gets the complete-parse and selected-profile syntax axis.</summary>
    public DumpExpressionSyntaxStatus Syntax { get; }
    /// <summary>Gets the context-acquisition axis.</summary>
    public DumpExpressionContextOutcome Context { get; }
    /// <summary>Gets the root-attribution axis.</summary>
    public DumpExpressionRootAttributionOutcome RootAttribution { get; }
    /// <summary>Gets the lexical-completeness axis.</summary>
    public DumpExpressionLexicalCompletenessOutcome LexicalCompleteness { get; }
    /// <summary>Gets the namespace/type-binding axis.</summary>
    public DumpExpressionTypeBindingOutcome TypeBinding { get; }
    /// <summary>Gets the closed-type-construction axis.</summary>
    public DumpExpressionTypeConstructionOutcome TypeConstruction { get; }
    /// <summary>Gets the member-lookup axis.</summary>
    public DumpExpressionMemberLookupOutcome MemberLookup { get; }
    /// <summary>Gets the runtime-construction axis.</summary>
    public DumpExpressionRuntimeConstructionOutcome RuntimeConstruction { get; }
    /// <summary>Gets the storage axis.</summary>
    public DumpExpressionStorageOutcome Storage { get; }
    /// <summary>Gets the value axis.</summary>
    public DumpExpressionValueOutcome Value { get; }
    /// <summary>Gets the suffix axis.</summary>
    public DumpExpressionSuffixOutcome Suffix { get; }
    /// <summary>Gets overall answer completeness.</summary>
    public DumpExpressionCompletenessOutcome Completeness { get; }
    /// <summary>Gets a defensive copy of the canonical bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => ExpressionV2ContractEncoding.Copy(canonicalBytes);
    /// <summary>Gets the lowercase SHA-256 digest of the canonical bytes.</summary>
    public string Sha256 { get; }

    /// <summary>Creates a validated fixed-order outcome aggregate.</summary>
    /// <param name="syntax">The initial syntax disposition.</param>
    /// <param name="context">The context-acquisition disposition.</param>
    /// <param name="rootAttribution">The root-attribution disposition.</param>
    /// <param name="lexicalCompleteness">The lexical-completeness disposition.</param>
    /// <param name="typeBinding">The namespace/type-binding disposition.</param>
    /// <param name="typeConstruction">The closed-type-construction disposition.</param>
    /// <param name="memberLookup">The member-lookup disposition.</param>
    /// <param name="runtimeConstruction">The runtime-construction disposition.</param>
    /// <param name="storage">The storage disposition.</param>
    /// <param name="value">The direct-value disposition.</param>
    /// <param name="suffix">The suffix disposition.</param>
    /// <param name="completeness">The independently retained overall completeness.</param>
    /// <returns>A sealed immutable draft aggregate.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Any enum value is undefined.</exception>
    /// <exception cref="ArgumentException">The axes describe impossible fixed-order progression or completeness.</exception>
    /// <remarks>This draft-phase factory validates the current progression model, which may change as the prototype evolves.</remarks>
    public static DumpExpressionV2OutcomeAxes Create(
        DumpExpressionSyntaxStatus syntax,
        DumpExpressionContextOutcome context,
        DumpExpressionRootAttributionOutcome rootAttribution,
        DumpExpressionLexicalCompletenessOutcome lexicalCompleteness,
        DumpExpressionTypeBindingOutcome typeBinding,
        DumpExpressionTypeConstructionOutcome typeConstruction,
        DumpExpressionMemberLookupOutcome memberLookup,
        DumpExpressionRuntimeConstructionOutcome runtimeConstruction,
        DumpExpressionStorageOutcome storage,
        DumpExpressionValueOutcome value,
        DumpExpressionSuffixOutcome suffix,
        DumpExpressionCompletenessOutcome completeness)
    {
        ExpressionV2ContractEncoding.RequireDefined(syntax, nameof(syntax));
        ExpressionV2ContractEncoding.RequireDefined(context, nameof(context));
        ExpressionV2ContractEncoding.RequireDefined(rootAttribution, nameof(rootAttribution));
        ExpressionV2ContractEncoding.RequireDefined(lexicalCompleteness, nameof(lexicalCompleteness));
        ExpressionV2ContractEncoding.RequireDefined(typeBinding, nameof(typeBinding));
        ExpressionV2ContractEncoding.RequireDefined(typeConstruction, nameof(typeConstruction));
        ExpressionV2ContractEncoding.RequireDefined(memberLookup, nameof(memberLookup));
        ExpressionV2ContractEncoding.RequireDefined(runtimeConstruction, nameof(runtimeConstruction));
        ExpressionV2ContractEncoding.RequireDefined(storage, nameof(storage));
        ExpressionV2ContractEncoding.RequireDefined(value, nameof(value));
        ExpressionV2ContractEncoding.RequireDefined(suffix, nameof(suffix));
        ExpressionV2ContractEncoding.RequireDefined(completeness, nameof(completeness));

        if (syntax is DumpExpressionSyntaxStatus.Invalid or DumpExpressionSyntaxStatus.Unsupported)
        {
            if (context != DumpExpressionContextOutcome.NotReached ||
                rootAttribution != DumpExpressionRootAttributionOutcome.NotReached ||
                lexicalCompleteness != DumpExpressionLexicalCompletenessOutcome.NotReached ||
                typeBinding != DumpExpressionTypeBindingOutcome.NotReached ||
                typeConstruction != DumpExpressionTypeConstructionOutcome.NotReached ||
                memberLookup != DumpExpressionMemberLookupOutcome.NotReached ||
                runtimeConstruction != DumpExpressionRuntimeConstructionOutcome.NotReached ||
                storage != DumpExpressionStorageOutcome.NotReached ||
                value != DumpExpressionValueOutcome.NotReached ||
                suffix != DumpExpressionSuffixOutcome.NotReached ||
                completeness != DumpExpressionCompletenessOutcome.NoAnswer)
            {
                throw new ArgumentException(
                    "An Invalid or Unsupported syntax stop requires every later axis NotReached and NoAnswer.");
            }
            return new DumpExpressionV2OutcomeAxes(
                syntax,
                context,
                rootAttribution,
                lexicalCompleteness,
                typeBinding,
                typeConstruction,
                memberLookup,
                runtimeConstruction,
                storage,
                value,
                suffix,
                completeness);
        }

        var stages = new[]
        {
            Stage(context is DumpExpressionContextOutcome.NotRequired or DumpExpressionContextOutcome.Exact,
                context == DumpExpressionContextOutcome.NotReached, context == DumpExpressionContextOutcome.Partial),
            Stage(rootAttribution is DumpExpressionRootAttributionOutcome.NotRequired or DumpExpressionRootAttributionOutcome.Exact,
                rootAttribution == DumpExpressionRootAttributionOutcome.NotReached,
                rootAttribution == DumpExpressionRootAttributionOutcome.Partial),
            Stage(lexicalCompleteness is DumpExpressionLexicalCompletenessOutcome.NotRequired or DumpExpressionLexicalCompletenessOutcome.Complete,
                lexicalCompleteness == DumpExpressionLexicalCompletenessOutcome.NotReached,
                lexicalCompleteness == DumpExpressionLexicalCompletenessOutcome.Partial),
            Stage(typeBinding is DumpExpressionTypeBindingOutcome.NotRequired or DumpExpressionTypeBindingOutcome.Exact,
                typeBinding == DumpExpressionTypeBindingOutcome.NotReached, typeBinding == DumpExpressionTypeBindingOutcome.Partial),
            Stage(typeConstruction is DumpExpressionTypeConstructionOutcome.NotRequired or DumpExpressionTypeConstructionOutcome.Exact,
                typeConstruction == DumpExpressionTypeConstructionOutcome.NotReached,
                typeConstruction == DumpExpressionTypeConstructionOutcome.Partial),
            Stage(memberLookup is DumpExpressionMemberLookupOutcome.NotRequired or DumpExpressionMemberLookupOutcome.Exact,
                memberLookup == DumpExpressionMemberLookupOutcome.NotReached, memberLookup == DumpExpressionMemberLookupOutcome.Partial),
            Stage(runtimeConstruction is DumpExpressionRuntimeConstructionOutcome.NotRequired or DumpExpressionRuntimeConstructionOutcome.Exact,
                runtimeConstruction == DumpExpressionRuntimeConstructionOutcome.NotReached,
                runtimeConstruction == DumpExpressionRuntimeConstructionOutcome.Partial),
            Stage(storage is DumpExpressionStorageOutcome.NotRequired or DumpExpressionStorageOutcome.Exact,
                storage == DumpExpressionStorageOutcome.NotReached, storage == DumpExpressionStorageOutcome.Partial),
        };

        var stopped = false;
        var partialStop = false;
        foreach (var stage in stages)
        {
            if (stopped)
            {
                if (!stage.NotReached)
                {
                    throw new ArgumentException("Every stage after a typed stop must be NotReached.");
                }
                continue;
            }
            if (stage.NotReached)
            {
                throw new ArgumentException("A stage cannot be NotReached while every earlier stage can continue.");
            }
            if (!stage.CanContinue)
            {
                stopped = true;
                partialStop = stage.Partial;
            }
        }

        if (stopped)
        {
            if (value != DumpExpressionValueOutcome.NotReached || suffix != DumpExpressionSuffixOutcome.NotReached)
            {
                throw new ArgumentException("Value and suffix must be NotReached after an earlier typed stop.");
            }
            var required = partialStop ? DumpExpressionCompletenessOutcome.Partial : DumpExpressionCompletenessOutcome.NoAnswer;
            if (completeness != required)
            {
                throw new ArgumentException("Overall completeness contradicts the first typed stop.", nameof(completeness));
            }
        }
        else
        {
            ValidateValueAndSuffix(value, suffix, completeness);
        }

        return new DumpExpressionV2OutcomeAxes(
            syntax,
            context,
            rootAttribution,
            lexicalCompleteness,
            typeBinding,
            typeConstruction,
            memberLookup,
            runtimeConstruction,
            storage,
            value,
            suffix,
            completeness);
    }

    /// <summary>Tests canonical content equality.</summary>
    /// <param name="other">The other aggregate.</param>
    /// <returns><see langword="true"/> only for byte-identical canonical content.</returns>
    public bool Equals(DumpExpressionV2OutcomeAxes? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as DumpExpressionV2OutcomeAxes);

    /// <inheritdoc/>
    public override int GetHashCode() => CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);

    private static (bool CanContinue, bool NotReached, bool Partial) Stage(
        bool canContinue,
        bool notReached,
        bool partial) => (canContinue, notReached, partial);

    private static void ValidateValueAndSuffix(
        DumpExpressionValueOutcome value,
        DumpExpressionSuffixOutcome suffix,
        DumpExpressionCompletenessOutcome completeness)
    {
        switch (value)
        {
            case DumpExpressionValueOutcome.ExactValue:
            case DumpExpressionValueOutcome.ExactNull:
                if (suffix is DumpExpressionSuffixOutcome.NotRequested or DumpExpressionSuffixOutcome.Completed)
                {
                    if (completeness != DumpExpressionCompletenessOutcome.Complete)
                    {
                        throw new ArgumentException("An exact completed value requires Complete overall disposition.");
                    }
                }
                else if (suffix is DumpExpressionSuffixOutcome.Blocked or DumpExpressionSuffixOutcome.Conflict or
                         DumpExpressionSuffixOutcome.Invalid or DumpExpressionSuffixOutcome.Unsupported)
                {
                    if (completeness != DumpExpressionCompletenessOutcome.NoAnswer)
                    {
                        throw new ArgumentException("A terminal suffix disposition requires NoAnswer.");
                    }
                }
                else
                {
                    throw new ArgumentException("An exact root value requires a reached suffix disposition.", nameof(suffix));
                }
                break;
            case DumpExpressionValueOutcome.Partial:
                if (suffix is not (DumpExpressionSuffixOutcome.Blocked or DumpExpressionSuffixOutcome.NotReached) ||
                    completeness != DumpExpressionCompletenessOutcome.Partial)
                {
                    throw new ArgumentException("A partial value requires a blocked or unreached suffix and Partial completeness.");
                }
                break;
            case DumpExpressionValueOutcome.Unavailable:
                RequireNoAnswerSuffix(suffix, completeness, DumpExpressionSuffixOutcome.Blocked);
                break;
            case DumpExpressionValueOutcome.Conflict:
                RequireNoAnswerSuffix(suffix, completeness, DumpExpressionSuffixOutcome.Conflict);
                break;
            case DumpExpressionValueOutcome.Invalid:
                RequireNoAnswerSuffix(suffix, completeness, DumpExpressionSuffixOutcome.Invalid);
                break;
            case DumpExpressionValueOutcome.Unsupported:
                RequireNoAnswerSuffix(suffix, completeness, DumpExpressionSuffixOutcome.Unsupported);
                break;
            case DumpExpressionValueOutcome.NotReached:
                throw new ArgumentException("Value cannot be NotReached when all preceding stages can continue.", nameof(value));
            default:
                throw new ArgumentOutOfRangeException(nameof(value));
        }
    }

    private static void RequireNoAnswerSuffix(
        DumpExpressionSuffixOutcome actualSuffix,
        DumpExpressionCompletenessOutcome completeness,
        DumpExpressionSuffixOutcome correspondingSuffix)
    {
        if (actualSuffix != correspondingSuffix && actualSuffix != DumpExpressionSuffixOutcome.NotReached ||
            completeness != DumpExpressionCompletenessOutcome.NoAnswer)
        {
            throw new ArgumentException("The value stop, suffix stop, and NoAnswer completeness must agree.");
        }
    }
}
