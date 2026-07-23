using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using Interpreter.Core.Abstractions;
using Interpreter.Product.DumpQuery;
using Xunit;

namespace Interpreter.IntegrationTests;

/// <summary>Exercises owner closed-construction binding and substituted generic-constraint validation.</summary>
public sealed class W8V2ClosedConstructionBindingTests
{
    private const int PublicClassAttributes = 0x0000_0001;
    private const int NestedPublicClassAttributes = 0x0000_0002;
    private const int InterfaceAttributes = 0x0000_00A1;
    private const int ReferenceTypeConstraintFlag = 0x0000_0004;
    private const int NotNullableValueTypeConstraintFlag = 0x0000_0008;

    private const string CoreAssemblyName = "Synthetic.Core";
    private const string TargetNamespace = "Synthetic.Target";

    private const int GenericSlotToken = 0x0200_0002;
    private const int OuterToken = 0x0200_0003;
    private const int InnerToken = 0x0200_0004;
    private const int WidgetToken = 0x0200_0005;
    private const int PointToken = 0x0200_0006;
    private const int BoundedToken = 0x0200_0007;
    private const int DerivedToken = 0x0200_0008;
    private const int StructSlotToken = 0x0200_0009;
    private const int InterfaceMarkerToken = 0x0200_000A;
    private const int PhantomToken = 0x0200_0010;

    private const int ObjectTypeReferenceToken = 0x0100_0001;
    private const int ValueTypeTypeReferenceToken = 0x0100_0002;
    private const int MissingTypeReferenceToken = 0x0100_0003;
    private const int ConstraintTypeSpecificationToken = 0x1B00_0001;

    /// <summary>
    /// Proves one top-level single-argument generic owner produces an exact closed construction whose canonical
    /// flattened vector, construction segment, and per-parameter constraint disposition are all retained.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void One_argument_generic_owner_constructs_exactly()
    {
        var world = BuildWorld();
        var outcome = Bind("global::Synthetic.Target.GenericSlot<int>.Current", world);

        Assert.Equal(StaticFieldV2ClosedConstructionResultKind.Exact, outcome.ResultKind);
        Assert.Equal(StaticFieldV2ClosedConstructionIssue.None, outcome.Issue);
        Assert.Null(outcome.ReachedBound);
        Assert.Equal(GenericSlotToken, outcome.RelatedMetadataToken);
        Assert.Equal(world.Target, outcome.SourceModule);

        var construction = Assert.IsType<MetadataClosedTypeIdentity>(outcome.OwnerConstruction);
        Assert.Equal(MetadataClosedTypeKind.Named, construction.Kind);
        var segment = Assert.Single(construction.ConstructionSegments);
        Assert.Equal(GenericSlotToken, segment.Classification.TypeDefinition.TypeDefinitionToken);
        Assert.Equal(0, segment.FlattenedArgumentOffset);

        var argument = Assert.Single(outcome.FlattenedArguments);
        Assert.Equal(MetadataClosedTypeKind.Primitive, argument.Kind);
        Assert.Equal(MetadataPrimitiveTypeKind.Int32, argument.PrimitiveKind);
        Assert.Equal(
            construction.FlattenedArguments.Select(static value => value.Sha256).ToArray(),
            outcome.FlattenedArguments.Select(static value => value.Sha256).ToArray());

        var check = Assert.Single(outcome.ConstraintChecks);
        Assert.Equal(StaticFieldV2ConstraintDisposition.Satisfied, check.Disposition);
        Assert.Equal(0, check.ParameterNumber);
        Assert.Null(check.ConstraintTarget);
        Assert.Null(check.Reason);
        Assert.Equal(argument, check.Argument);
        Assert.Equal(1, outcome.ObservedCount);
    }

    /// <summary>
    /// Proves a nested owner flattens outer arguments before inner arguments and partitions that canonical vector
    /// across its outer-to-inner construction segments at contiguous offsets.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Nested_owner_flattens_outer_arguments_before_inner_arguments()
    {
        var world = BuildWorld();
        var outcome = Bind("global::Synthetic.Target.Outer<int>.Inner<string>.Field", world);

        Assert.Equal(StaticFieldV2ClosedConstructionResultKind.Exact, outcome.ResultKind);
        Assert.Equal(InnerToken, outcome.RelatedMetadataToken);
        Assert.Equal(
            [MetadataPrimitiveTypeKind.Int32, MetadataPrimitiveTypeKind.String],
            outcome.FlattenedArguments.Select(static value => value.PrimitiveKind).ToArray());

        var construction = outcome.OwnerConstruction!;
        Assert.Equal(2, construction.ConstructionSegments.Length);
        Assert.Equal(OuterToken, construction.ConstructionSegments[0].Classification.TypeDefinition.TypeDefinitionToken);
        Assert.Equal(InnerToken, construction.ConstructionSegments[1].Classification.TypeDefinition.TypeDefinitionToken);
        Assert.Equal([0, 1], construction.ConstructionSegments.Select(static value => value.FlattenedArgumentOffset).ToArray());
        Assert.Equal([1, 1], construction.ConstructionSegments.Select(static value => value.LocalArgumentCount).ToArray());
        Assert.Equal(
            MetadataPrimitiveTypeKind.Int32,
            construction.ConstructionSegments[0].LocalArguments[0].PrimitiveKind);
        Assert.Equal(
            MetadataPrimitiveTypeKind.String,
            construction.ConstructionSegments[1].LocalArguments[0].PrimitiveKind);

        Assert.Equal(3, outcome.ConstraintChecks.Length);
        Assert.All(
            outcome.ConstraintChecks,
            static check => Assert.Equal(StaticFieldV2ConstraintDisposition.Satisfied, check.Disposition));
        Assert.Equal([0, 0, 1], outcome.ConstraintChecks.Select(static check => check.ParameterNumber).ToArray());
    }

    /// <summary>
    /// Proves a named type argument is resolved by the same exhaustive namespace-split, projected-simple-name, and
    /// introduced-arity rule that binds the owner, and that its own arguments are bound recursively.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Recursive_named_type_argument_constructs_exactly()
    {
        var world = BuildWorld();
        var outcome = Bind(
            "global::Synthetic.Target.GenericSlot<Synthetic.Target.GenericSlot<int>>.Current",
            world);

        Assert.Equal(StaticFieldV2ClosedConstructionResultKind.Exact, outcome.ResultKind);
        var argument = Assert.Single(outcome.FlattenedArguments);
        Assert.Equal(MetadataClosedTypeKind.Named, argument.Kind);
        Assert.Equal(
            GenericSlotToken,
            argument.FinalClassification!.TypeDefinition.TypeDefinitionToken);
        Assert.Equal(MetadataPrimitiveTypeKind.Int32, argument.FlattenedArguments[0].PrimitiveKind);
        Assert.Equal(2, argument.TopologyDepth);
        Assert.Equal(3, outcome.OwnerConstruction!.TopologyDepth);
        Assert.Equal(2, outcome.ObservedCount);
    }

