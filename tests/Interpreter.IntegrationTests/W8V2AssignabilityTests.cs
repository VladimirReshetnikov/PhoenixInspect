using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using Interpreter.Core.Abstractions;
using Interpreter.Product.DumpQuery;
using Xunit;

namespace Interpreter.IntegrationTests;

/// <summary>Exercises exact constructed assignability over closed types and the ancestry authority portfolio.</summary>
/// <remarks>
/// The differential reproduced here mirrors the frozen W8.1 assignability rows from one synthetic authority world:
/// identity, the System.Object root, direct and two-hop base edges, invariant, covariant, and contravariant argument
/// positions in both directions, value-type arguments in a declared variant position, vector and matrix covariance,
/// rank mismatch, SZ-versus-multidimensional topology mismatch, value-element covariance rejection, nullable and
/// primitive equality, the unreachable interface target, and both declared bounds at cap plus one.
/// </remarks>
public sealed class W8V2AssignabilityTests
{
    private const int PublicClassAttributes = 0x0000_0001;
    private const int InterfaceAttributes = 0x0000_00A1;

    private const int CovariantVarianceFlag = 0x0000_0001;
    private const int ContravariantVarianceFlag = 0x0000_0002;

    private const string CoreAssemblyName = "Synthetic.Core";
    private const string TargetAssemblyName = "Synthetic.AssignabilityTarget";
    private const string TargetNamespace = "Synthetic.Assignability";

    private const int ObjectToken = 0x0200_0002;
    private const int ValueTypeToken = 0x0200_0003;
    private const int NullableToken = 0x0200_0007;

    private const int RequestToken = 0x0200_0002;
    private const int BatchToken = 0x0200_0003;
    private const int ValueToken = 0x0200_0004;
    private const int PointToken = 0x0200_0005;
    private const int BaseNodeToken = 0x0200_0006;
    private const int DerivedNodeToken = 0x0200_0007;
    private const int LeafNodeToken = 0x0200_0008;
    private const int InvariantNodeToken = 0x0200_0009;
    private const int CovariantNodeToken = 0x0200_000A;
    private const int ContravariantNodeToken = 0x0200_000B;
    private const int CarrierToken = 0x0200_000C;
    private const int PairNodeToken = 0x0200_000D;
    private const int GenericExtendsToken = 0x0200_000E;
    private const int WideSlotToken = 0x0200_000F;

    private const int ObjectTypeReferenceToken = 0x0100_0001;
    private const int ValueTypeTypeReferenceToken = 0x0100_0002;

    private const int DeepChainLength = 70;
    private const int WideArity = 64;

    /// <summary>
    /// Proves the canonical-identity rule decides before every later rule and retains no base-chain edge and no
    /// variance position, and that one comparison was accounted for.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Identical_closed_types_are_assignable_by_identity()
    {
        var world = BuildWorld();
        var outcome = Decide(world, world.Request, world.Request);

        AssertAnswer(
            outcome,
            StaticFieldV2AssignabilityResultKind.Assignable,
            StaticFieldV2AssignabilityReason.Identity);
        Assert.Empty(outcome.BaseChainEdges);
        Assert.Empty(outcome.VariancePositions);
        Assert.Equal(1, outcome.ObservedCount);
        Assert.Null(outcome.ReachedBound);

        AssertAnswer(
            Decide(world, world.Primitive(MetadataPrimitiveTypeKind.Int32), world.Primitive(MetadataPrimitiveTypeKind.Int32)),
            StaticFieldV2AssignabilityResultKind.Assignable,
            StaticFieldV2AssignabilityReason.Identity);
        AssertAnswer(
            Decide(world, world.Nullable(world.Value), world.Nullable(world.Value)),
            StaticFieldV2AssignabilityResultKind.Assignable,
            StaticFieldV2AssignabilityReason.Identity);
    }

    /// <summary>
    /// Proves the System.Object core-role root admits every reference source in both its named and its primitive
    /// spelling, and that a value-typed source is refused because this draft slice models no boxing conversion.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Object_root_admits_reference_sources_and_refuses_value_sources()
    {
        var world = BuildWorld();

        AssertAnswer(
            Decide(world, world.Request, world.Object),
            StaticFieldV2AssignabilityResultKind.Assignable,
            StaticFieldV2AssignabilityReason.ObjectRoot);
        AssertAnswer(
            Decide(world, world.SzArray(world.Request), world.Object),
            StaticFieldV2AssignabilityResultKind.Assignable,
            StaticFieldV2AssignabilityReason.ObjectRoot);
        AssertAnswer(
            Decide(world, world.Request, world.Primitive(MetadataPrimitiveTypeKind.Object)),
            StaticFieldV2AssignabilityResultKind.Assignable,
            StaticFieldV2AssignabilityReason.ObjectRoot);
        AssertAnswer(
            Decide(world, world.Primitive(MetadataPrimitiveTypeKind.String), world.Object),
            StaticFieldV2AssignabilityResultKind.Assignable,
            StaticFieldV2AssignabilityReason.ObjectRoot);

        var boxed = Decide(world, world.Value, world.Object);
        AssertAnswer(
            boxed,
            StaticFieldV2AssignabilityResultKind.NotAssignable,
            StaticFieldV2AssignabilityReason.ValueSourceRequiresBoxing);
        Assert.Contains(
            StaticFieldV2AssignabilityCoverageBoundary.BoxingConversionNotModeled,
            boxed.DeclaredCoverageBoundaries);
        AssertAnswer(
            Decide(world, world.Value, world.Named(world.Core, ValueTypeToken)),
            StaticFieldV2AssignabilityResultKind.NotAssignable,
            StaticFieldV2AssignabilityReason.ValueSourceRequiresBoxing);
    }

