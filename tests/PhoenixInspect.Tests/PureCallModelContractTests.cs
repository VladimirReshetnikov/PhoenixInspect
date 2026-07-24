using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.InteropServices;
using PhoenixInspect.Core.Abstractions;
using Xunit;
using ModuleHandle = PhoenixInspect.Core.Abstractions.ModuleHandle;

namespace PhoenixInspect.Tests;

/// <summary>Verifies the bounded, immutable, structural W4.6 pure-model abstraction contracts.</summary>
public sealed class PureCallModelContractTests
{
    private static readonly ModuleHandle Module = new(
        0x0102030405060708UL,
        0x1112131415161718UL);
    private static readonly MethodHandle Caller = new(Module, 0x06000001);
    private static readonly MethodHandle TargetMethod = new(Module, 0x06000002);
    private static readonly MethodHandle OtherTargetMethod = new(Module, 0x06000003);
    private static readonly TypeSig DeclaringType = TypeSig.CreateTypeDefinition(
        Module,
        0x02000001,
        "W4ModelOwner");
    private static readonly DirectCallSiteIdentity CallSite = new(Caller, 12, TargetMethod);
    private static readonly PureCallModelIdentity Identity = new(
        "w4.combine-markers",
        new PureCallModelVersion(1, 0, 0));

    /// <summary>Checks semantic-version bounds, canonical formatting, and component-sensitive identity.</summary>
    [Fact]
    public void VersionIsBoundedThreePartAndContentEqual()
    {
        var minimum = new PureCallModelVersion(0, 0, 0);
        var maximum = new PureCallModelVersion(
            PureCallModelVersion.MaximumComponent,
            PureCallModelVersion.MaximumComponent,
            PureCallModelVersion.MaximumComponent);
        var equal = new PureCallModelVersion(1, 2, 3);
        var same = new PureCallModelVersion(1, 2, 3);

        Assert.Equal("0.0.0", minimum.ToString());
        Assert.Equal("65535.65535.65535", maximum.ToString());
        Assert.Equal(equal, same);
        Assert.Equal(equal.GetHashCode(), same.GetHashCode());
        Assert.NotEqual(equal, new PureCallModelVersion(2, 2, 3));
        Assert.NotEqual(equal, new PureCallModelVersion(1, 3, 3));
        Assert.NotEqual(equal, new PureCallModelVersion(1, 2, 4));
    }