    /// <summary>
    /// Proves every admitted predefined keyword with a closed metadata counterpart maps exactly and that the one
    /// keyword without a counterpart stops as unsupported rather than binding a wrong primitive.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Predefined_arguments_map_exactly_and_decimal_is_unsupported()
    {
        var world = BuildWorld();

        Assert.Equal(
            MetadataPrimitiveTypeKind.Object,
            Bind("global::Synthetic.Target.GenericSlot<object>.Current", world).FlattenedArguments[0].PrimitiveKind);
        Assert.Equal(
            MetadataPrimitiveTypeKind.UInt8,
            Bind("global::Synthetic.Target.GenericSlot<byte>.Current", world).FlattenedArguments[0].PrimitiveKind);
        Assert.Equal(
            MetadataPrimitiveTypeKind.Boolean,
            Bind("global::Synthetic.Target.GenericSlot<bool>.Current", world).FlattenedArguments[0].PrimitiveKind);
        Assert.Equal(
            [MetadataPrimitiveTypeKind.Double, MetadataPrimitiveTypeKind.Char],
            Bind("global::Synthetic.Target.Outer<double>.Inner<char>.Field", world)
                .FlattenedArguments
                .Select(static value => value.PrimitiveKind)
                .ToArray());

        var unsupported = Bind("global::Synthetic.Target.GenericSlot<decimal>.Current", world);
        AssertStop(
            unsupported,
            StaticFieldV2ClosedConstructionResultKind.Unsupported,
            StaticFieldV2ClosedConstructionIssue.PredefinedTypeUnsupported);
        Assert.Equal((int)StaticFieldV2PredefinedTypeKind.Decimal, unsupported.ObservedCount);
    }

    /// <summary>
    /// Proves nullable, single-dimensional array, and multidimensional array arguments build their exact closed
    /// topologies, and that a nullable over a reference element stops rather than minting an invalid head.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Nullable_and_array_arguments_build_exact_closed_topologies()
    {
        var world = BuildWorld();

        var nullable = Bind("global::Synthetic.Target.GenericSlot<int?>.Current", world).FlattenedArguments[0];
        Assert.Equal(MetadataClosedTypeKind.Nullable, nullable.Kind);
        Assert.Equal(MetadataPrimitiveTypeKind.Int32, nullable.ElementType!.PrimitiveKind);
        Assert.Equal("Nullable`1", nullable.FinalClassification!.TypeDefinition.TypeName);
        Assert.Equal(world.Core, nullable.FinalClassification.SourceModule);

        var valueTypeNullable =
            Bind("global::Synthetic.Target.GenericSlot<Synthetic.Target.Point?>.Current", world).FlattenedArguments[0];
        Assert.Equal(MetadataClosedTypeKind.Nullable, valueTypeNullable.Kind);
        Assert.Equal(PointToken, valueTypeNullable.ElementType!.FinalClassification!.TypeDefinition.TypeDefinitionToken);

        var szArray = Bind("global::Synthetic.Target.GenericSlot<int[]>.Current", world).FlattenedArguments[0];
        Assert.Equal(MetadataClosedTypeKind.SzArray, szArray.Kind);
        Assert.Equal(1, szArray.ArrayRank);
        Assert.True(szArray.IsReferenceType);

        var rectangular = Bind("global::Synthetic.Target.GenericSlot<int[,,]>.Current", world).FlattenedArguments[0];
        Assert.Equal(MetadataClosedTypeKind.MultidimensionalArray, rectangular.Kind);
        Assert.Equal(3, rectangular.ArrayRank);
        Assert.Empty(rectangular.ArraySizes);
        Assert.Empty(rectangular.ArrayLowerBounds);

        AssertStop(
            Bind("global::Synthetic.Target.GenericSlot<Synthetic.Target.Widget?>.Current", world),
            StaticFieldV2ClosedConstructionResultKind.Invalid,
            StaticFieldV2ClosedConstructionIssue.NullableElementInvalid);
    }

    /// <summary>
    /// Proves a reference-type special flag and a base-class constraint target are both proven satisfied, that an
    /// exact definition match and a bounded ancestry-chain reach are distinguished, and that a satisfied value-type
    /// flag is proven from the authority-issued role rather than from a name.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Class_and_base_class_constraints_are_proven_satisfied()
    {
        var world = BuildWorld();

        var exact = Bind("global::Synthetic.Target.Bounded<Synthetic.Target.Widget>.Field", world);
        Assert.Equal(StaticFieldV2ClosedConstructionResultKind.Exact, exact.ResultKind);
        Assert.Equal(2, exact.ConstraintChecks.Length);
        Assert.All(
            exact.ConstraintChecks,
            static check => Assert.Equal(StaticFieldV2ConstraintDisposition.Satisfied, check.Disposition));
        Assert.Equal("constraint.reference-type-satisfied", exact.ConstraintChecks[0].Reason);
        Assert.Null(exact.ConstraintChecks[0].ConstraintTarget);
        Assert.Equal("constraint.base-definition-exact", exact.ConstraintChecks[1].Reason);
        Assert.Equal(
            WidgetToken,
            exact.ConstraintChecks[1].ConstraintTarget!.TargetTypeDefinition!.TypeDefinitionToken);

        var reached = Bind("global::Synthetic.Target.Bounded<Synthetic.Target.Derived>.Field", world);
        Assert.Equal(StaticFieldV2ClosedConstructionResultKind.Exact, reached.ResultKind);
        Assert.Equal("constraint.base-definition-reached", reached.ConstraintChecks[1].Reason);
        Assert.Equal(
            DerivedToken,
            reached.FlattenedArguments[0].FinalClassification!.TypeDefinition.TypeDefinitionToken);

        var valueType = Bind("global::Synthetic.Target.StructSlot<Synthetic.Target.Point>.Field", world);
        Assert.Equal(StaticFieldV2ClosedConstructionResultKind.Exact, valueType.ResultKind);
        Assert.Equal("constraint.value-type-satisfied", Assert.Single(valueType.ConstraintChecks).Reason);
    }

    /// <summary>
    /// Proves a violated hard constraint produces an invalid draft stop that retains exactly the one decisive check
    /// with its parameter row and substituted argument and retains no owner construction.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Violated_hard_constraints_are_invalid_and_retain_the_decisive_check()
    {
        var world = BuildWorld();

        var valueTypeViolation = Bind("global::Synthetic.Target.StructSlot<Synthetic.Target.Widget>.Field", world);
        AssertStop(
            valueTypeViolation,
            StaticFieldV2ClosedConstructionResultKind.Invalid,
            StaticFieldV2ClosedConstructionIssue.ConstraintViolated);
        var check = Assert.Single(valueTypeViolation.ConstraintChecks);
        Assert.Equal(StaticFieldV2ConstraintDisposition.Violated, check.Disposition);
        Assert.Equal("constraint.value-type-required", check.Reason);
        Assert.Equal(NotNullableValueTypeConstraintFlag, check.Parameter.Flags);
        Assert.Equal(WidgetToken, check.Argument.FinalClassification!.TypeDefinition.TypeDefinitionToken);
        Assert.Equal(check.Parameter.GenericParameterToken, valueTypeViolation.RelatedMetadataToken);

        var referenceViolation = Bind("global::Synthetic.Target.Bounded<int>.Field", world);
        AssertStop(
            referenceViolation,
            StaticFieldV2ClosedConstructionResultKind.Invalid,
            StaticFieldV2ClosedConstructionIssue.ConstraintViolated);
        var referenceCheck = Assert.Single(referenceViolation.ConstraintChecks);
        Assert.Equal("constraint.reference-type-required", referenceCheck.Reason);
        Assert.Equal(ReferenceTypeConstraintFlag, referenceCheck.Parameter.Flags);

        var unreachableBase = Bind("global::Synthetic.Target.Bounded<Synthetic.Target.GenericSlot<int>>.Field", world);
        AssertStop(
            unreachableBase,
            StaticFieldV2ClosedConstructionResultKind.Invalid,
            StaticFieldV2ClosedConstructionIssue.ConstraintViolated);
        Assert.Equal(
            "constraint.base-definition-unreachable",
            Assert.Single(unreachableBase.ConstraintChecks).Reason);
    }