    /// <summary>
    /// Proves a direct base edge and a two-hop base edge both admit through the bounded ancestry chain with their
    /// consulted levels retained most-derived-first, and that the reversed direction is a proven negative.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Direct_and_two_hop_base_edges_admit_and_reverse_directions_refuse()
    {
        var world = BuildWorld();

        var direct = Decide(world, world.DerivedNode, world.BaseNode);
        AssertAnswer(
            direct,
            StaticFieldV2AssignabilityResultKind.Assignable,
            StaticFieldV2AssignabilityReason.BaseChainDefinitionReached);
        Assert.Equal([0, 1], direct.BaseChainEdges.Select(static edge => edge.LevelIndex).ToArray());
        Assert.Equal(
            [DerivedNodeToken, BaseNodeToken],
            direct.BaseChainEdges.Select(static edge => edge.TypeDefinitionToken).ToArray());
        Assert.Equal([false, true], direct.BaseChainEdges.Select(static edge => edge.IsTargetDefinition).ToArray());
        Assert.Equal(BaseNodeToken, direct.RelatedMetadataToken);

        var twoHop = Decide(world, world.LeafNode, world.BaseNode);
        AssertAnswer(
            twoHop,
            StaticFieldV2AssignabilityResultKind.Assignable,
            StaticFieldV2AssignabilityReason.BaseChainDefinitionReached);
        Assert.Equal(
            [LeafNodeToken, DerivedNodeToken, BaseNodeToken],
            twoHop.BaseChainEdges.Select(static edge => edge.TypeDefinitionToken).ToArray());

        var reversed = Decide(world, world.BaseNode, world.DerivedNode);
        AssertAnswer(
            reversed,
            StaticFieldV2AssignabilityResultKind.NotAssignable,
            StaticFieldV2AssignabilityReason.DefinitionNotOnBaseChain);
        Assert.Equal(
            [BaseNodeToken, ObjectToken],
            reversed.BaseChainEdges.Select(static edge => edge.TypeDefinitionToken).ToArray());
        Assert.DoesNotContain(reversed.BaseChainEdges, static edge => edge.IsTargetDefinition);

        AssertAnswer(
            Decide(world, world.Request, world.BatchContext),
            StaticFieldV2AssignabilityResultKind.NotAssignable,
            StaticFieldV2AssignabilityReason.DefinitionNotOnBaseChain);
    }

    /// <summary>
    /// Proves an invariant argument position admits only canonically equal arguments while a covariant position of the
    /// same construction admits an assignable pair, and that every examined position is retained as draft evidence.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Invariant_positions_require_equality_beside_a_covariant_position()
    {
        var world = BuildWorld();

        var admitted = Decide(
            world,
            world.Named(world.Target, PairNodeToken, world.Request, world.DerivedNode),
            world.Named(world.Target, PairNodeToken, world.Request, world.BaseNode));
        AssertAnswer(
            admitted,
            StaticFieldV2AssignabilityResultKind.Assignable,
            StaticFieldV2AssignabilityReason.SameDefinitionArgumentsMatched);
        Assert.Equal([0, 1], admitted.VariancePositions.Select(static position => position.PositionIndex).ToArray());
        Assert.Equal(
            [StaticFieldV2GenericParameterVariance.Invariant, StaticFieldV2GenericParameterVariance.Covariant],
            admitted.VariancePositions.Select(static position => position.DeclaredVariance).ToArray());
        Assert.Equal(
            admitted.VariancePositions.Select(static position => position.DeclaredVariance).ToArray(),
            admitted.VariancePositions.Select(static position => position.EffectiveVariance).ToArray());
        Assert.All(admitted.VariancePositions, static position => Assert.True(position.IsAdmitted));
        Assert.Equal(world.Request, admitted.VariancePositions[0].SourceArgument);
        Assert.Equal(world.BaseNode, admitted.VariancePositions[1].TargetArgument);

        var refused = Decide(
            world,
            world.Named(world.Target, PairNodeToken, world.Request, world.BaseNode),
            world.Named(world.Target, PairNodeToken, world.BatchContext, world.BaseNode));
        AssertAnswer(
            refused,
            StaticFieldV2AssignabilityResultKind.NotAssignable,
            StaticFieldV2AssignabilityReason.InvariantArgumentMismatch);
        var decisive = Assert.Single(refused.VariancePositions);
        Assert.Equal(0, decisive.PositionIndex);
        Assert.False(decisive.IsAdmitted);
        Assert.Equal(decisive.GenericParameterToken, refused.RelatedMetadataToken);

        AssertAnswer(
            Decide(
                world,
                world.Named(world.Target, InvariantNodeToken, world.Request),
                world.Named(world.Target, InvariantNodeToken, world.BatchContext)),
            StaticFieldV2AssignabilityResultKind.NotAssignable,
            StaticFieldV2AssignabilityReason.InvariantArgumentMismatch);
    }

    /// <summary>
    /// Proves a covariant reference argument admits forward and refuses reversed, and that a contravariant reference
    /// argument admits exactly the opposite direction.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Covariant_and_contravariant_positions_admit_exactly_one_direction()
    {
        var world = BuildWorld();

        var covariant = Decide(
            world,
            world.Named(world.Target, CovariantNodeToken, world.Request),
            world.Named(world.Target, CovariantNodeToken, world.Object));
        AssertAnswer(
            covariant,
            StaticFieldV2AssignabilityResultKind.Assignable,
            StaticFieldV2AssignabilityReason.SameDefinitionArgumentsMatched);
        Assert.Equal(
            StaticFieldV2GenericParameterVariance.Covariant,
            Assert.Single(covariant.VariancePositions).EffectiveVariance);

        AssertAnswer(
            Decide(
                world,
                world.Named(world.Target, CovariantNodeToken, world.Object),
                world.Named(world.Target, CovariantNodeToken, world.Request)),
            StaticFieldV2AssignabilityResultKind.NotAssignable,
            StaticFieldV2AssignabilityReason.CovariantArgumentNotAssignable);

        var contravariant = Decide(
            world,
            world.Named(world.Target, ContravariantNodeToken, world.Object),
            world.Named(world.Target, ContravariantNodeToken, world.Request));
        AssertAnswer(
            contravariant,
            StaticFieldV2AssignabilityResultKind.Assignable,
            StaticFieldV2AssignabilityReason.SameDefinitionArgumentsMatched);
        Assert.Equal(
            StaticFieldV2GenericParameterVariance.Contravariant,
            Assert.Single(contravariant.VariancePositions).EffectiveVariance);

        AssertAnswer(
            Decide(
                world,
                world.Named(world.Target, ContravariantNodeToken, world.Request),
                world.Named(world.Target, ContravariantNodeToken, world.Object)),
            StaticFieldV2AssignabilityResultKind.NotAssignable,
            StaticFieldV2AssignabilityReason.ContravariantArgumentNotAssignable);
    }