    /// <summary>Checks every version component rejects values outside its finite canonical range.</summary>
    [Theory]
    [InlineData(-1, 0, 0)]
    [InlineData(0, -1, 0)]
    [InlineData(0, 0, -1)]
    [InlineData(PureCallModelVersion.MaximumComponent + 1, 0, 0)]
    [InlineData(0, PureCallModelVersion.MaximumComponent + 1, 0)]
    [InlineData(0, 0, PureCallModelVersion.MaximumComponent + 1)]
    public void VersionRejectsOutOfRangeComponents(int major, int minor, int patch) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new PureCallModelVersion(major, minor, patch));

    /// <summary>Checks stable model IDs are canonical, bounded, versioned, and compared ordinally.</summary>
    [Fact]
    public void IdentityFreezesCanonicalIdAndVersion()
    {
        var version = new PureCallModelVersion(1, 23, 456);
        var identity = new PureCallModelIdentity("w4.combine-markers_v1", version);
        var same = new PureCallModelIdentity("w4.combine-markers_v1", version);

        Assert.Equal("w4.combine-markers_v1", identity.StableId);
        Assert.Equal(version, identity.Version);
        Assert.Equal("w4.combine-markers_v1@1.23.456", identity.ToString());
        Assert.Equal(identity, same);
        Assert.Equal(identity.GetHashCode(), same.GetHashCode());
        Assert.NotEqual(identity, new PureCallModelIdentity("w4.other", version));
        Assert.NotEqual(identity, new PureCallModelIdentity(
            identity.StableId,
            new PureCallModelVersion(1, 23, 457)));
        Assert.Equal(string.Empty, default(PureCallModelIdentity).ToString());
    }

    /// <summary>Checks identifiers reject ambiguity, unbounded text, display-like case, and non-ASCII input.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("W4.combine-markers")]
    [InlineData(".w4")]
    [InlineData("w4.")]
    [InlineData("w4..model")]
    [InlineData("w4-_model")]
    [InlineData("w4/model")]
    [InlineData("w4.mödel")]
    public void IdentityRejectsNoncanonicalStableIds(string? stableId) =>
        Assert.Throws<ArgumentException>(() => new PureCallModelIdentity(
            stableId!,
            new PureCallModelVersion(1, 0, 0)));

    /// <summary>Checks the exact maximum ID length is admitted and one additional character is rejected.</summary>
    [Fact]
    public void IdentityEnforcesStableIdLengthBound()
    {
        var maximum = new string('a', PureCallModelIdentity.MaximumStableIdLength);
        Assert.Equal(maximum, new PureCallModelIdentity(maximum, default).StableId);
        Assert.Throws<ArgumentException>(() => new PureCallModelIdentity($"{maximum}a", default));
    }

    /// <summary>Checks a descriptor retains every content-equality and normalized policy axis.</summary>
    [Fact]
    public void DescriptorFreezesExactStructuralTargetConfidenceAndEffects()
    {
        var target = Target();
        var descriptor = new PureCallModelDescriptor(
            Identity,
            target,
            PureCallModelConfidence.Exact,
            EvaluationEffectStatus.None);
        var freshEqual = new PureCallModelDescriptor(
            new PureCallModelIdentity("w4.combine-markers", new PureCallModelVersion(1, 0, 0)),
            Target(),
            PureCallModelConfidence.Exact,
            EvaluationEffectStatus.None);

        Assert.Equal(Identity, descriptor.Identity);
        Assert.Equal(target, descriptor.Target);
        Assert.Equal(PureCallModelConfidence.Exact, descriptor.Confidence);
        Assert.Equal(EvaluationEffectStatus.None, descriptor.Effects);
        Assert.Equal(descriptor, freshEqual);
        Assert.Equal(descriptor.GetHashCode(), freshEqual.GetHashCode());
        Assert.NotEqual(
            descriptor,
            new PureCallModelDescriptor(
                new PureCallModelIdentity("w4.combine-markers", new PureCallModelVersion(1, 0, 1)),
                target,
                PureCallModelConfidence.Exact,
                EvaluationEffectStatus.None));
        Assert.NotEqual(
            descriptor,
            new PureCallModelDescriptor(
                Identity,
                Target(OtherTargetMethod),
                PureCallModelConfidence.Exact,
                EvaluationEffectStatus.None));
        Assert.NotEqual(
            descriptor,
            new PureCallModelDescriptor(
                Identity,
                Target(
                    TargetMethod,
                    ImmutableArray.Create(TypeSig.Int32),
                    TypeSig.Int32),
                PureCallModelConfidence.Exact,
                EvaluationEffectStatus.None));
        Assert.NotEqual(
            descriptor,
            new PureCallModelDescriptor(
                Identity,
                target,
                PureCallModelConfidence.BestEffort,
                EvaluationEffectStatus.None));
        Assert.NotEqual(
            descriptor,
            new PureCallModelDescriptor(
                Identity,
                target,
                PureCallModelConfidence.Exact,
                EvaluationEffectStatus.Unsupported));
    }

    /// <summary>Checks every closed confidence declaration is representable independently of scenario admission.</summary>
    [Theory]
    [InlineData(PureCallModelConfidence.Exact)]
    [InlineData(PureCallModelConfidence.BestEffort)]
    [InlineData(PureCallModelConfidence.Partial)]
    [InlineData(PureCallModelConfidence.UnsupportedLayout)]
    public void DescriptorAcceptsEveryClosedConfidenceDeclaration(PureCallModelConfidence confidence)
    {
        var descriptor = new PureCallModelDescriptor(
            Identity,
            Target(),
            confidence,
            EvaluationEffectStatus.None);

        Assert.Equal(confidence, descriptor.Confidence);
    }

    /// <summary>Checks descriptor construction rejects malformed identity and unnormalized declaration states.</summary>
    [Fact]
    public void DescriptorRejectsDefaultIdentityNullTargetAndUnnormalizedDeclarations()
    {
        Assert.Throws<ArgumentException>(() => new PureCallModelDescriptor(
            default,
            Target(),
            PureCallModelConfidence.Exact,
            EvaluationEffectStatus.None));
        Assert.Throws<ArgumentNullException>(() => new PureCallModelDescriptor(
            Identity,
            null!,
            PureCallModelConfidence.Exact,
            EvaluationEffectStatus.None));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PureCallModelDescriptor(
            Identity,
            Target(),
            (PureCallModelConfidence)123,
            EvaluationEffectStatus.None));

        var unsupported = new PureCallModelDescriptor(
            Identity,
            Target(),
            PureCallModelConfidence.Exact,
            EvaluationEffectStatus.Unsupported);
        Assert.Equal(EvaluationEffectStatus.Unsupported, unsupported.Effects);

        foreach (var effects in new[]
                 {
                     EvaluationEffectStatus.VirtualOnly,
                     EvaluationEffectStatus.Modeled,
                     (EvaluationEffectStatus)123,
                 })
        {
            Assert.Throws<ArgumentException>(() => new PureCallModelDescriptor(
                Identity,
                Target(),
                PureCallModelConfidence.Exact,
                effects));
        }
    }

    /// <summary>Checks caller-owned signature storage cannot mutate a descriptor's structural target identity.</summary>
    [Fact]
    public void DescriptorDefensivelyFreezesSignatureParameterStorage()
    {
        var source = ImmutableArray.Create(TypeSig.Int32, TypeSig.Int32);
        var descriptor = new PureCallModelDescriptor(
            Identity,
            Target(TargetMethod, source, TypeSig.Int32),
            PureCallModelConfidence.Exact,
            EvaluationEffectStatus.None);
        var expected = Descriptor();
        var originalHashCode = descriptor.GetHashCode();

        ImmutableCollectionsMarshal.AsArray(source)![0] = TypeSig.Int64;

        Assert.Equal(new[] { TypeSig.Int32, TypeSig.Int32 }, descriptor.Target.Signature.ParameterTypes);
        Assert.Equal(expected, descriptor);
        Assert.Equal(originalHashCode, descriptor.GetHashCode());
        Assert.Equal(expected.GetHashCode(), descriptor.GetHashCode());
    }

    /// <summary>Checks the argument union contains exactly one permitted payload shape.</summary>
    [Fact]
    public void ArgumentAtomsAreClosedTypedAndLineageFree()
    {
        var exact = PureCallModelArgument.ExactInt32(int.MinValue);
        var unknown = PureCallModelArgument.ExplainedUnknownInt32();

        Assert.Equal(PureCallModelArgumentKind.ExactInt32, exact.Kind);
        Assert.Equal(int.MinValue, exact.Int32Value);
        Assert.Equal(PureCallModelArgumentKind.ExplainedUnknownInt32, unknown.Kind);
        Assert.Null(unknown.Int32Value);
        Assert.NotEqual(exact, unknown);
        Assert.Equal(0, (int)default(PureCallModelArgument).Kind);
    }

    /// <summary>Checks invocation retains only a copied, ordered atom vector plus bounded call-site and policy facts.</summary>
    [Fact]
    public void InvocationIsDefensivelyImmutableContentEqualAndNongeneric()
    {
        var source = ImmutableArray.Create(
            PureCallModelArgument.ExactInt32(10),
            PureCallModelArgument.ExplainedUnknownInt32());
        var invocation = new PureCallModelInvocation(
            CallSite,
            source,
            PureCallModelUnknownPolicy.ExplainedInt32);
        var same = new PureCallModelInvocation(
            new DirectCallSiteIdentity(Caller, 12, TargetMethod),
            ImmutableArray.Create(
                PureCallModelArgument.ExactInt32(10),
                PureCallModelArgument.ExplainedUnknownInt32()),
            PureCallModelUnknownPolicy.ExplainedInt32);

        var mutableBacking = ImmutableCollectionsMarshal.AsArray(source)!;
        mutableBacking[0] = PureCallModelArgument.ExactInt32(999);

        Assert.Equal(CallSite, invocation.CallSite);
        Assert.Equal(PureCallModelUnknownPolicy.ExplainedInt32, invocation.UnknownPolicy);
        Assert.Equal(10, invocation.Arguments[0].Int32Value);
        Assert.Equal(PureCallModelArgumentKind.ExplainedUnknownInt32, invocation.Arguments[1].Kind);
        Assert.Equal(invocation, same);
        Assert.Equal(invocation.GetHashCode(), same.GetHashCode());
        var exact = ExactInvocation(CallSite, 10, 20, PureCallModelUnknownPolicy.ExactOnly);
        Assert.NotEqual(
            exact,
            ExactInvocation(
                new DirectCallSiteIdentity(Caller, 13, TargetMethod),
                10,
                20,
                PureCallModelUnknownPolicy.ExactOnly));
        Assert.NotEqual(exact, ExactInvocation(CallSite, 10, 20, PureCallModelUnknownPolicy.ExplainedInt32));
        Assert.NotEqual(exact, ExactInvocation(CallSite, 20, 10, PureCallModelUnknownPolicy.ExactOnly));
        Assert.NotEqual(exact, ExactInvocation(CallSite, 10, 21, PureCallModelUnknownPolicy.ExactOnly));
        Assert.False(typeof(PureCallModelInvocation).IsGenericType);
        Assert.False(typeof(IPureCallModel).IsGenericType);

        static PureCallModelInvocation ExactInvocation(
            DirectCallSiteIdentity callSite,
            int first,
            int second,
            PureCallModelUnknownPolicy policy) =>
            new(
                callSite,
                ImmutableArray.Create(
                    PureCallModelArgument.ExactInt32(first),
                    PureCallModelArgument.ExactInt32(second)),
                policy);
    }

    /// <summary>Checks invocation rejects malformed call sites, vectors, unions, policies, and exact-only unknowns.</summary>
    [Fact]
    public void InvocationRejectsEveryMalformedBoundaryShape()
    {
        var exactArguments = ImmutableArray.Create(
            PureCallModelArgument.ExactInt32(1),
            PureCallModelArgument.ExactInt32(2));

        Assert.Throws<ArgumentException>(() => new PureCallModelInvocation(
            default,
            exactArguments,
            PureCallModelUnknownPolicy.ExactOnly));
        Assert.Throws<ArgumentException>(() => new PureCallModelInvocation(
            CallSite,
            default,
            PureCallModelUnknownPolicy.ExactOnly));
        Assert.Throws<ArgumentException>(() => new PureCallModelInvocation(
            CallSite,
            ImmutableArray<PureCallModelArgument>.Empty,
            PureCallModelUnknownPolicy.ExactOnly));
        Assert.Throws<ArgumentException>(() => new PureCallModelInvocation(
            CallSite,
            ImmutableArray.Create(PureCallModelArgument.ExactInt32(1)),
            PureCallModelUnknownPolicy.ExactOnly));
        Assert.Throws<ArgumentException>(() => new PureCallModelInvocation(
            CallSite,
            ImmutableArray.Create(
                PureCallModelArgument.ExactInt32(1),
                PureCallModelArgument.ExactInt32(2),
                PureCallModelArgument.ExactInt32(3)),
            PureCallModelUnknownPolicy.ExactOnly));
        Assert.Throws<ArgumentException>(() => new PureCallModelInvocation(
            CallSite,
            ImmutableArray.Create(default(PureCallModelArgument), PureCallModelArgument.ExactInt32(2)),
            PureCallModelUnknownPolicy.ExactOnly));
        Assert.Throws<ArgumentException>(() => new PureCallModelInvocation(
            CallSite,
            ImmutableArray.Create(
                PureCallModelArgument.ExplainedUnknownInt32(),
                PureCallModelArgument.ExactInt32(2)),
            PureCallModelUnknownPolicy.ExactOnly));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PureCallModelInvocation(
            CallSite,
            exactArguments,
            (PureCallModelUnknownPolicy)123));
    }

    /// <summary>Checks the public invocation surface has no value-domain, lineage, state, or capability channel.</summary>
    [Fact]
    public void InvocationSurfaceContainsOnlyBoundedStructuralFacts()
    {
        var propertyTypes = typeof(PureCallModelInvocation)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(property => property.PropertyType)
            .ToArray();

        Assert.Equal(
            new[]
            {
                typeof(DirectCallSiteIdentity),
                typeof(ImmutableArray<PureCallModelArgument>),
                typeof(PureCallModelUnknownPolicy),
            },
            propertyTypes);
        Assert.All(
            typeof(PureCallModelInvocation).GetProperties(),
            property => Assert.False(property.CanWrite));

        var invoke = typeof(IPureCallModel).GetMethod(nameof(IPureCallModel.Invoke));
        Assert.NotNull(invoke);
        Assert.Equal(typeof(PureCallModelOutcome), invoke.ReturnType);
        Assert.Equal(
            new[] { typeof(PureCallModelInvocation) },
            invoke.GetParameters().Select(parameter => parameter.ParameterType));
    }

    /// <summary>Checks every typed model outcome exposes exactly its permitted payload.</summary>
    [Fact]
    public void OutcomesPreserveClosedDiscriminatedUnionShape()
    {
        var exact = PureCallModelOutcome.ExactReturn(unchecked(int.MaxValue + 1));
        var unknown = PureCallModelOutcome.UnknownReturn();
        var blocked = PureCallModelOutcome.Blocked("W4.Model.Limitation");
        var invalid = PureCallModelOutcome.Invalid("W4.Model.OutcomeInvalid");

        AssertOutcome(exact, PureCallModelOutcomeKind.ExactReturn, int.MinValue, null, TypeSig.Int32);
        AssertOutcome(unknown, PureCallModelOutcomeKind.UnknownReturn, null, null, TypeSig.Int32);
        AssertOutcome(blocked, PureCallModelOutcomeKind.Blocked, null, "W4.Model.Limitation", null);
        AssertOutcome(invalid, PureCallModelOutcomeKind.Invalid, null, "W4.Model.OutcomeInvalid", null);
        Assert.Equal(PureCallModelOutcome.ExactReturn(int.MinValue), exact);
        Assert.Equal(PureCallModelOutcome.UnknownReturn(), unknown);
        Assert.NotEqual(PureCallModelOutcome.ExactReturn(0), exact);
    }

    /// <summary>Checks model failures accept only bounded canonical codes from the stable W4 model family.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("EXEC_MODEL_FAILURE")]
    [InlineData("W4.Other.Failure")]
    [InlineData("W4.Model.")]
    [InlineData("W4.Model..Failure")]
    [InlineData("W4.Model.Bad-Failure")]
    [InlineData("W4.Model.Fáilure")]
    public void OutcomeRejectsMalformedStableCodes(string? code)
    {
        Assert.Throws<ArgumentException>(() => PureCallModelOutcome.Blocked(code!));
        Assert.Throws<ArgumentException>(() => PureCallModelOutcome.Invalid(code!));
    }

    /// <summary>Checks model outcomes and selections expose and enforce one shared finite code bound.</summary>
    [Fact]
    public void ResultsExposeAndEnforceSharedStableCodeLengthBound()
    {
        var prefix = "W4.Model.";
        Assert.Equal(128, PureCallModelOutcome.MaximumStableCodeLength);
        Assert.Equal(
            PureCallModelOutcome.MaximumStableCodeLength,
            PureCallModelSelectionResult.MaximumStableCodeLength);
        var maximum = prefix + new string(
            'A',
            PureCallModelOutcome.MaximumStableCodeLength - prefix.Length);

        Assert.Equal(maximum, PureCallModelOutcome.Blocked(maximum).StableCode);
        Assert.Equal(maximum, PureCallModelSelectionResult.NotApplicable(maximum).StableCode);
        Assert.Throws<ArgumentException>(() => PureCallModelOutcome.Blocked($"{maximum}A"));
        Assert.Throws<ArgumentException>(() => PureCallModelSelectionResult.NotApplicable($"{maximum}A"));
    }

    /// <summary>Checks selected and failed registry results retain mutually exclusive immutable payloads.</summary>
    [Fact]
    public void SelectionResultPreservesClosedPayloadShape()
    {
        var model = new AdditionModel(Descriptor());
        var selected = PureCallModelSelectionResult.Selected(model);
        var notApplicable = PureCallModelSelectionResult.NotApplicable("W4.Model.SelectionUnavailable");
        var blocked = PureCallModelSelectionResult.Blocked("W4.Model.Capability");
        var invalid = PureCallModelSelectionResult.Invalid("W4.Model.SelectionInvalid");

        AssertSelection(selected, PureCallModelSelectionKind.Selected, model, null, true);
        AssertSelection(
            notApplicable,
            PureCallModelSelectionKind.NotApplicable,
            null,
            "W4.Model.SelectionUnavailable",
            false);
        AssertSelection(blocked, PureCallModelSelectionKind.Blocked, null, "W4.Model.Capability", false);
        AssertSelection(invalid, PureCallModelSelectionKind.Invalid, null, "W4.Model.SelectionInvalid", false);
        Assert.Throws<ArgumentNullException>(() => PureCallModelSelectionResult.Selected(null!));
        Assert.NotEqual(selected, PureCallModelSelectionResult.Selected(model));
        Assert.NotEqual(
            notApplicable,
            PureCallModelSelectionResult.NotApplicable("W4.Model.SelectionUnavailable"));
        Assert.False(typeof(IEquatable<PureCallModelSelectionResult>).IsAssignableFrom(
            typeof(PureCallModelSelectionResult)));
    }

    /// <summary>Checks every failed selection factory shares strict bounded stable-code validation.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Model.SelectionUnavailable")]
    [InlineData("W4.Model.Bad_Code")]
    [InlineData("W4.Model..Failure")]
    public void SelectionRejectsMalformedStableCodes(string? code)
    {
        Assert.Throws<ArgumentException>(() => PureCallModelSelectionResult.NotApplicable(code!));
        Assert.Throws<ArgumentException>(() => PureCallModelSelectionResult.Blocked(code!));
        Assert.Throws<ArgumentException>(() => PureCallModelSelectionResult.Invalid(code!));
    }

    /// <summary>Checks registry selection compares exact MethodDef and signature structure rather than a name.</summary>
    [Fact]
    public void RegistrySelectsOnlyTheExactStructuralTarget()
    {
        var model = new AdditionModel(Descriptor());
        var registry = new SingleStructuralRegistry(model);

        var selected = registry.Select(Target());
        var differentMethod = registry.Select(Target(OtherTargetMethod));
        var differentSignature = registry.Select(Target(
            TargetMethod,
            ImmutableArray.Create(TypeSig.Int32),
            TypeSig.Int32));

        Assert.True(selected.IsSelected);
        Assert.Same(model, selected.Model);
        Assert.Equal(PureCallModelSelectionKind.NotApplicable, differentMethod.Kind);
        Assert.Equal(PureCallModelSelectionKind.NotApplicable, differentSignature.Kind);
        Assert.Equal(3, registry.SelectionCount);
        Assert.Equal(
            new[] { Target(), Target(OtherTargetMethod), Target(TargetMethod, ImmutableArray.Create(TypeSig.Int32), TypeSig.Int32) },
            registry.Requests);
    }

    /// <summary>Checks the model capability consumes only immutable atoms and produces deterministic typed outcomes.</summary>
    [Fact]
    public void ModelCapabilityIsNongenericAndDeterministicForEqualInvocations()
    {
        var model = new AdditionModel(Descriptor());
        var exact = new PureCallModelInvocation(
            CallSite,
            ImmutableArray.Create(
                PureCallModelArgument.ExactInt32(int.MaxValue),
                PureCallModelArgument.ExactInt32(1)),
            PureCallModelUnknownPolicy.ExactOnly);
        var unknown = new PureCallModelInvocation(
            CallSite,
            ImmutableArray.Create(
                PureCallModelArgument.ExplainedUnknownInt32(),
                PureCallModelArgument.ExactInt32(1)),
            PureCallModelUnknownPolicy.ExplainedInt32);

        Assert.Equal(PureCallModelOutcome.ExactReturn(int.MinValue), model.Invoke(exact));
        Assert.Equal(PureCallModelOutcome.ExactReturn(int.MinValue), model.Invoke(exact));
        Assert.Equal(PureCallModelOutcome.UnknownReturn(), model.Invoke(unknown));
        Assert.Equal(3, model.InvocationCount);
    }

    private static PureCallModelDescriptor Descriptor(
        EvaluationEffectStatus effects = EvaluationEffectStatus.None) =>
        new(Identity, Target(), PureCallModelConfidence.Exact, effects);

    private static ResolvedMethodCallTarget Target(MethodHandle? method = null) =>
        Target(
            method ?? TargetMethod,
            ImmutableArray.Create(TypeSig.Int32, TypeSig.Int32),
            TypeSig.Int32);

    private static ResolvedMethodCallTarget Target(
        MethodHandle method,
        ImmutableArray<TypeSig> parameters,
        TypeSig returnType) =>
        new(
            method,
            new MethodCallSignatureShape(
                DeclaringType,
                MethodCallingConventionKind.Default,
                hasImplicitThis: false,
                hasExplicitThis: false,
                genericParameterCount: 0,
                parameters,
                returnType));

    private static void AssertOutcome(
        PureCallModelOutcome outcome,
        PureCallModelOutcomeKind kind,
        int? value,
        string? code,
        TypeSig? returnType)
    {
        Assert.Equal(kind, outcome.Kind);
        Assert.Equal(value, outcome.Int32Value);
        Assert.Equal(code, outcome.StableCode);
        Assert.Equal(returnType, outcome.ReturnType);
    }

    private static void AssertSelection(
        PureCallModelSelectionResult result,
        PureCallModelSelectionKind kind,
        IPureCallModel? model,
        string? code,
        bool selected)
    {
        Assert.Equal(kind, result.Kind);
        Assert.Same(model, result.Model);
        Assert.Equal(code, result.StableCode);
        Assert.Equal(selected, result.IsSelected);
    }

    private sealed class AdditionModel(PureCallModelDescriptor descriptor) : IPureCallModel
    {
        public PureCallModelDescriptor Descriptor { get; } = descriptor;

        internal int InvocationCount { get; private set; }

        public PureCallModelOutcome Invoke(PureCallModelInvocation invocation)
        {
            ArgumentNullException.ThrowIfNull(invocation);
            InvocationCount++;
            if (invocation.Arguments.Any(static argument =>
                    argument.Kind == PureCallModelArgumentKind.ExplainedUnknownInt32))
            {
                return PureCallModelOutcome.UnknownReturn();
            }

            return PureCallModelOutcome.ExactReturn(unchecked(
                invocation.Arguments[0].Int32Value!.Value +
                invocation.Arguments[1].Int32Value!.Value));
        }
    }

    private sealed class SingleStructuralRegistry(IPureCallModel model) : IPureCallModelRegistry
    {
        private readonly List<ResolvedMethodCallTarget> requests = [];

        internal int SelectionCount => requests.Count;

        internal IReadOnlyList<ResolvedMethodCallTarget> Requests => requests;

        public PureCallModelSelectionResult Select(ResolvedMethodCallTarget target)
        {
            ArgumentNullException.ThrowIfNull(target);
            requests.Add(target);
            return model.Descriptor.Target == target
                ? PureCallModelSelectionResult.Selected(model)
                : PureCallModelSelectionResult.NotApplicable("W4.Model.SelectionUnavailable");
        }
    }
}