    /// <summary>
    /// Proves the declared boundary of this draft slice: an interface constraint target, a substituted TypeSpec
    /// constraint target, and an unresolved TypeRef constraint target are all non-exact and unprovable rather than
    /// violated, because their proofs belong to separately owned later authorities.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Interface_typespec_and_unresolved_constraints_are_unprovable_rather_than_violated()
    {
        var world = BuildWorld();

        var interfaceBound = Bind("global::Synthetic.Target.IfaceBound<Synthetic.Target.Widget>.Field", world);
        AssertStop(
            interfaceBound,
            StaticFieldV2ClosedConstructionResultKind.NonExact,
            StaticFieldV2ClosedConstructionIssue.ConstraintUnprovable);
        var interfaceCheck = Assert.Single(interfaceBound.ConstraintChecks);
        Assert.Equal(StaticFieldV2ConstraintDisposition.Unprovable, interfaceCheck.Disposition);
        Assert.Equal("constraint.interface-target-deferred", interfaceCheck.Reason);
        Assert.Equal(MetadataConstraintTargetKind.TypeDefinition, interfaceCheck.ConstraintTarget!.Kind);
        Assert.Equal(
            InterfaceMarkerToken,
            interfaceCheck.ConstraintTarget.TargetTypeDefinition!.TypeDefinitionToken);
        Assert.Equal(
            MetadataTypeDefinitionSemanticRole.Interface,
            world.Ancestry.ExactClassificationOrDefault(world.Target, InterfaceMarkerToken)!.Role);

        var specificationBound = Bind("global::Synthetic.Target.SpecBound<Synthetic.Target.Widget>.Field", world);
        AssertStop(
            specificationBound,
            StaticFieldV2ClosedConstructionResultKind.NonExact,
            StaticFieldV2ClosedConstructionIssue.ConstraintUnprovable);
        var specificationCheck = Assert.Single(specificationBound.ConstraintChecks);
        Assert.Equal("constraint.typespec-target-deferred", specificationCheck.Reason);
        Assert.Equal(MetadataConstraintTargetKind.TypeSpecification, specificationCheck.ConstraintTarget!.Kind);

        var unresolvedBound = Bind("global::Synthetic.Target.RefBound<Synthetic.Target.Widget>.Field", world);
        AssertStop(
            unresolvedBound,
            StaticFieldV2ClosedConstructionResultKind.NonExact,
            StaticFieldV2ClosedConstructionIssue.ConstraintUnprovable);
        var unresolvedCheck = Assert.Single(unresolvedBound.ConstraintChecks);
        Assert.Equal("constraint.unresolved-reference-target", unresolvedCheck.Reason);
        Assert.Equal(
            MetadataConstraintTargetKind.TypeReferenceUnresolved,
            unresolvedCheck.ConstraintTarget!.Kind);

        var primitiveArgument = Bind("global::Synthetic.Target.SpecBound<int>.Field", world);
        Assert.Equal(StaticFieldV2ClosedConstructionResultKind.NonExact, primitiveArgument.ResultKind);
        Assert.Equal("constraint.typespec-target-deferred", Assert.Single(primitiveArgument.ConstraintChecks).Reason);
    }

    /// <summary>
    /// Proves an interface constraint target becomes provable once the binder is given an interface-implementation
    /// portfolio: a direct emitted edge and a base-class contribution both satisfy, a complete closure that never
    /// names the target violates, and an incomplete closure stays unprovable with its typed terminal named in the
    /// retained reason rather than being reported as a violation.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Interface_constraint_targets_are_provable_with_the_interface_portfolio()
    {
        var world = BuildWorld();

        var satisfied = BindWithInterfaces("global::Synthetic.Target.IfaceBound<Synthetic.Target.Widget>.Field", world);
        Assert.Equal(StaticFieldV2ClosedConstructionResultKind.Exact, satisfied.ResultKind);
        Assert.Equal(StaticFieldV2ClosedConstructionIssue.None, satisfied.Issue);
        var satisfiedCheck = Assert.Single(satisfied.ConstraintChecks);
        Assert.Equal(StaticFieldV2ConstraintDisposition.Satisfied, satisfiedCheck.Disposition);
        Assert.Equal("constraint.interface-implemented", satisfiedCheck.Reason);
        Assert.Equal(
            InterfaceMarkerToken,
            satisfiedCheck.ConstraintTarget!.TargetTypeDefinition!.TypeDefinitionToken);
        Assert.Equal(world.InterfaceImplementations, satisfied.InterfaceImplementationPortfolio);

        var inherited = BindWithInterfaces(
            "global::Synthetic.Target.IfaceBound<Synthetic.Target.Derived>.Field",
            world);
        Assert.Equal(StaticFieldV2ClosedConstructionResultKind.Exact, inherited.ResultKind);
        Assert.Equal("constraint.interface-implemented", Assert.Single(inherited.ConstraintChecks).Reason);

        var violated = BindWithInterfaces("global::Synthetic.Target.IfaceBound<Synthetic.Target.Point>.Field", world);
        AssertStop(
            violated,
            StaticFieldV2ClosedConstructionResultKind.Invalid,
            StaticFieldV2ClosedConstructionIssue.ConstraintViolated);
        var violatedCheck = Assert.Single(violated.ConstraintChecks);
        Assert.Equal(StaticFieldV2ConstraintDisposition.Violated, violatedCheck.Disposition);
        Assert.Equal("constraint.interface-not-implemented", violatedCheck.Reason);

        var unprovable = BindWithInterfaces(
            "global::Synthetic.Target.IfaceBound<Synthetic.Target.Phantom>.Field",
            world);
        AssertStop(
            unprovable,
            StaticFieldV2ClosedConstructionResultKind.NonExact,
            StaticFieldV2ClosedConstructionIssue.ConstraintUnprovable);
        var unprovableCheck = Assert.Single(unprovable.ConstraintChecks);
        Assert.Equal(StaticFieldV2ConstraintDisposition.Unprovable, unprovableCheck.Disposition);
        Assert.Equal(
            "constraint.interface-closure-unresolved-reference-reached",
            unprovableCheck.Reason);

        // Omitting the portfolio keeps the pre-migration deferral for exactly the same obligation.
        Assert.Equal(
            "constraint.interface-target-deferred",
            Assert.Single(
                Bind("global::Synthetic.Target.IfaceBound<Synthetic.Target.Widget>.Field", world).ConstraintChecks)
                .Reason);
        Assert.Null(
            Bind("global::Synthetic.Target.IfaceBound<Synthetic.Target.Widget>.Field", world)
                .InterfaceImplementationPortfolio);
    }