    /// <summary>
    /// Proves the CLI rule that a value-type argument makes a declared variant position invariant, so a covariant
    /// position holding a value argument refuses a pair that a reference argument would have admitted.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Value_type_argument_makes_a_declared_variant_position_invariant()
    {
        var world = BuildWorld();

        var outcome = Decide(
            world,
            world.Named(world.Target, CovariantNodeToken, world.Value),
            world.Named(world.Target, CovariantNodeToken, world.Object));
        AssertAnswer(
            outcome,
            StaticFieldV2AssignabilityResultKind.NotAssignable,
            StaticFieldV2AssignabilityReason.ValueArgumentPositionInvariant);
        var position = Assert.Single(outcome.VariancePositions);
        Assert.Equal(StaticFieldV2GenericParameterVariance.Covariant, position.DeclaredVariance);
        Assert.Equal(StaticFieldV2GenericParameterVariance.Invariant, position.EffectiveVariance);
        Assert.Equal(CovariantVarianceFlag, position.Parameter.Flags);
        Assert.False(position.IsAdmitted);

        var contravariant = Decide(
            world,
            world.Named(world.Target, ContravariantNodeToken, world.Object),
            world.Named(world.Target, ContravariantNodeToken, world.Value));
        AssertAnswer(
            contravariant,
            StaticFieldV2AssignabilityResultKind.NotAssignable,
            StaticFieldV2AssignabilityReason.ValueArgumentPositionInvariant);
        Assert.Equal(
            ContravariantVarianceFlag,
            Assert.Single(contravariant.VariancePositions).Parameter.Flags);
    }

    /// <summary>
    /// Proves vector and equal-rank matrix covariance over reference elements, that unequal multidimensional ranks are
    /// refused, and that equal value elements admit only through canonical element equality.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Array_covariance_requires_equal_rank_and_reference_elements()
    {
        var world = BuildWorld();

        AssertAnswer(
            Decide(world, world.SzArray(world.Request), world.SzArray(world.Object)),
            StaticFieldV2AssignabilityResultKind.Assignable,
            StaticFieldV2AssignabilityReason.ArrayElementCovariance);
        AssertAnswer(
            Decide(world, world.Matrix(world.Request, 2), world.Matrix(world.Object, 2)),
            StaticFieldV2AssignabilityResultKind.Assignable,
            StaticFieldV2AssignabilityReason.ArrayElementCovariance);
        AssertAnswer(
            Decide(world, world.SzArray(world.DerivedNode), world.SzArray(world.BaseNode)),
            StaticFieldV2AssignabilityResultKind.Assignable,
            StaticFieldV2AssignabilityReason.ArrayElementCovariance);

        AssertAnswer(
            Decide(world, world.Matrix(world.Request, 2), world.Matrix(world.Object, 3)),
            StaticFieldV2AssignabilityResultKind.NotAssignable,
            StaticFieldV2AssignabilityReason.ArrayRankMismatch);
        AssertAnswer(
            Decide(world, world.Matrix(world.Request, 3), world.Matrix(world.Object, 2)),
            StaticFieldV2AssignabilityResultKind.NotAssignable,
            StaticFieldV2AssignabilityReason.ArrayRankMismatch);

        AssertAnswer(
            Decide(world, world.SzArray(world.Object), world.SzArray(world.Request)),
            StaticFieldV2AssignabilityResultKind.NotAssignable,
            StaticFieldV2AssignabilityReason.DefinitionNotOnBaseChain);
    }

    /// <summary>
    /// Proves the deliberate W8.1 divergence from the pinned runtime.
    /// </summary>
    /// <remarks>
    /// The pinned runtime reports a positive result when the target is a rank-one multidimensional array and the
    /// source is an SZ array with a compatible reference element. W8 does not inherit that asymmetry: array
    /// assignability requires equal rank <em>and</em> equal SZ-versus-multidimensional topology before any recursive
    /// reference-element assignability, so this exact pair is refused here and the divergence is retained as the
    /// declared <see cref="StaticFieldV2AssignabilityCoverageBoundary.SzToRankOneMultidimensionalDivergenceFromRuntime"/>
    /// boundary. The reverse direction, on which the runtime and W8 agree, is refused by the same rule.
    /// </remarks>
    [Fact]
    [Trait("Category", "Fast")]
    public void Sz_source_with_rank_one_multidimensional_target_is_the_deliberate_runtime_divergence()
    {
        var world = BuildWorld();

        var divergent = Decide(world, world.SzArray(world.Request), world.Matrix(world.Object, 1));
        AssertAnswer(
            divergent,
            StaticFieldV2AssignabilityResultKind.NotAssignable,
            StaticFieldV2AssignabilityReason.ArrayTopologyMismatch);
        Assert.Contains(
            StaticFieldV2AssignabilityCoverageBoundary.SzToRankOneMultidimensionalDivergenceFromRuntime,
            divergent.DeclaredCoverageBoundaries);
        Assert.Equal(1, world.Matrix(world.Object, 1).ArrayRank);
        Assert.Equal(1, world.SzArray(world.Request).ArrayRank);
        Assert.NotEqual(world.SzArray(world.Object).Kind, world.Matrix(world.Object, 1).Kind);
        Assert.True(world.Request.IsReferenceType);

        var reversed = Decide(world, world.Matrix(world.Object, 1), world.SzArray(world.Request));
        AssertAnswer(
            reversed,
            StaticFieldV2AssignabilityResultKind.NotAssignable,
            StaticFieldV2AssignabilityReason.ArrayTopologyMismatch);

        AssertAnswer(
            Decide(world, world.SzArray(world.Request), world.Matrix(world.Object, 2)),
            StaticFieldV2AssignabilityResultKind.NotAssignable,
            StaticFieldV2AssignabilityReason.ArrayTopologyMismatch);
        AssertAnswer(
            Decide(world, world.Matrix(world.Object, 2), world.SzArray(world.Request)),
            StaticFieldV2AssignabilityResultKind.NotAssignable,
            StaticFieldV2AssignabilityReason.ArrayTopologyMismatch);
    }

