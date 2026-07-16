using Interpreter.Core.Abstractions;
using Interpreter.Domain.Concrete;
using Xunit;
using ModuleHandle = Interpreter.Core.Abstractions.ModuleHandle;

namespace Interpreter.Tests;

/// <summary>
/// Verifies structural type identity, deterministic defaults, and the algebraic laws of the concrete validation
/// domain.
/// </summary>
public sealed class ConcreteDomainLawTests
{
    private readonly ConcreteDomain domain = new();

    /// <summary>Checks reflexivity, antisymmetry, and transitivity over representative lifted-flat values.</summary>
    [Fact]
    public void LiftedFlatInt32DomainSatisfiesPartialOrderLaws()
    {
        var values = Values();

        foreach (var value in values)
        {
            Assert.True(domain.IsLessThanOrEqual(value, value));
        }

        foreach (var left in values)
        {
            foreach (var right in values)
            {
                if (domain.IsLessThanOrEqual(left, right) && domain.IsLessThanOrEqual(right, left))
                {
                    Assert.Equal(left, right);
                }

                foreach (var third in values)
                {
                    if (domain.IsLessThanOrEqual(left, right) && domain.IsLessThanOrEqual(right, third))
                    {
                        Assert.True(domain.IsLessThanOrEqual(left, third));
                    }
                }
            }
        }
    }

    /// <summary>Checks that join is a lawful least upper bound independent of operand order and grouping.</summary>
    [Fact]
    public void JoinIsACommutativeAssociativeIdempotentLeastUpperBound()
    {
        var values = Values();
        foreach (var left in values)
        {
            Assert.Equal(left, domain.Join(left, left));
            foreach (var right in values)
            {
                var join = domain.Join(left, right);
                Assert.Equal(join, domain.Join(right, left));
                Assert.True(domain.IsLessThanOrEqual(left, join));
                Assert.True(domain.IsLessThanOrEqual(right, join));

                foreach (var third in values)
                {
                    Assert.Equal(
                        domain.Join(domain.Join(left, right), third),
                        domain.Join(left, domain.Join(right, third)));
                    if (domain.IsLessThanOrEqual(left, third) && domain.IsLessThanOrEqual(right, third))
                    {
                        Assert.True(domain.IsLessThanOrEqual(join, third));
                    }
                }
            }
        }
    }

    /// <summary>Checks that meet is a lawful greatest lower bound independent of operand order and grouping.</summary>
    [Fact]
    public void MeetIsACommutativeAssociativeIdempotentGreatestLowerBound()
    {
        var values = Values();
        foreach (var left in values)
        {
            Assert.Equal(left, domain.Meet(left, left));
            foreach (var right in values)
            {
                var meet = domain.Meet(left, right);
                Assert.Equal(meet, domain.Meet(right, left));
                Assert.True(domain.IsLessThanOrEqual(meet, left));
                Assert.True(domain.IsLessThanOrEqual(meet, right));

                foreach (var third in values)
                {
                    Assert.Equal(
                        domain.Meet(domain.Meet(left, right), third),
                        domain.Meet(left, domain.Meet(right, third)));
                    if (domain.IsLessThanOrEqual(third, left) && domain.IsLessThanOrEqual(third, right))
                    {
                        Assert.True(domain.IsLessThanOrEqual(third, meet));
                    }
                }
            }
        }
    }

    /// <summary>Checks widening coverage and confirms repeated top construction is one semantic element.</summary>
    [Fact]
    public void WidenCoversBothInputsAndTopIsCanonical()
    {
        var firstTop = domain.Top(TypeSig.Int32);
        var secondTop = domain.Top(TypeSig.Int32);

        Assert.Equal(firstTop, secondTop);

        foreach (var left in Values())
        {
            foreach (var right in Values())
            {
                var widened = domain.Widen(left, right);
                Assert.True(domain.IsLessThanOrEqual(left, widened));
                Assert.True(domain.IsLessThanOrEqual(right, widened));
            }
        }
    }

    /// <summary>
    /// Checks that exact metadata types use module-and-TypeDef identity while names remain diagnostic-only evidence.
    /// </summary>
    [Fact]
    public void MetadataTypeIdentityIsStructuralAndDisplayNamesDoNotAliasOrSplitIt()
    {
        var firstModule = new ModuleHandle(1, 2);
        var secondModule = new ModuleHandle(1, 3);
        var firstName = TypeSig.CreateTypeDefinition(firstModule, 0x02000001, "Fixture.FirstName");
        var changedName = TypeSig.CreateTypeDefinition(firstModule, 0x02000001, "Fixture.Renamed");
        var otherRow = TypeSig.CreateTypeDefinition(firstModule, 0x02000002, "Fixture.FirstName");
        var otherModule = TypeSig.CreateTypeDefinition(secondModule, 0x02000001, "Fixture.FirstName");

        Assert.Equal(firstName, changedName);
        Assert.Equal(firstName.GetHashCode(), changedName.GetHashCode());
        Assert.NotEqual(firstName, otherRow);
        Assert.NotEqual(firstName, otherModule);
        Assert.NotEqual(new TypeSig("Fixture.FirstName"), firstName);
        Assert.Equal(
            TypeSig.CreateSzArray(firstName),
            TypeSig.CreateSzArray(changedName));
        Assert.NotEqual(
            TypeSig.CreateSzArray(firstName),
            TypeSig.CreateSzArray(otherModule));
    }