    /// <summary>
    /// Proves a supplied interface-implementation portfolio that itself stopped propagates as a typed prefix-free
    /// draft stop rather than silently degrading to the deferred interface obligation.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Interface_portfolio_prerequisite_stops_are_typed_and_prefix_free()
    {
        var world = BuildWorld();

        var nonExact = MetadataInterfaceImplementationPortfolioIdentity.Create(world.Ancestry, default);
        Assert.Equal(MetadataInterfaceImplementationPortfolioResultKind.NonExact, nonExact.ResultKind);
        AssertStop(
            BindWithInterfaces("global::Synthetic.Target.GenericSlot<int>.Current", world, nonExact),
            StaticFieldV2ClosedConstructionResultKind.NonExact,
            StaticFieldV2ClosedConstructionIssue.InterfaceImplementationPortfolioNonExact);

        var twoCores = W8MetadataAncestryAuthorityContractTests.BuildAncestryWorld(
            W8MetadataAncestryAuthorityContractTests.BuildCoreModule(),
            W8MetadataAncestryAuthorityContractTests.BuildCoreModule("Synthetic.Core4", 0xC400, 'b')).Ancestry;
        var invalid = MetadataInterfaceImplementationPortfolioIdentity.Create(twoCores, []);
        Assert.Equal(MetadataInterfaceImplementationPortfolioResultKind.Invalid, invalid.ResultKind);
        AssertStop(
            BindWithInterfaces("global::Synthetic.Target.GenericSlot<int>.Current", world, invalid),
            StaticFieldV2ClosedConstructionResultKind.Invalid,
            StaticFieldV2ClosedConstructionIssue.InterfaceImplementationPortfolioInvalid);
    }

    /// <summary>
    /// Proves a type-argument name that matches no authority chain and a type-argument name that matches two distinct
    /// physical chains both stop with typed prefix-free dispositions instead of selecting one silently.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Absent_and_ambiguous_type_arguments_are_typed_and_prefix_free()
    {
        AssertStop(
            Bind("global::Synthetic.Target.GenericSlot<Nope>.Current", BuildWorld()),
            StaticFieldV2ClosedConstructionResultKind.NonExact,
            StaticFieldV2ClosedConstructionIssue.TypeArgumentAbsent);

        var ambiguousWorld = BuildWorld(BuildOtherModule(
            [new ConstructionTypeRow(TargetNamespace, "Widget", PublicClassAttributes, ObjectTypeReferenceToken)]));
        var ambiguous = Bind(
            "global::Synthetic.Target.GenericSlot<Synthetic.Target.Widget>.Current",
            ambiguousWorld);
        AssertStop(
            ambiguous,
            StaticFieldV2ClosedConstructionResultKind.Invalid,
            StaticFieldV2ClosedConstructionIssue.TypeArgumentAmbiguous);
        Assert.Equal(2, ambiguous.ObservedCount);
    }

    /// <summary>
    /// Proves every prerequisite contradiction stops with a typed prefix-free draft disposition that retains no
    /// construction, and records that per-segment arity re-assertion is unreachable because the preceding name binder
    /// already refuses every arity disagreement with a complete absent answer.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Prerequisite_stops_are_typed_and_prefix_free()
    {
        var world = BuildWorld();

        var absent = Bind("global::Synthetic.Target.GenericSlot<int, string>.Current", world);
        AssertStop(
            absent,
            StaticFieldV2ClosedConstructionResultKind.NonExact,
            StaticFieldV2ClosedConstructionIssue.NameBindingAbsent);
        Assert.Equal(StaticFieldV2TypeNameBindingResultKind.Absent, absent.NameBinding.ResultKind);
        Assert.Contains(
            absent.NameBinding.PartitionResults,
            static result => result.FirstRejectionReason == StaticFieldV2TypeNameRejectionReason.ArityMismatch);
        Assert.Equal(StaticFieldV2ClosedConstructionIssue.ArityDisagreement, DeclaredArityDisagreementIssue());

        var ambiguousOwnerWorld = BuildWorld(BuildOtherModule(
            [new ConstructionTypeRow(
                TargetNamespace,
                "GenericSlot`1",
                PublicClassAttributes,
                ObjectTypeReferenceToken,
                GenericArity: 1)]));
        AssertStop(
            Bind("global::Synthetic.Target.GenericSlot<int>.Current", ambiguousOwnerWorld),
            StaticFieldV2ClosedConstructionResultKind.Invalid,
            StaticFieldV2ClosedConstructionIssue.NameBindingAmbiguous);

        AssertStop(
            Bind("alias::Synthetic.Target.GenericSlot<int>.Current", world),
            StaticFieldV2ClosedConstructionResultKind.Unsupported,
            StaticFieldV2ClosedConstructionIssue.NameBindingNotExact);

        var nonExactChainPortfolio = MetadataNamedTypeDefinitionChainPortfolioIdentity.Create(
            MetadataDefinitionCompatibilityPortfolioIdentity.Create(
                ImmutableArray<MetadataW7TypeDefinitionCompatibilityCatalogIdentity>.Empty.Add(null!)),
            [world.TargetModule.Ancestry.ChainCatalog]);
        var nonExactName = StaticFieldV2TypeNameBinder.BindExplicitRoute(
            Parse("global::Synthetic.Target.GenericSlot<int>.Current"),
            nonExactChainPortfolio);
        AssertStop(
            StaticFieldV2ClosedConstructionBinder.BindOwnerConstruction(
                nonExactName,
                world.Ancestry,
                world.Constraints),
            StaticFieldV2ClosedConstructionResultKind.NonExact,
            StaticFieldV2ClosedConstructionIssue.NameBindingNotExact);

        var exactName = StaticFieldV2TypeNameBinder.BindExplicitRoute(
            Parse("global::Synthetic.Target.GenericSlot<int>.Current"),
            world.ChainPortfolio);
        Assert.Equal(StaticFieldV2TypeNameBindingResultKind.Exact, exactName.ResultKind);

        var coreless = W8MetadataAncestryAuthorityContractTests.BuildAncestryWorld(
            W8MetadataAncestryAuthorityContractTests.BuildCustomModule(
                "Synthetic.NoCore",
                0xF100,
                'f',
                [new W8MetadataAncestryAuthorityContractTests.TypeRow(
                    "Synthetic.NoCore", "Loner", PublicClassAttributes, null)]));
        Assert.Equal(MetadataAncestryAuthorityPortfolioResultKind.NonExact, coreless.Ancestry.ResultKind);
        AssertStop(
            StaticFieldV2ClosedConstructionBinder.BindOwnerConstruction(
                exactName,
                coreless.Ancestry,
                world.Constraints),
            StaticFieldV2ClosedConstructionResultKind.NonExact,
            StaticFieldV2ClosedConstructionIssue.AncestryPortfolioNonExact);

        var twoCores = W8MetadataAncestryAuthorityContractTests.BuildAncestryWorld(
            W8MetadataAncestryAuthorityContractTests.BuildCoreModule(),
            W8MetadataAncestryAuthorityContractTests.BuildCoreModule("Synthetic.Core2", 0xF200, 'e'));
        Assert.Equal(MetadataAncestryAuthorityPortfolioResultKind.Invalid, twoCores.Ancestry.ResultKind);
        AssertStop(
            StaticFieldV2ClosedConstructionBinder.BindOwnerConstruction(
                exactName,
                twoCores.Ancestry,
                world.Constraints),
            StaticFieldV2ClosedConstructionResultKind.Invalid,
            StaticFieldV2ClosedConstructionIssue.AncestryPortfolioInvalid);

        var uninitializedConstraints = MetadataConstraintTargetResolutionPortfolioIdentity.Create(
            world.Resolution,
            default);
        Assert.Equal(
            MetadataConstraintTargetResolutionPortfolioResultKind.NonExact,
            uninitializedConstraints.ResultKind);
        AssertStop(
            StaticFieldV2ClosedConstructionBinder.BindOwnerConstruction(
                exactName,
                world.Ancestry,
                uninitializedConstraints),
            StaticFieldV2ClosedConstructionResultKind.NonExact,
            StaticFieldV2ClosedConstructionIssue.ConstraintPortfolioNonExact);

        var conflictingConstraints = MetadataConstraintTargetResolutionPortfolioIdentity.Create(
            world.Resolution,
            [world.CoreModule.Constraints, world.TargetModule.Constraints, world.CoreModule.Constraints]);
        Assert.Equal(
            MetadataConstraintTargetResolutionPortfolioResultKind.Invalid,
            conflictingConstraints.ResultKind);
        AssertStop(
            StaticFieldV2ClosedConstructionBinder.BindOwnerConstruction(
                exactName,
                world.Ancestry,
                conflictingConstraints),
            StaticFieldV2ClosedConstructionResultKind.Invalid,
            StaticFieldV2ClosedConstructionIssue.ConstraintPortfolioInvalid);

        var stranger = BuildOtherModule(
            [new ConstructionTypeRow(TargetNamespace, "Widget", PublicClassAttributes, ObjectTypeReferenceToken)]);
        var strangerResolution =
            W8MetadataAncestryAuthorityContractTests.BuildAncestryWorld(stranger.Ancestry).Resolution;
        var strangerConstraints = MetadataConstraintTargetResolutionPortfolioIdentity.Create(
            strangerResolution,
            [stranger.Constraints]);
        Assert.Equal(
            MetadataConstraintTargetResolutionPortfolioResultKind.Exact,
            strangerConstraints.ResultKind);
        AssertStop(
            StaticFieldV2ClosedConstructionBinder.BindOwnerConstruction(
                exactName,
                world.Ancestry,
                strangerConstraints),
            StaticFieldV2ClosedConstructionResultKind.Invalid,
            StaticFieldV2ClosedConstructionIssue.OwnerModuleNotInPortfolio);

        Assert.Throws<ArgumentNullException>(() =>
            StaticFieldV2ClosedConstructionBinder.BindOwnerConstruction(null!, world.Ancestry, world.Constraints));
        Assert.Throws<ArgumentNullException>(() =>
            StaticFieldV2ClosedConstructionBinder.BindOwnerConstruction(exactName, null!, world.Constraints));
        Assert.Throws<ArgumentNullException>(() =>
            StaticFieldV2ClosedConstructionBinder.BindOwnerConstruction(exactName, world.Ancestry, null!));
    }