    /// <summary>
    /// Proves value elements never gain covariance in either array topology and that two arrays of the same rank whose
    /// value elements are canonically equal admit through element equality rather than through a covariance edge.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Value_elements_never_gain_array_covariance()
    {
        var world = BuildWorld();

        AssertAnswer(
            Decide(world, world.SzArray(world.Value), world.SzArray(world.Object)),
            StaticFieldV2AssignabilityResultKind.NotAssignable,
            StaticFieldV2AssignabilityReason.ArrayValueElementMismatch);
        AssertAnswer(
            Decide(world, world.SzArray(world.Value), world.SzArray(world.Point)),
            StaticFieldV2AssignabilityResultKind.NotAssignable,
            StaticFieldV2AssignabilityReason.ArrayValueElementMismatch);
        AssertAnswer(
            Decide(
                world,
                world.SzArray(world.Primitive(MetadataPrimitiveTypeKind.Int32)),
                world.SzArray(world.Primitive(MetadataPrimitiveTypeKind.Int64))),
            StaticFieldV2AssignabilityResultKind.NotAssignable,
            StaticFieldV2AssignabilityReason.ArrayValueElementMismatch);

        AssertAnswer(
            Decide(world, world.Matrix(world.Value, 2, [3, 3]), world.Matrix(world.Value, 2)),
            StaticFieldV2AssignabilityResultKind.Assignable,
            StaticFieldV2AssignabilityReason.ArrayElementEqual);
    }

    /// <summary>
    /// Proves nullable and primitive closed types admit only canonical equality, and that the declared
    /// boxing-conversion boundary is retained rather than silently widening either rule.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Nullable_and_primitive_types_require_canonical_equality()
    {
        var world = BuildWorld();

        var nullable = Decide(world, world.Nullable(world.Value), world.Nullable(world.Point));
        AssertAnswer(
            nullable,
            StaticFieldV2AssignabilityResultKind.NotAssignable,
            StaticFieldV2AssignabilityReason.NullableNotEqual);
        Assert.Contains(
            StaticFieldV2AssignabilityCoverageBoundary.BoxingConversionNotModeled,
            nullable.DeclaredCoverageBoundaries);

        AssertAnswer(
            Decide(
                world,
                world.Primitive(MetadataPrimitiveTypeKind.Int32),
                world.Primitive(MetadataPrimitiveTypeKind.Int64)),
            StaticFieldV2AssignabilityResultKind.NotAssignable,
            StaticFieldV2AssignabilityReason.PrimitiveNotEqual);
        AssertAnswer(
            Decide(world, world.Nullable(world.Value), world.Object),
            StaticFieldV2AssignabilityResultKind.NotAssignable,
            StaticFieldV2AssignabilityReason.ValueSourceRequiresBoxing);
        AssertAnswer(
            Decide(world, world.Primitive(MetadataPrimitiveTypeKind.Int32), world.SzArray(world.Object)),
            StaticFieldV2AssignabilityResultKind.NotAssignable,
            StaticFieldV2AssignabilityReason.ClosedTypeKindMismatch);
    }

    /// <summary>
    /// Proves the declared boundary of this draft slice: an interface target that no class base chain reaches is
    /// unprovable rather than a proven negative, exactly as the closed-construction binder treats an interface
    /// constraint target, and that a generic base definition reached on the chain is likewise unprovable.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Interface_and_generic_base_targets_are_unprovable_rather_than_negative()
    {
        var world = BuildWorld();

        var carrier = Decide(
            world,
            world.Named(world.Target, CarrierToken, world.Request),
            world.Named(world.Target, InvariantNodeToken, world.Request));
        AssertAnswer(
            carrier,
            StaticFieldV2AssignabilityResultKind.Unprovable,
            StaticFieldV2AssignabilityReason.InterfaceTargetNotModeled);
        Assert.Contains(
            StaticFieldV2AssignabilityCoverageBoundary.InterfaceImplementationNotModeled,
            carrier.DeclaredCoverageBoundaries);
        Assert.Equal(InvariantNodeToken, carrier.RelatedMetadataToken);
        Assert.NotEmpty(carrier.BaseChainEdges);
        Assert.DoesNotContain(carrier.BaseChainEdges, static edge => edge.IsTargetDefinition);

        AssertAnswer(
            Decide(world, world.Request, world.Named(world.Target, CovariantNodeToken, world.Object)),
            StaticFieldV2AssignabilityResultKind.Unprovable,
            StaticFieldV2AssignabilityReason.InterfaceTargetNotModeled);

        var genericBase = Decide(
            world,
            world.Named(world.Target, GenericExtendsToken),
            world.Named(world.Target, CarrierToken, world.Request));
        AssertAnswer(
            genericBase,
            StaticFieldV2AssignabilityResultKind.Unprovable,
            StaticFieldV2AssignabilityReason.GenericBaseInstantiationNotModeled);
        Assert.Contains(
            StaticFieldV2AssignabilityCoverageBoundary.GenericBaseInstantiationNotModeled,
            genericBase.DeclaredCoverageBoundaries);
        Assert.Contains(genericBase.BaseChainEdges, static edge => edge.IsTargetDefinition);
    }