    /// <summary>Checks validation of nil and wrong-kind structural metadata tokens.</summary>
    [Fact]
    public void StructuralMetadataTypesRejectInvalidModuleAndTypeDefinitionTokens()
    {
        var module = new ModuleHandle(1, 2);

        Assert.False(TypeSig.IsValidTypeDefinitionToken(0));
        Assert.False(TypeSig.IsValidTypeDefinitionToken(0x02000000));
        Assert.False(TypeSig.IsValidTypeDefinitionToken(0x04000001));
        Assert.True(TypeSig.IsValidTypeDefinitionToken(0x02000001));
        Assert.Throws<ArgumentException>(
            () => TypeSig.CreateTypeDefinition(default, 0x02000001, "Fixture.Type"));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => TypeSig.CreateTypeDefinition(module, 0x02000000, "Fixture.Type"));
        Assert.Throws<ArgumentException>(() => TypeSig.CreateSzArray(TypeSig.Void));
    }

    /// <summary>
    /// Checks that dump runtime-module execution handles bind stable source identity without claiming PE identity.
    /// </summary>
    [Fact]
    public void RuntimeEvidenceModuleHandlesAreStableAndSnapshotScoped()
    {
        var content = ModuleContentIdentity.FromMetadata(
            Guid.Parse("131e2add-42c7-4c4a-bd3d-c92a6f50437c"),
            [0x42, 0x53, 0x4A, 0x42]);

        var first = ModuleHandle.FromRuntimeEvidenceIdentity(content, "snapshot-1|runtime-1|module-1");
        var replay = ModuleHandle.FromRuntimeEvidenceIdentity(content, "snapshot-1|runtime-1|module-1");
        var otherSnapshot = ModuleHandle.FromRuntimeEvidenceIdentity(content, "snapshot-2|runtime-1|module-1");

        Assert.Equal(first, replay);
        Assert.NotEqual(first, otherSnapshot);
        Assert.Throws<ArgumentException>(
            () => ModuleHandle.FromRuntimeEvidenceIdentity(content, " "));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => ModuleHandle.FromRuntimeEvidenceIdentity(
                content,
                new string('s', ModuleHandle.MaximumRuntimeEvidenceSourceIdLength + 1)));
    }

    /// <summary>Checks exact CLI defaults for W3 value types and typed references.</summary>
    [Fact]
    public void DefaultValueProducesExactZeroAndTypedNullWithoutUsingLatticeBottom()
    {
        var objectType = TypeSig.CreateTypeDefinition(
            new ModuleHandle(9, 10),
            0x02000001,
            "Fixture.Object");
        var intDefault = domain.DefaultValue(TypeSig.Int32);
        var objectDefault = domain.DefaultValue(objectType);
        var arrayDefault = domain.DefaultValue(TypeSig.CreateSzArray(TypeSig.Int32));

        Assert.True(domain.TryGetConstInt32(intDefault, out var integer));
        Assert.Equal(0, integer);
        Assert.False(domain.IsBottom(intDefault));
        Assert.Equal(ConcreteValueKind.Null, objectDefault.Kind);
        Assert.Equal(objectType, objectDefault.StaticType);
        Assert.Equal(StackKind.Ref, domain.GetStackKind(objectDefault));
        Assert.Equal(ConcreteValueKind.Null, arrayDefault.Kind);
        Assert.Equal(TypeSig.CreateSzArray(TypeSig.Int32), arrayDefault.StaticType);
        Assert.Throws<ArgumentException>(() => domain.DefaultValue(TypeSig.Void));
    }

    /// <summary>Checks that exact static type remains stricter than the shared CLI I4 stack category.</summary>
    [Fact]
    public void BooleanAndInt32RemainDifferentStaticTypesDespiteSharingI4StackKind()
    {
        var integer = domain.ConstInt32(1);
        var booleanDefault = domain.DefaultValue(TypeSig.Boolean);

        Assert.Equal(StackKind.I4, domain.GetStackKind(integer));
        Assert.Equal(StackKind.I4, domain.GetStackKind(booleanDefault));
        Assert.NotEqual(domain.GetStaticType(integer), domain.GetStaticType(booleanDefault));
        Assert.Throws<ArgumentException>(() => domain.Join(integer, booleanDefault));
    }

    /// <summary>Checks that diagnostic formatting omits raw string or numeric target payloads.</summary>
    [Fact]
    public void DiagnosticFormattingOmitsRawPayloads()
    {
        var value = domain.ConstString("artifact-derived-value");

        Assert.DoesNotContain("artifact-derived-value", value.ToString(), StringComparison.Ordinal);
        Assert.Contains("length=22", value.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("123456789", domain.ConstInt32(123456789).ToString(), StringComparison.Ordinal);
    }

    private ConcreteValue[] Values() =>
    [
        domain.Bottom(TypeSig.Int32),
        domain.ConstInt32(-1),
        domain.ConstInt32(0),
        domain.ConstInt32(1),
        domain.ConstInt32(2),
        domain.Top(TypeSig.Int32),
    ];
}