    /// <summary>
    /// Proves the closed-type topology depth cap and the cumulative closed generic-argument cap both stop at cap plus
    /// one with the declared shared bound retained and no partial construction, even though the admitted source
    /// syntax alone stayed inside every parser bound.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Closed_topology_bounds_stop_at_cap_plus_one_without_a_prefix()
    {
        const int depthCap = StaticFieldV2ClosedConstructionOutcome.MaximumClosedTypeTopologyDepth;
        const int argumentCap = StaticFieldV2ClosedConstructionOutcome.MaximumClosedArgumentCount;
        var world = BuildWorld();

        var deepArgument =
            string.Concat(Enumerable.Repeat("G<", depthCap - 1)) +
            "int" +
            new string('>', depthCap - 1);
        var deep = Bind($"global::Synthetic.Target.GenericSlot<{deepArgument}>.Current", world);
        AssertStop(
            deep,
            StaticFieldV2ClosedConstructionResultKind.NonExact,
            StaticFieldV2ClosedConstructionIssue.TopologyDepthBoundReached);
        var depthBound = Assert.IsType<EvaluationDeterministicBound>(deep.ReachedBound);
        Assert.Equal("expression-v2.closed-type.depth", depthBound.Name);
        Assert.Equal(depthCap, depthBound.Value);
        Assert.Equal(depthCap + 1, deep.ObservedCount);

        var wideArguments = string.Join(", ", Enumerable.Repeat("int?", 33));
        var wide = Bind($"global::Synthetic.Target.Wide<{wideArguments}>.Current", world);
        AssertStop(
            wide,
            StaticFieldV2ClosedConstructionResultKind.NonExact,
            StaticFieldV2ClosedConstructionIssue.ArgumentCountBoundReached);
        var argumentBound = Assert.IsType<EvaluationDeterministicBound>(wide.ReachedBound);
        Assert.Equal("expression-v2.signature.arguments", argumentBound.Name);
        Assert.Equal(argumentCap, argumentBound.Value);
        Assert.Equal(argumentCap + 1, wide.ObservedCount);
    }