    /// <summary>
    /// Proves both declared draft bounds stop prefix-free at cap plus one: the bounded ancestry chain that never
    /// reaches the target definition, and the cumulative comparison total, which deliberately reuses the declared
    /// constructed-ancestry-depth bound name and value.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Ancestry_depth_and_comparison_bounds_stop_at_cap_plus_one()
    {
        const int cap = StaticFieldV2AssignabilityOutcome.MaximumAncestryDepth;
        Assert.Equal(cap, StaticFieldV2AssignabilityOutcome.MaximumComparisonCount);

        var deep = BuildDeepWorld();
        var depth = Decide(deep.Ancestry, deep.Deepest, deep.Unrelated);
        AssertStop(
            depth,
            StaticFieldV2AssignabilityResultKind.NonExact,
            StaticFieldV2AssignabilityReason.AncestryDepthBoundReached);
        var depthBound = Assert.IsType<EvaluationDeterministicBound>(depth.ReachedBound);
        Assert.Equal("expression-v2.metadata.ancestry-depth", depthBound.Name);
        Assert.Equal(cap, depthBound.Value);
        Assert.Equal(cap + 1, depth.ObservedCount);

        var world = BuildWorld();
        var wide = Decide(world, world.WideSlot(world.Request), world.WideSlot(world.BatchContext));
        AssertStop(
            wide,
            StaticFieldV2AssignabilityResultKind.NonExact,
            StaticFieldV2AssignabilityReason.ComparisonBoundReached);
        var comparisonBound = Assert.IsType<EvaluationDeterministicBound>(wide.ReachedBound);
        Assert.Equal(depthBound.Name, comparisonBound.Name);
        Assert.Equal(cap, comparisonBound.Value);
        Assert.Equal(cap + 1, wide.ObservedCount);
    }

    /// <summary>
    /// Proves each non-exact and invalid prerequisite propagates as a typed prefix-free draft stop that retains no
    /// edge, no variance position, and no related token, and that every issuer argument is null-checked.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Prerequisite_stops_are_typed_and_prefix_free()
    {
        var world = BuildWorld();

        var coreless = W8MetadataAncestryAuthorityContractTests.BuildAncestryWorld(
            W8MetadataAncestryAuthorityContractTests.BuildCustomModule(
                "Synthetic.NoCore",
                0xA400,
                'd',
                [new W8MetadataAncestryAuthorityContractTests.TypeRow(
                    "Synthetic.NoCore", "Loner", PublicClassAttributes, null)]));
        Assert.Equal(MetadataAncestryAuthorityPortfolioResultKind.NonExact, coreless.Ancestry.ResultKind);
        AssertStop(
            StaticFieldV2AssignabilityBinder.IsAssignable(StaticFieldV2AssignabilityRequest.Create(
                world.DerivedNode,
                world.BaseNode,
                coreless.Ancestry)),
            StaticFieldV2AssignabilityResultKind.NonExact,
            StaticFieldV2AssignabilityReason.AncestryPortfolioNonExact);

        var twoCores = W8MetadataAncestryAuthorityContractTests.BuildAncestryWorld(
            W8MetadataAncestryAuthorityContractTests.BuildCoreModule(),
            W8MetadataAncestryAuthorityContractTests.BuildCoreModule("Synthetic.Core2", 0xA500, 'e'));
        Assert.Equal(MetadataAncestryAuthorityPortfolioResultKind.Invalid, twoCores.Ancestry.ResultKind);
        AssertStop(
            StaticFieldV2AssignabilityBinder.IsAssignable(StaticFieldV2AssignabilityRequest.Create(
                world.DerivedNode,
                world.BaseNode,
                twoCores.Ancestry)),
            StaticFieldV2AssignabilityResultKind.Invalid,
            StaticFieldV2AssignabilityReason.AncestryPortfolioInvalid);

        var stranger = W8MetadataAncestryAuthorityContractTests.BuildAncestryWorld(
            W8MetadataAncestryAuthorityContractTests.BuildCoreModule("Synthetic.Core", 0xA600, 'f'));
        Assert.Equal(MetadataAncestryAuthorityPortfolioResultKind.Exact, stranger.Ancestry.ResultKind);
        AssertStop(
            StaticFieldV2AssignabilityBinder.IsAssignable(StaticFieldV2AssignabilityRequest.Create(
                world.DerivedNode,
                world.BaseNode,
                stranger.Ancestry)),
            StaticFieldV2AssignabilityResultKind.Invalid,
            StaticFieldV2AssignabilityReason.SourceDefinitionNotIssued);

        Assert.Throws<ArgumentNullException>(() => StaticFieldV2AssignabilityBinder.IsAssignable(null!));
        Assert.Throws<ArgumentNullException>(() =>
            StaticFieldV2AssignabilityRequest.Create(null!, world.BaseNode, world.Ancestry));
        Assert.Throws<ArgumentNullException>(() =>
            StaticFieldV2AssignabilityRequest.Create(world.DerivedNode, null!, world.Ancestry));
        Assert.Throws<ArgumentNullException>(() =>
            StaticFieldV2AssignabilityRequest.Create(world.DerivedNode, world.BaseNode, null!));
    }

    /// <summary>
    /// Proves canonical replay equality against a frozen golden digest, defensive copies for every returned draft
    /// array, guarded private issuance for both retained row kinds, the closed public issuer surface, and emitted
    /// draft XML documentation.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Assignability_contracts_are_replayable_immutable_guarded_and_documented()
    {
        var outcome = GoldenOutcome();
        var replay = GoldenOutcome();

        Assert.Equal(outcome, replay);
        Assert.Equal(outcome.GetHashCode(), replay.GetHashCode());
        Assert.Equal(outcome.Sha256, replay.Sha256);
        Assert.Equal(
            "f67af7e28fde51225fe6c09ff4bc32defb4a86dd218f3382ada9f97bb8861acc",
            outcome.Sha256);
        Assert.Equal(
            [
                StaticFieldV2AssignabilityCoverageBoundary.InterfaceImplementationNotModeled,
                StaticFieldV2AssignabilityCoverageBoundary.BoxingConversionNotModeled,
                StaticFieldV2AssignabilityCoverageBoundary.SzToRankOneMultidimensionalDivergenceFromRuntime,
            ],
            outcome.DeclaredCoverageBoundaries.ToArray());

        var originalBytes = outcome.CanonicalBytes;
        var originalEdges = outcome.BaseChainEdges;
        var originalPositions = outcome.VariancePositions;
        var originalBoundaries = outcome.DeclaredCoverageBoundaries;
        var edge = originalEdges[0];
        var position = originalPositions[0];
        var originalEdgeBytes = edge.CanonicalBytes;
        var originalPositionBytes = position.CanonicalBytes;
        var originalRequestBytes = outcome.Request.CanonicalBytes;

        ImmutableCollectionsMarshal.AsArray(outcome.CanonicalBytes)![0] ^= 0x5A;
        ImmutableCollectionsMarshal.AsArray(outcome.BaseChainEdges)![0] = null!;
        ImmutableCollectionsMarshal.AsArray(outcome.VariancePositions)![0] = null!;
        ImmutableCollectionsMarshal.AsArray(outcome.DeclaredCoverageBoundaries)![0] = default;
        ImmutableCollectionsMarshal.AsArray(edge.CanonicalBytes)![0] ^= 0x33;
        ImmutableCollectionsMarshal.AsArray(position.CanonicalBytes)![0] ^= 0x33;
        ImmutableCollectionsMarshal.AsArray(outcome.Request.CanonicalBytes)![0] ^= 0x33;

        Assert.True(originalBytes.AsSpan().SequenceEqual(outcome.CanonicalBytes.AsSpan()));
        Assert.True(originalEdgeBytes.AsSpan().SequenceEqual(edge.CanonicalBytes.AsSpan()));
        Assert.True(originalPositionBytes.AsSpan().SequenceEqual(position.CanonicalBytes.AsSpan()));
        Assert.True(originalRequestBytes.AsSpan().SequenceEqual(outcome.Request.CanonicalBytes.AsSpan()));
        Assert.Equal(originalEdges[0], outcome.BaseChainEdges[0]);
        Assert.Equal(originalPositions[0], outcome.VariancePositions[0]);
        Assert.Equal(originalBoundaries[0], outcome.DeclaredCoverageBoundaries[0]);
        Assert.Equal(outcome.Sha256, replay.Sha256);

        Assert.False(StaticFieldV2AssignabilityOutcome.OwnsRowMintCapability(new object()));
        Assert.Throws<ArgumentException>(() => StaticFieldV2AssignabilityBaseChainEdgeIdentity.Create(
            new object(),
            edge.LevelIndex,
            edge.SourceModule,
            edge.TypeDefinition,
            edge.IsTargetDefinition));
        Assert.Throws<ArgumentException>(() => StaticFieldV2AssignabilityVariancePositionIdentity.Create(
            new object(),
            position.PositionIndex,
            position.Parameter,
            position.DeclaredVariance,
            position.EffectiveVariance,
            position.SourceArgument,
            position.TargetArgument,
            position.IsAdmitted));

        var publicTypes = new[]
        {
            typeof(StaticFieldV2AssignabilityResultKind),
            typeof(StaticFieldV2AssignabilityReason),
            typeof(StaticFieldV2GenericParameterVariance),
            typeof(StaticFieldV2AssignabilityCoverageBoundary),
            typeof(StaticFieldV2AssignabilityBaseChainEdgeIdentity),
            typeof(StaticFieldV2AssignabilityVariancePositionIdentity),
            typeof(StaticFieldV2AssignabilityRequest),
            typeof(StaticFieldV2AssignabilityOutcome),
            typeof(StaticFieldV2AssignabilityBinder),
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
            if (type == typeof(StaticFieldV2AssignabilityBinder))
            {
                Assert.Equal(["IsAssignable"], publicStatics);
            }
            else if (type == typeof(StaticFieldV2AssignabilityRequest))
            {
                Assert.Equal(["Create"], publicStatics);
            }
            else if (!type.IsEnum)
            {
                Assert.Empty(publicStatics);
            }
        }
        AssertPublicDraftXml(publicTypes);
    }

    private static StaticFieldV2AssignabilityOutcome GoldenOutcome()
    {
        var world = BuildWorld();
        return Decide(
            world,
            world.Named(world.Target, PairNodeToken, world.Request, world.DerivedNode),
            world.Named(world.Target, PairNodeToken, world.Request, world.BaseNode));
    }

    private static StaticFieldV2AssignabilityOutcome Decide(
        AssignabilityWorld world,
        MetadataClosedTypeIdentity source,
        MetadataClosedTypeIdentity target) =>
        Decide(world.Ancestry, source, target);

    private static StaticFieldV2AssignabilityOutcome Decide(
        MetadataAncestryAuthorityPortfolioIdentity ancestry,
        MetadataClosedTypeIdentity source,
        MetadataClosedTypeIdentity target) =>
        StaticFieldV2AssignabilityBinder.IsAssignable(
            StaticFieldV2AssignabilityRequest.Create(source, target, ancestry));

    private static void AssertAnswer(
        StaticFieldV2AssignabilityOutcome outcome,
        StaticFieldV2AssignabilityResultKind resultKind,
        StaticFieldV2AssignabilityReason reason)
    {
        Assert.Equal(resultKind, outcome.ResultKind);
        Assert.Equal(reason, outcome.Reason);
        Assert.Null(outcome.ReachedBound);
        Assert.Contains(
            StaticFieldV2AssignabilityCoverageBoundary.InterfaceImplementationNotModeled,
            outcome.DeclaredCoverageBoundaries);
    }

    private static void AssertStop(
        StaticFieldV2AssignabilityOutcome outcome,
        StaticFieldV2AssignabilityResultKind resultKind,
        StaticFieldV2AssignabilityReason reason)
    {
        Assert.Equal(resultKind, outcome.ResultKind);
        Assert.Equal(reason, outcome.Reason);
        Assert.Empty(outcome.BaseChainEdges);
        Assert.Empty(outcome.VariancePositions);
    }

    private static AssignabilityWorld BuildWorld()
    {
        var core = BuildCoreModule();
        var target = BuildTargetModule();
        var world = W8MetadataAncestryAuthorityContractTests.BuildAncestryWorld(core, target);
        Assert.Equal(MetadataAncestryAuthorityPortfolioResultKind.Exact, world.Ancestry.ResultKind);
        return new AssignabilityWorld(core.Module, target.Module, world.Ancestry);
    }