    /// <summary>
    /// Proves canonical replay equality against a frozen golden digest, defensive copies for every returned draft
    /// array, guarded private issuance for the retained constraint check, the closed public issuer surface, and
    /// emitted draft XML documentation.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Closed_construction_contracts_are_replayable_immutable_guarded_and_documented()
    {
        var outcome = Bind("global::Synthetic.Target.Bounded<Synthetic.Target.Derived>.Field", BuildWorld());
        var replay = Bind("global::Synthetic.Target.Bounded<Synthetic.Target.Derived>.Field", BuildWorld());

        Assert.Equal(outcome, replay);
        Assert.Equal(outcome.GetHashCode(), replay.GetHashCode());
        Assert.Equal(outcome.Sha256, replay.Sha256);
        Assert.Equal(
            "b2dea9b211bde9c11e1409003a99809a1507286c371bb8dea82a309d546ed8fc",
            outcome.Sha256);

        var originalBytes = outcome.CanonicalBytes;
        var originalArguments = outcome.FlattenedArguments;
        var originalChecks = outcome.ConstraintChecks;
        var check = originalChecks[0];
        var originalCheckBytes = check.CanonicalBytes;

        ImmutableCollectionsMarshal.AsArray(outcome.CanonicalBytes)![0] ^= 0x5A;
        ImmutableCollectionsMarshal.AsArray(outcome.FlattenedArguments)![0] = null!;
        ImmutableCollectionsMarshal.AsArray(outcome.ConstraintChecks)![0] = null!;
        ImmutableCollectionsMarshal.AsArray(check.CanonicalBytes)![0] ^= 0x33;

        Assert.True(originalBytes.AsSpan().SequenceEqual(outcome.CanonicalBytes.AsSpan()));
        Assert.True(originalCheckBytes.AsSpan().SequenceEqual(check.CanonicalBytes.AsSpan()));
        Assert.Equal(originalArguments[0], outcome.FlattenedArguments[0]);
        Assert.Equal(originalChecks[0], outcome.ConstraintChecks[0]);
        Assert.Equal(outcome.Sha256, replay.Sha256);

        Assert.False(StaticFieldV2ClosedConstructionOutcome.OwnsRowMintCapability(new object()));
        Assert.Throws<ArgumentException>(() => StaticFieldV2ConstraintCheckIdentity.Create(
            new object(),
            check.Parameter,
            check.Argument,
            check.Disposition,
            check.ConstraintTarget,
            check.Reason));

        var publicTypes = new[]
        {
            typeof(StaticFieldV2ClosedConstructionResultKind),
            typeof(StaticFieldV2ClosedConstructionIssue),
            typeof(StaticFieldV2ConstraintDisposition),
            typeof(StaticFieldV2ConstraintCheckIdentity),
            typeof(StaticFieldV2ClosedConstructionOutcome),
            typeof(StaticFieldV2ClosedConstructionBinder),
        };
        foreach (var type in publicTypes)
        {
            Assert.Empty(type.GetConstructors(BindingFlags.Public | BindingFlags.Instance));
            var publicStatics = type.GetMethods(
                    BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Where(static method => !method.IsSpecialName)
                .Select(static method => method.Name)
                .Distinct()
                .ToArray();
            if (type == typeof(StaticFieldV2ClosedConstructionBinder))
            {
                Assert.Equal(["BindOwnerConstruction"], publicStatics);
            }
            else if (!type.IsEnum)
            {
                Assert.Empty(publicStatics);
            }
        }
        AssertPublicDraftXml(publicTypes);
    }

    private static StaticFieldV2ClosedConstructionIssue DeclaredArityDisagreementIssue() =>
        Enum.GetValues<StaticFieldV2ClosedConstructionIssue>()
            .Single(static issue => issue == StaticFieldV2ClosedConstructionIssue.ArityDisagreement);

    private static StaticFieldV2ExpressionDescriptor Parse(string expressionText)
    {
        var parsed = StaticFieldV2ExpressionParser.Parse(expressionText);
        Assert.Equal(DumpExpressionSyntaxStatus.Admitted, parsed.Status);
        return parsed.Descriptor!;
    }

    private static StaticFieldV2ClosedConstructionOutcome Bind(string expressionText, ConstructionWorld world)
    {
        var nameBinding = StaticFieldV2TypeNameBinder.BindExplicitRoute(Parse(expressionText), world.ChainPortfolio);
        return StaticFieldV2ClosedConstructionBinder.BindOwnerConstruction(
            nameBinding,
            world.Ancestry,
            world.Constraints);
    }

    private static StaticFieldV2ClosedConstructionOutcome BindWithInterfaces(
        string expressionText,
        ConstructionWorld world,
        MetadataInterfaceImplementationPortfolioIdentity? portfolio = null)
    {
        var nameBinding = StaticFieldV2TypeNameBinder.BindExplicitRoute(Parse(expressionText), world.ChainPortfolio);
        return StaticFieldV2ClosedConstructionBinder.BindOwnerConstruction(
            nameBinding,
            world.Ancestry,
            world.Constraints,
            portfolio ?? world.InterfaceImplementations);
    }

    private static void AssertStop(
        StaticFieldV2ClosedConstructionOutcome outcome,
        StaticFieldV2ClosedConstructionResultKind resultKind,
        StaticFieldV2ClosedConstructionIssue issue)
    {
        Assert.Equal(resultKind, outcome.ResultKind);
        Assert.Equal(issue, outcome.Issue);
        Assert.Null(outcome.OwnerConstruction);
        Assert.Empty(outcome.FlattenedArguments);
    }

    private static ConstructionWorld BuildWorld(params ConstructionModule[] additionalModules)
    {
        var core = BuildCoreModule();
        var target = BuildTargetModule();
        var modules = new[] { core, target }.Concat(additionalModules).ToArray();
        var ancestryWorld = W8MetadataAncestryAuthorityContractTests.BuildAncestryWorld(
            [.. modules.Select(static module => module.Ancestry)]);
        Assert.Equal(MetadataAncestryAuthorityPortfolioResultKind.Exact, ancestryWorld.Ancestry.ResultKind);
        var constraints = MetadataConstraintTargetResolutionPortfolioIdentity.Create(
            ancestryWorld.Resolution,
            [.. modules.Select(static module => module.Constraints)]);
        Assert.Equal(MetadataConstraintTargetResolutionPortfolioResultKind.Exact, constraints.ResultKind);
        var byModule = modules.ToDictionary(static module => module.Ancestry.Module);
        var interfaces = MetadataInterfaceImplementationPortfolioIdentity.Create(
            ancestryWorld.Ancestry,
            [.. ancestryWorld.Ancestry.Entries.Select(entry => byModule[entry.SourceModule].InterfaceCatalog)]);
        Assert.Equal(MetadataInterfaceImplementationPortfolioResultKind.Exact, interfaces.ResultKind);
        return new ConstructionWorld(
            core,
            target,
            ancestryWorld.Resolution,
            ancestryWorld.Ancestry,
            constraints,
            interfaces);
    }

    private static ConstructionModule BuildCoreModule() =>
        BuildModule(
            CoreAssemblyName,
            0xC100,
            'c',
            [
                new ConstructionTypeRow("System", "Object", PublicClassAttributes, null),
                new ConstructionTypeRow("System", "ValueType", PublicClassAttributes, 0x0200_0002),
                new ConstructionTypeRow("System", "Enum", PublicClassAttributes, 0x0200_0003),
                new ConstructionTypeRow("System", "Delegate", PublicClassAttributes, 0x0200_0002),
                new ConstructionTypeRow("System", "MulticastDelegate", PublicClassAttributes, 0x0200_0005),
                new ConstructionTypeRow("System", "Nullable`1", PublicClassAttributes, 0x0200_0003, GenericArity: 1),
            ]);

    private static ConstructionModule BuildTargetModule() =>
        BuildModule(
            "Synthetic.TargetAssembly",
            0xC200,
            'd',
            [
                new ConstructionTypeRow(
                    TargetNamespace, "GenericSlot`1", PublicClassAttributes, ObjectTypeReferenceToken, GenericArity: 1),
                new ConstructionTypeRow(
                    TargetNamespace, "Outer`1", PublicClassAttributes, ObjectTypeReferenceToken, GenericArity: 1),
                new ConstructionTypeRow(
                    string.Empty,
                    "Inner`1",
                    NestedPublicClassAttributes,
                    ObjectTypeReferenceToken,
                    GenericArity: 2,
                    EnclosingTypeRowId: 3),
                new ConstructionTypeRow(TargetNamespace, "Widget", PublicClassAttributes, ObjectTypeReferenceToken),
                new ConstructionTypeRow(TargetNamespace, "Point", PublicClassAttributes, ValueTypeTypeReferenceToken),
                new ConstructionTypeRow(
                    TargetNamespace,
                    "Bounded`1",
                    PublicClassAttributes,
                    ObjectTypeReferenceToken,
                    GenericArity: 1,
                    GenericParameterFlags: ReferenceTypeConstraintFlag),
                new ConstructionTypeRow(TargetNamespace, "Derived", PublicClassAttributes, WidgetToken),
                new ConstructionTypeRow(
                    TargetNamespace,
                    "StructSlot`1",
                    PublicClassAttributes,
                    ObjectTypeReferenceToken,
                    GenericArity: 1,
                    GenericParameterFlags: NotNullableValueTypeConstraintFlag),
                new ConstructionTypeRow(TargetNamespace, "IMarker", InterfaceAttributes, null),
                new ConstructionTypeRow(
                    TargetNamespace, "IfaceBound`1", PublicClassAttributes, ObjectTypeReferenceToken, GenericArity: 1),
                new ConstructionTypeRow(
                    TargetNamespace, "RefBound`1", PublicClassAttributes, ObjectTypeReferenceToken, GenericArity: 1),
                new ConstructionTypeRow(
                    TargetNamespace, "Wide`33", PublicClassAttributes, ObjectTypeReferenceToken, GenericArity: 33),
                new ConstructionTypeRow(
                    TargetNamespace, "SpecBound`1", PublicClassAttributes, ObjectTypeReferenceToken, GenericArity: 1),
                new ConstructionTypeRow(
                    string.Empty, "G`1", PublicClassAttributes, ObjectTypeReferenceToken, GenericArity: 1),
                new ConstructionTypeRow(
                    TargetNamespace, "Phantom", PublicClassAttributes, ObjectTypeReferenceToken),
            ],
            interfaceImplementations: module =>
            [
                MetadataInterfaceImplementationRowObservationIdentity.Create(
                    module,
                    0x0900_0001,
                    WidgetToken,
                    InterfaceMarkerToken),
                MetadataInterfaceImplementationRowObservationIdentity.Create(
                    module,
                    0x0900_0002,
                    PhantomToken,
                    MissingTypeReferenceToken),
            ],
            constraints:
            [
                new ConstraintRowSpec(5, WidgetToken),
                new ConstraintRowSpec(7, InterfaceMarkerToken),
                new ConstraintRowSpec(8, MissingTypeReferenceToken),
                new ConstraintRowSpec(42, ConstraintTypeSpecificationToken),
            ],
            typeReferences:
            [
                ("System", "Object"),
                ("System", "ValueType"),
                ("Synthetic", "Missing"),
            ],
            typeSpecifications: true);

    private static ConstructionModule BuildOtherModule(ImmutableArray<ConstructionTypeRow> namedTypes) =>
        BuildModule(
            "Synthetic.OtherAssembly",
            0xC300,
            'e',
            namedTypes,
            typeReferences: [("System", "Object")]);

    private static ConstructionModule BuildModule(
        string assemblyName,
        ulong moduleAddress,
        char digestCharacter,
        ImmutableArray<ConstructionTypeRow> namedTypes,
        ImmutableArray<ConstraintRowSpec> constraints = default,
        ImmutableArray<(string NamespaceName, string TypeName)> typeReferences = default,
        bool typeSpecifications = false,
        Func<StaticFieldMetadataModuleIdentity,
            ImmutableArray<MetadataInterfaceImplementationRowObservationIdentity>>? interfaceImplementations = null)
    {
        var module = W8CompilerNameMappingContractTests.CreateMetadataModule(
            moduleAddress,
            digestCharacter,
            assemblyName);
        var constraintSpecs = constraints.IsDefault ? [] : constraints;
        var referenceSpecs = typeReferences.IsDefault ? [] : typeReferences;
        var interfaceRows = interfaceImplementations?.Invoke(module) ?? [];

        var typeReferenceRows =
            ImmutableArray.CreateBuilder<MetadataTypeReferenceRowObservationIdentity>(referenceSpecs.Length);
        for (var index = 0; index < referenceSpecs.Length; index++)
        {
            typeReferenceRows.Add(W8MetadataAncestryAuthorityContractTests.TypeReferenceRow(
                module,
                index + 1,
                referenceSpecs[index].NamespaceName,
                referenceSpecs[index].TypeName,
                0x2300_0001));
        }
        var assemblyReferenceRows = referenceSpecs.IsEmpty
            ? ImmutableArray<MetadataAssemblyReferenceRowObservationIdentity>.Empty
            : [W8MetadataAncestryAuthorityContractTests.AssemblyReferenceRow(module, 1, CoreAssemblyName)];
        var typeSpecificationRows = typeSpecifications
            ? ImmutableArray.Create(MetadataTypeSpecificationRowObservationIdentity.Create(
                module,
                ConstraintTypeSpecificationToken,
                [0x15, 0x12, 0x08, 0x01, 0x0E]))
            : ImmutableArray<MetadataTypeSpecificationRowObservationIdentity>.Empty;

        var genericParameterRows = ImmutableArray.CreateBuilder<MetadataGenericParameterRowObservationIdentity>();
        var nestedClassRows = ImmutableArray.CreateBuilder<MetadataNestedClassRowObservationIdentity>();
        for (var index = 0; index < namedTypes.Length; index++)
        {
            var row = namedTypes[index];
            var ownerToken = 0x0200_0002 + index;
            for (var number = 0; number < row.GenericArity; number++)
            {
                genericParameterRows.Add(MetadataGenericParameterRowObservationIdentity.Create(
                    module,
                    0x2A00_0000 | checked(genericParameterRows.Count + 1),
                    number,
                    flags: row.GenericParameterFlags,
                    ownerMetadataToken: ownerToken,
                    name: $"T{ownerToken & 0x00FF_FFFF}_{number}"));
            }
            if (row.EnclosingTypeRowId is { } enclosingRowId)
            {
                nestedClassRows.Add(MetadataNestedClassRowObservationIdentity.Create(
                    module,
                    0x2900_0000 | checked(nestedClassRows.Count + 1),
                    nestedTypeDefinitionToken: ownerToken,
                    enclosingTypeDefinitionToken: 0x0200_0000 | enclosingRowId));
            }
        }

        var constraintRows =
            ImmutableArray.CreateBuilder<MetadataGenericParameterConstraintRowObservationIdentity>(
                constraintSpecs.Length);
        for (var index = 0; index < constraintSpecs.Length; index++)
        {
            constraintRows.Add(MetadataGenericParameterConstraintRowObservationIdentity.Create(
                module,
                0x2C00_0000 | (index + 1),
                0x2A00_0000 | constraintSpecs[index].OwnerGenericParameterRowId,
                constraintSpecs[index].ConstraintToken));
        }

        var totalTypeCount = namedTypes.Length + 1;
        var sourceEnds = MetadataSourceEndIdentity.Create(
            module,
            StaticFieldModuleSearchFact.Exact(
                module: module.Module,
                moduleContent: module.ModuleContent,
                typeDefinitionsExamined: totalTypeCount,
                fieldDefinitionsExamined: 0,
                typeDefinitionRowCount: totalTypeCount,
                fieldDefinitionRowCount: 0,
                typeReferenceRowCount: typeReferenceRows.Count,
                typeSpecificationRowCount: typeSpecificationRows.Length,
                assemblyReferenceRowCount: assemblyReferenceRows.Length,
                interfaceImplementationRowCount: interfaceRows.Length,
                nestedClassRowCount: nestedClassRows.Count,
                genericParameterRowCount: genericParameterRows.Count,
                genericParameterConstraintRowCount: constraintRows.Count));

        var typeRows = ImmutableArray.CreateBuilder<MetadataTypeDefinitionRowObservationIdentity>(totalTypeCount);
        typeRows.Add(MetadataTypeDefinitionRowObservationIdentity.Create(
            module,
            0x0200_0001,
            fieldListRowId: 1,
            methodListRowId: 1,
            namespaceName: string.Empty,
            typeName: "<Module>",
            typeAttributes: 0,
            extendsMetadataToken: null));
        for (var index = 0; index < namedTypes.Length; index++)
        {
            var row = namedTypes[index];
            typeRows.Add(MetadataTypeDefinitionRowObservationIdentity.Create(
                module,
                0x0200_0002 + index,
                fieldListRowId: 1,
                methodListRowId: 1,
                namespaceName: row.NamespaceName,
                typeName: row.TypeName,
                typeAttributes: row.TypeAttributes,
                extendsMetadataToken: row.ExtendsMetadataToken));
        }

        var pointers = MetadataMemberPointerTableCatalogIdentity.Create(sourceEnds, default, default);
        var typeDefinitions = MetadataTypeDefinitionTableCatalogIdentity.Create(
            sourceEnds,
            typeRows.MoveToImmutable(),
            pointers);
        var nestedClasses = MetadataNestedClassTableCatalogIdentity.Create(
            sourceEnds,
            typeDefinitions,
            nestedClassRows.ToImmutable());
        var genericParameters = MetadataGenericParameterPhysicalTableCatalogIdentity.Create(
            sourceEnds,
            genericParameterRows.Count == 0 ? default : genericParameterRows.ToImmutable());
        var methods = MetadataMethodDefinitionTableCatalogIdentity.Create(typeDefinitions, default);
        var authority = MetadataDefinitionAuthorityCatalogIdentity.Create(
            typeDefinitions,
            nestedClasses,
            genericParameters,
            methods);
        Assert.Equal(MetadataDefinitionAuthorityResultKind.Exact, authority.ResultKind);

        var referenceEnds = MetadataReferenceSourceEndIdentity.Create(sourceEnds);
        var tables = MetadataModuleReferenceTableSetIdentity.Create(
            referenceEnds,
            MetadataTypeReferencePhysicalTableCatalogIdentity.Create(referenceEnds, typeReferenceRows.ToImmutable()),
            MetadataModuleReferencePhysicalTableCatalogIdentity.Create(
                referenceEnds,
                ImmutableArray<MetadataModuleReferenceRowObservationIdentity>.Empty),
            MetadataTypeSpecificationPhysicalTableCatalogIdentity.Create(referenceEnds, typeSpecificationRows),
            MetadataAssemblyReferencePhysicalTableCatalogIdentity.Create(referenceEnds, assemblyReferenceRows),
            MetadataAssemblyFilePhysicalTableCatalogIdentity.Create(
                referenceEnds,
                ImmutableArray<MetadataAssemblyFileRowObservationIdentity>.Empty),
            MetadataExportedTypePhysicalTableCatalogIdentity.Create(
                referenceEnds,
                ImmutableArray<MetadataExportedTypeRowObservationIdentity>.Empty));
        Assert.True(tables.AllTablesExact);

        var compatibility = MetadataW7TypeDefinitionCompatibilityCatalogIdentity.Create(
            authority,
            NullCandidateSlots(authority));
        var chainCatalog = MetadataNamedTypeDefinitionChainCatalogIdentity.Create(
            compatibility,
            MetadataCompilerNameMappingCatalogIdentity.Create(authority));
        Assert.Equal(MetadataNamedTypeDefinitionChainCatalogResultKind.Exact, chainCatalog.ResultKind);

        var constraintCatalog = MetadataGenericParameterConstraintPhysicalTableCatalogIdentity.Create(
            sourceEnds,
            MetadataGenericParameterAuthorityCatalogIdentity.Create(authority),
            constraintRows.ToImmutable());
        Assert.Equal(
            MetadataGenericParameterConstraintPhysicalTableResultKind.Exact,
            constraintCatalog.ResultKind);

        var interfaceCatalog = MetadataInterfaceImplementationTableCatalogIdentity.Create(authority, interfaceRows);
        Assert.Equal(MetadataInterfaceImplementationTableResultKind.Exact, interfaceCatalog.ResultKind);

        return new ConstructionModule(
            new W8MetadataAncestryAuthorityContractTests.AncestryModule(
                module,
                compatibility,
                chainCatalog,
                tables),
            constraintCatalog,
            interfaceCatalog);
    }

    private static ImmutableArray<StaticFieldTypeDefinitionIdentity?> NullCandidateSlots(
        MetadataDefinitionAuthorityCatalogIdentity authority)
    {
        var builder = ImmutableArray.CreateBuilder<StaticFieldTypeDefinitionIdentity?>(
            authority.TypeDefinitions.Length);
        for (var index = 0; index < authority.TypeDefinitions.Length; index++)
        {
            builder.Add(null);
        }
        return builder.MoveToImmutable();
    }

    private static void AssertPublicDraftXml(params Type[] publicTypes)
    {
        var assembly = typeof(StaticFieldV2ClosedConstructionBinder).Assembly;
        var documentation = XDocument.Load(Path.ChangeExtension(assembly.Location, ".xml"));
        var members = documentation.Descendants("member").ToArray();
        foreach (var type in publicTypes)
        {
            var typeDocumentation = Assert.Single(members, member =>
                string.Equals((string?)member.Attribute("name"), $"T:{type.FullName}", StringComparison.Ordinal));
            Assert.Contains("draft", typeDocumentation.Value, StringComparison.OrdinalIgnoreCase);
            foreach (var method in type.GetMethods(
                         BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                     .Where(static method => !method.IsSpecialName))
            {
                var prefix = $"M:{type.FullName}.{method.Name}";
                var methodDocumentation = members.Where(member =>
                    ((string?)member.Attribute("name")) is { } name &&
                    (string.Equals(name, prefix, StringComparison.Ordinal) ||
                     name.StartsWith($"{prefix}(", StringComparison.Ordinal))).ToArray();
                Assert.NotEmpty(methodDocumentation);
                Assert.All(methodDocumentation, static member =>
                    Assert.Contains("draft", member.Value, StringComparison.OrdinalIgnoreCase));
            }
        }
    }

    private sealed record ConstructionTypeRow(
        string NamespaceName,
        string TypeName,
        int TypeAttributes,
        int? ExtendsMetadataToken,
        int GenericArity = 0,
        int? EnclosingTypeRowId = null,
        int GenericParameterFlags = 0);

    private sealed record ConstraintRowSpec(int OwnerGenericParameterRowId, int ConstraintToken);

    private sealed record ConstructionModule(
        W8MetadataAncestryAuthorityContractTests.AncestryModule Ancestry,
        MetadataGenericParameterConstraintPhysicalTableCatalogIdentity Constraints,
        MetadataInterfaceImplementationTableCatalogIdentity InterfaceCatalog);

    private sealed record ConstructionWorld(
        ConstructionModule CoreModule,
        ConstructionModule TargetModule,
        MetadataTypeReferenceResolutionPortfolioIdentity Resolution,
        MetadataAncestryAuthorityPortfolioIdentity Ancestry,
        MetadataConstraintTargetResolutionPortfolioIdentity Constraints,
        MetadataInterfaceImplementationPortfolioIdentity InterfaceImplementations)
    {
        internal StaticFieldMetadataModuleIdentity Core => CoreModule.Ancestry.Module;

        internal StaticFieldMetadataModuleIdentity Target => TargetModule.Ancestry.Module;

        internal MetadataNamedTypeDefinitionChainPortfolioIdentity ChainPortfolio => Resolution.ChainPortfolio;
    }
}