    private static DeepWorld BuildDeepWorld()
    {
        var core = BuildCoreModule();
        var rows = ImmutableArray.CreateBuilder<W8MetadataAncestryAuthorityContractTests.TypeRow>(
            DeepChainLength + 1);
        for (var index = 0; index < DeepChainLength; index++)
        {
            rows.Add(new W8MetadataAncestryAuthorityContractTests.TypeRow(
                "Synthetic.Deep",
                $"Deep{index}",
                PublicClassAttributes,
                index == 0 ? ObjectTypeReferenceToken : 0x0200_0002 + index - 1));
        }
        rows.Add(new W8MetadataAncestryAuthorityContractTests.TypeRow(
            "Synthetic.Deep",
            "Unrelated",
            PublicClassAttributes,
            ObjectTypeReferenceToken));

        var deep = W8MetadataAncestryAuthorityContractTests.BuildCustomModule(
            "Synthetic.DeepAssembly",
            0xA700,
            'c',
            rows.MoveToImmutable(),
            typeReferences: module =>
            [
                W8MetadataAncestryAuthorityContractTests.TypeReferenceRow(
                    module, 1, "System", "Object", 0x2300_0001),
            ],
            assemblyReferences: module =>
            [
                W8MetadataAncestryAuthorityContractTests.AssemblyReferenceRow(module, 1, CoreAssemblyName),
            ]);
        var world = W8MetadataAncestryAuthorityContractTests.BuildAncestryWorld(core, deep);
        Assert.Equal(MetadataAncestryAuthorityPortfolioResultKind.Exact, world.Ancestry.ResultKind);

        var deepest = 0x0200_0002 + DeepChainLength - 1;
        var chain = world.Ancestry.ExactAncestryChainOrDefault(deep.Module, deepest);
        Assert.Equal(
            MetadataAncestryChainTerminalKind.DepthBoundReached,
            Assert.IsType<MetadataTypeDefinitionAncestryChainIdentity>(chain).TerminalKind);
        return new DeepWorld(deep.Module, world.Ancestry, deepest, 0x0200_0002 + DeepChainLength);
    }

    private static W8MetadataAncestryAuthorityContractTests.AncestryModule BuildCoreModule() =>
        W8MetadataAncestryAuthorityContractTests.BuildCustomModule(
            CoreAssemblyName,
            0xA100,
            'a',
            [
                new W8MetadataAncestryAuthorityContractTests.TypeRow(
                    "System", "Object", PublicClassAttributes, null),
                new W8MetadataAncestryAuthorityContractTests.TypeRow(
                    "System", "ValueType", PublicClassAttributes, ObjectToken),
                new W8MetadataAncestryAuthorityContractTests.TypeRow(
                    "System", "Enum", PublicClassAttributes, ValueTypeToken),
                new W8MetadataAncestryAuthorityContractTests.TypeRow(
                    "System", "Delegate", PublicClassAttributes, ObjectToken),
                new W8MetadataAncestryAuthorityContractTests.TypeRow(
                    "System", "MulticastDelegate", PublicClassAttributes, 0x0200_0005),
                new W8MetadataAncestryAuthorityContractTests.TypeRow(
                    "System", "Nullable`1", PublicClassAttributes, ValueTypeToken, GenericArity: 1),
            ]);

    private static W8MetadataAncestryAuthorityContractTests.AncestryModule BuildTargetModule() =>
        BuildVarianceModule(
            TargetAssemblyName,
            0xA200,
            'b',
            [
                new VarianceTypeRow(TargetNamespace, "RequestContext", PublicClassAttributes, ObjectTypeReferenceToken),
                new VarianceTypeRow(TargetNamespace, "BatchContext", PublicClassAttributes, ObjectTypeReferenceToken),
                new VarianceTypeRow(
                    TargetNamespace, "ValueContext", PublicClassAttributes, ValueTypeTypeReferenceToken),
                new VarianceTypeRow(TargetNamespace, "PointContext", PublicClassAttributes, ValueTypeTypeReferenceToken),
                new VarianceTypeRow(
                    TargetNamespace, "AssignabilityBaseNode", PublicClassAttributes, ObjectTypeReferenceToken),
                new VarianceTypeRow(
                    TargetNamespace, "AssignabilityDerivedNode", PublicClassAttributes, BaseNodeToken),
                new VarianceTypeRow(
                    TargetNamespace, "AssignabilityLeafNode", PublicClassAttributes, DerivedNodeToken),
                new VarianceTypeRow(
                    TargetNamespace, "IInvariantNode`1", InterfaceAttributes, null, [0]),
                new VarianceTypeRow(
                    TargetNamespace, "ICovariantNode`1", InterfaceAttributes, null, [CovariantVarianceFlag]),
                new VarianceTypeRow(
                    TargetNamespace, "IContravariantNode`1", InterfaceAttributes, null, [ContravariantVarianceFlag]),
                new VarianceTypeRow(
                    TargetNamespace,
                    "AssignabilityCarrier`1",
                    PublicClassAttributes,
                    ObjectTypeReferenceToken,
                    [0]),
                new VarianceTypeRow(
                    TargetNamespace, "IPairNode`2", InterfaceAttributes, null, [0, CovariantVarianceFlag]),
                new VarianceTypeRow(
                    TargetNamespace, "GenericExtendsNode", PublicClassAttributes, CarrierToken),
                new VarianceTypeRow(
                    TargetNamespace,
                    $"WideSlot`{WideArity}",
                    PublicClassAttributes,
                    ObjectTypeReferenceToken,
                    [.. Enumerable.Repeat(0, WideArity)]),
            ]);

    private static W8MetadataAncestryAuthorityContractTests.AncestryModule BuildVarianceModule(
        string assemblyName,
        ulong moduleAddress,
        char digestCharacter,
        ImmutableArray<VarianceTypeRow> namedTypes)
    {
        var module = W8CompilerNameMappingContractTests.CreateMetadataModule(
            moduleAddress,
            digestCharacter,
            assemblyName);
        var typeReferenceRows = ImmutableArray.Create(
            W8MetadataAncestryAuthorityContractTests.TypeReferenceRow(module, 1, "System", "Object", 0x2300_0001),
            W8MetadataAncestryAuthorityContractTests.TypeReferenceRow(module, 2, "System", "ValueType", 0x2300_0001));
        var assemblyReferenceRows = ImmutableArray.Create(
            W8MetadataAncestryAuthorityContractTests.AssemblyReferenceRow(module, 1, CoreAssemblyName));

        var genericParameterRows = ImmutableArray.CreateBuilder<MetadataGenericParameterRowObservationIdentity>();
        for (var index = 0; index < namedTypes.Length; index++)
        {
            var row = namedTypes[index];
            var ownerToken = 0x0200_0002 + index;
            for (var number = 0; number < row.VarianceFlags.Length; number++)
            {
                genericParameterRows.Add(MetadataGenericParameterRowObservationIdentity.Create(
                    module,
                    0x2A00_0000 | checked(genericParameterRows.Count + 1),
                    number,
                    flags: row.VarianceFlags[number],
                    ownerMetadataToken: ownerToken,
                    name: $"T{ownerToken & 0x00FF_FFFF}_{number}"));
            }
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
                typeReferenceRowCount: typeReferenceRows.Length,
                typeSpecificationRowCount: 0,
                assemblyReferenceRowCount: assemblyReferenceRows.Length,
                nestedClassRowCount: 0,
                genericParameterRowCount: genericParameterRows.Count));

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
            ImmutableArray<MetadataNestedClassRowObservationIdentity>.Empty);
        var genericParameters = MetadataGenericParameterPhysicalTableCatalogIdentity.Create(
            sourceEnds,
            genericParameterRows.ToImmutable());
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
            MetadataTypeReferencePhysicalTableCatalogIdentity.Create(referenceEnds, typeReferenceRows),
            MetadataModuleReferencePhysicalTableCatalogIdentity.Create(
                referenceEnds,
                ImmutableArray<MetadataModuleReferenceRowObservationIdentity>.Empty),
            MetadataTypeSpecificationPhysicalTableCatalogIdentity.Create(
                referenceEnds,
                ImmutableArray<MetadataTypeSpecificationRowObservationIdentity>.Empty),
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
        return new W8MetadataAncestryAuthorityContractTests.AncestryModule(
            module,
            compatibility,
            chainCatalog,
            tables);
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
        var assembly = typeof(StaticFieldV2AssignabilityBinder).Assembly;
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

    private sealed record VarianceTypeRow(
        string NamespaceName,
        string TypeName,
        int TypeAttributes,
        int? ExtendsMetadataToken,
        ImmutableArray<int> VarianceFlags = default)
    {
        internal ImmutableArray<int> VarianceFlags { get; } =
            VarianceFlags.IsDefault ? ImmutableArray<int>.Empty : VarianceFlags;
    }

    private sealed record DeepWorld(
        StaticFieldMetadataModuleIdentity Module,
        MetadataAncestryAuthorityPortfolioIdentity Ancestry,
        int DeepestToken,
        int UnrelatedToken)
    {
        internal MetadataClosedTypeIdentity Deepest => Named(DeepestToken);

        internal MetadataClosedTypeIdentity Unrelated => Named(UnrelatedToken);

        private MetadataClosedTypeIdentity Named(int token) =>
            MetadataClosedTypeIdentity.ConstructNamed(
                [Assert.IsType<MetadataTypeDefinitionSemanticClassificationIdentity>(
                    Ancestry.ExactClassificationOrDefault(Module, token))],
                ImmutableArray<MetadataClosedTypeIdentity>.Empty);
    }

    private sealed record AssignabilityWorld(
        StaticFieldMetadataModuleIdentity Core,
        StaticFieldMetadataModuleIdentity Target,
        MetadataAncestryAuthorityPortfolioIdentity Ancestry)
    {
        internal MetadataClosedTypeIdentity Object => Named(Core, ObjectToken);

        internal MetadataClosedTypeIdentity Request => Named(Target, RequestToken);

        internal MetadataClosedTypeIdentity BatchContext => Named(Target, BatchToken);

        internal MetadataClosedTypeIdentity Value => Named(Target, ValueToken);

        internal MetadataClosedTypeIdentity Point => Named(Target, PointToken);

        internal MetadataClosedTypeIdentity BaseNode => Named(Target, BaseNodeToken);

        internal MetadataClosedTypeIdentity DerivedNode => Named(Target, DerivedNodeToken);

        internal MetadataClosedTypeIdentity LeafNode => Named(Target, LeafNodeToken);

        internal MetadataClosedTypeIdentity Named(
            StaticFieldMetadataModuleIdentity module,
            int typeDefinitionToken,
            params MetadataClosedTypeIdentity[] arguments) =>
            MetadataClosedTypeIdentity.ConstructNamed(
                [Assert.IsType<MetadataTypeDefinitionSemanticClassificationIdentity>(
                    Ancestry.ExactClassificationOrDefault(module, typeDefinitionToken))],
                [.. arguments]);

        internal MetadataClosedTypeIdentity Primitive(MetadataPrimitiveTypeKind kind) =>
            MetadataClosedTypeIdentity.Primitive(kind);

        internal MetadataClosedTypeIdentity Nullable(MetadataClosedTypeIdentity element) =>
            Named(Core, NullableToken, element);

        internal MetadataClosedTypeIdentity SzArray(MetadataClosedTypeIdentity element) =>
            MetadataClosedTypeIdentity.SzArray(element);

        internal MetadataClosedTypeIdentity Matrix(
            MetadataClosedTypeIdentity element,
            int rank,
            ImmutableArray<int> sizes = default) =>
            MetadataClosedTypeIdentity.MultidimensionalArray(
                element,
                rank,
                sizes.IsDefault ? ImmutableArray<int>.Empty : sizes,
                ImmutableArray<int>.Empty);

        internal MetadataClosedTypeIdentity WideSlot(MetadataClosedTypeIdentity last)
        {
            var arguments = ImmutableArray.CreateBuilder<MetadataClosedTypeIdentity>(WideArity);
            for (var index = 0; index < WideArity - 1; index++)
            {
                arguments.Add(Object);
            }
            arguments.Add(last);
            return Named(Target, WideSlotToken, [.. arguments]);
        }
    }
}
