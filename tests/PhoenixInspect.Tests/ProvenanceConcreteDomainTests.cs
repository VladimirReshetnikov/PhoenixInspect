using System.Text;
using PhoenixInspect.Core.Abstractions;
using PhoenixInspect.Domain.Concrete;
using Xunit;
using ModuleHandle = PhoenixInspect.Core.Abstractions.ModuleHandle;

namespace PhoenixInspect.Tests;

/// <summary>
/// Verifies the semantic and explanatory laws of the W4 provenance-aware concrete domain.
/// </summary>
public sealed class ProvenanceConcreteDomainTests
{
    private readonly ProvenanceConcreteDomain domain = new();

    /// <summary>Checks the full lifted-flat Int32 partial order without treating lineage as semantic state.</summary>
    [Fact]
    public void LiftedFlatInt32ValuesSatisfyPartialOrderLawsAcrossDistinctOrigins()
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

    /// <summary>Checks least-upper-bound laws over exact, bottom, bare-top, and independently explained top values.</summary>
    [Fact]
    public void JoinIsCommutativeAssociativeIdempotentAndLeastUpperBound()
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

    /// <summary>Checks greatest-lower-bound laws independently of explanatory roots.</summary>
    [Fact]
    public void MeetIsCommutativeAssociativeIdempotentAndGreatestLowerBound()
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

    /// <summary>Checks widening coverage and stable convergence over the finite lifted-flat domain.</summary>
    [Fact]
    public void WidenCoversBothInputsAndConvergesAtSemanticTop()
    {
        foreach (var left in Values())
        {
            foreach (var right in Values())
            {
                var widened = domain.Widen(left, right);
                Assert.True(domain.IsLessThanOrEqual(left, widened));
                Assert.True(domain.IsLessThanOrEqual(right, widened));
                Assert.Equal(widened, domain.Widen(widened, right));
            }
        }
    }

    /// <summary>Checks that two explanations of the same top value compare and hash as one semantic element.</summary>
    [Fact]
    public void ExplainedUnknownEqualityAndHashingIgnoreLineage()
    {
        var first = Unknown("first", 0, EvaluationEvidenceStatus.Partial);
        var second = Unknown("second", 1, EvaluationEvidenceStatus.Unavailable);

        Assert.True(first.TryGetLineageRoot(out var firstRoot));
        Assert.True(second.TryGetLineageRoot(out var secondRoot));
        Assert.NotEqual(firstRoot, secondRoot);
        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
        Assert.Single(new HashSet<ProvenanceConcreteValue> { first, second, domain.Top(TypeSig.Int32) });
    }

    /// <summary>Checks deterministic explanation selection without making lineage part of lattice semantics.</summary>
    [Fact]
    public void LatticeMergesPreserveOnlyAnUnambiguousExplanation()
    {
        var first = Unknown("first", 0, EvaluationEvidenceStatus.Partial);
        var second = Unknown("second", 1, EvaluationEvidenceStatus.Unavailable);
        var bottom = domain.Bottom(TypeSig.Int32);
        var exact = domain.ConstInt32(7);

        foreach (var ambiguous in new[]
        {
            domain.Join(first, second),
            domain.Join(second, first),
            domain.Meet(first, second),
            domain.Meet(second, first),
            domain.Widen(first, second),
            domain.Widen(second, first),
        })
        {
            Assert.Equal(ValuePrecisionKind.UnexplainedUnknown, domain.GetPrecision(ambiguous));
            Assert.False(ambiguous.TryGetLineageRoot(out _));
        }

        var bottomJoin = domain.Join(bottom, first);
        Assert.Equal(ValuePrecisionKind.ExplainedUnknown, domain.GetPrecision(bottomJoin));
        Assert.True(first.TryGetLineageRoot(out var firstRoot));
        Assert.True(bottomJoin.TryGetLineageRoot(out var joinedRoot));
        Assert.Equal(firstRoot, joinedRoot);

        var exactMeet = domain.Meet(first, exact);
        Assert.Equal(ValuePrecisionKind.Exact, domain.GetPrecision(exactMeet));
        Assert.True(domain.TryGetConstInt32(exactMeet, out var exactValue));
        Assert.Equal(7, exactValue);
    }

    /// <summary>Checks canonical bottom, exact, default, top, type, stack, and constant-query behavior.</summary>
    [Fact]
    public void ValueFactoriesPreserveSemanticTypeStackAndPrecisionInvariants()
    {
        var bottom = domain.Bottom(TypeSig.Int32);
        var zero = domain.DefaultValue(TypeSig.Int32);
        var boolean = domain.DefaultValue(TypeSig.Boolean);
        var nullObject = domain.DefaultValue(TypeSig.Object);
        var nullArray = domain.DefaultValue(TypeSig.CreateSzArray(TypeSig.Int32));
        var integer = domain.ConstInt32(-42);
        var longInteger = domain.ConstInt64(long.MinValue);
        var text = domain.ConstString("fixture-value");
        var objectType = TypeSig.CreateTypeDefinition(
            new ModuleHandle(0x1234, 0x5678),
            0x02000001,
            "Fixture.Object");
        var objectReference = domain.ObjectReference(17, objectType);
        var bareTop = domain.Top(TypeSig.Int32);
        var explained = Unknown("partial-argument", 0, EvaluationEvidenceStatus.Partial);

        Assert.True(domain.IsBottom(bottom));
        Assert.Equal(ConcreteValueKind.Bottom, bottom.SemanticValue.Kind);
        Assert.Throws<ArgumentException>(() => domain.GetPrecision(bottom));

        Assert.True(domain.TryGetConstInt32(zero, out var defaultInteger));
        Assert.Equal(0, defaultInteger);
        Assert.True(domain.TryGetConstInt32(boolean, out var defaultBoolean));
        Assert.Equal(0, defaultBoolean);
        Assert.Equal(ConcreteValueKind.Null, nullObject.SemanticValue.Kind);
        Assert.Equal(ConcreteValueKind.Null, nullArray.SemanticValue.Kind);
        Assert.Equal(ValuePrecisionKind.Exact, domain.GetPrecision(zero));
        Assert.Equal(ValuePrecisionKind.Exact, domain.GetPrecision(nullObject));

        Assert.True(domain.TryGetConstInt32(integer, out var exact));
        Assert.Equal(-42, exact);
        Assert.False(domain.TryGetConstInt32(longInteger, out _));
        Assert.False(domain.TryGetConstInt32(text, out _));
        Assert.Equal(TypeSig.Int32, domain.GetStaticType(integer));
        Assert.Equal(TypeSig.Int64, domain.GetStaticType(longInteger));
        Assert.Equal(TypeSig.String, domain.GetStaticType(text));
        Assert.Equal(objectType, domain.GetStaticType(objectReference));
        Assert.Equal(StackKind.I4, domain.GetStackKind(integer));
        Assert.Equal(StackKind.I8, domain.GetStackKind(longInteger));
        Assert.Equal(StackKind.Ref, domain.GetStackKind(text));
        Assert.Equal(StackKind.Ref, domain.GetStackKind(objectReference));
        Assert.Equal(ConcreteValueKind.ObjectReference, objectReference.SemanticValue.Kind);
        Assert.True(objectReference.SemanticValue.TryGetReferenceId(out var referenceId));
        Assert.Equal(17, referenceId);
        Assert.Equal(ValuePrecisionKind.Exact, domain.GetPrecision(objectReference));

        Assert.Equal(ConcreteValueKind.Unknown, bareTop.SemanticValue.Kind);
        Assert.False(bareTop.TryGetLineageRoot(out _));
        Assert.Equal(ValuePrecisionKind.UnexplainedUnknown, domain.GetPrecision(bareTop));
        Assert.False(domain.TryGetConstInt32(bareTop, out _));

        Assert.Equal(ConcreteValueKind.Unknown, explained.SemanticValue.Kind);
        Assert.True(explained.TryGetLineageRoot(out _));
        Assert.Equal(ValuePrecisionKind.ExplainedUnknown, domain.GetPrecision(explained));
        Assert.Equal(TypeSig.Int32, domain.GetStaticType(explained));
        Assert.Equal(StackKind.I4, domain.GetStackKind(explained));
        Assert.False(domain.TryGetConstInt32(explained, out _));
    }

    /// <summary>Checks unchecked Int32 arithmetic, including overflow at every admitted binary operation.</summary>
    [Theory]
    [InlineData(BinaryOp.Add, int.MaxValue, 1, int.MinValue)]
    [InlineData(BinaryOp.Sub, int.MinValue, 1, int.MaxValue)]
    [InlineData(BinaryOp.Mul, int.MaxValue, 2, -2)]
    [InlineData(BinaryOp.Add, 17, 25, 42)]
    [InlineData(BinaryOp.Sub, 17, 25, -8)]
    [InlineData(BinaryOp.Mul, -7, 6, -42)]
    public void ExactBinaryOperationsUseUncheckedInt32Semantics(BinaryOp operation, int left, int right, int expected)
    {
        var result = domain.ApplyBinary(operation, domain.ConstInt32(left), domain.ConstInt32(right));

        Assert.True(domain.TryGetConstInt32(result, out var actual));
        Assert.Equal(expected, actual);
        Assert.False(result.TryGetLineageRoot(out _));
        Assert.Equal(ValuePrecisionKind.Exact, domain.GetPrecision(result));
    }

    /// <summary>Checks that any grounded unknown operand stays unknown for add, subtract, and multiply.</summary>
    [Theory]
    [InlineData(BinaryOp.Add)]
    [InlineData(BinaryOp.Sub)]
    [InlineData(BinaryOp.Mul)]
    public void ExplainedUnknownBinaryOperationsDoNotFabricateAConstant(BinaryOp operation)
    {
        var unknown = Unknown("unknown-left", 0, EvaluationEvidenceStatus.Partial);
        var exact = domain.ConstInt32(11);

        var leftUnknown = domain.ApplyBinary(operation, unknown, exact);
        var rightUnknown = domain.ApplyBinary(operation, exact, unknown);
        var bothUnknown = domain.ApplyBinary(
            operation,
            unknown,
            Unknown("unknown-right", 1, EvaluationEvidenceStatus.Unavailable));

        AssertUnknownWithLineage(leftUnknown);
        AssertUnknownWithLineage(rightUnknown);
        AssertUnknownWithLineage(bothUnknown);
        Assert.False(domain.TryGetConstInt32(leftUnknown, out _));
        Assert.False(domain.TryGetConstInt32(rightUnknown, out _));
        Assert.False(domain.TryGetConstInt32(bothUnknown, out _));
    }

    /// <summary>Checks that a bare lattice top remains ungrounded rather than acquiring invented provenance.</summary>
    [Theory]
    [InlineData(BinaryOp.Add)]
    [InlineData(BinaryOp.Sub)]
    [InlineData(BinaryOp.Mul)]
    public void BareTopArithmeticRemainsUngrounded(BinaryOp operation)
    {
        var bareTop = domain.Top(TypeSig.Int32);
        var explained = Unknown("known-origin", 0, EvaluationEvidenceStatus.Partial);

        foreach (var result in new[]
        {
            domain.ApplyBinary(operation, bareTop, domain.ConstInt32(1)),
            domain.ApplyBinary(operation, domain.ConstInt32(1), bareTop),
            domain.ApplyBinary(operation, bareTop, explained),
        })
        {
            Assert.Equal(ValuePrecisionKind.UnexplainedUnknown, domain.GetPrecision(result));
            Assert.False(result.TryGetLineageRoot(out _));
            Assert.False(domain.TryGetConstInt32(result, out _));
        }
    }

    /// <summary>Checks bottom absorption and the absence of lineage on infeasible arithmetic results.</summary>
    [Theory]
    [InlineData(BinaryOp.Add)]
    [InlineData(BinaryOp.Sub)]
    [InlineData(BinaryOp.Mul)]
    public void BottomIsAbsorbingForBinaryOperations(BinaryOp operation)
    {
        var bottom = domain.Bottom(TypeSig.Int32);
        var unknown = Unknown("unknown", 0, EvaluationEvidenceStatus.Partial);

        var first = domain.ApplyBinary(operation, bottom, unknown);
        var second = domain.ApplyBinary(operation, domain.ConstInt32(1), bottom);

        Assert.True(domain.IsBottom(first));
        Assert.True(domain.IsBottom(second));
        Assert.False(first.TryGetLineageRoot(out _));
        Assert.False(second.TryGetLineageRoot(out _));
    }

    /// <summary>Checks null, type, operation, and unsupported-factory validation at the public domain boundary.</summary>
    [Fact]
    public void DomainOperationsRejectMalformedOrMismatchedInputs()
    {
        Assert.Throws<ArgumentNullException>(() => domain.Bottom(null!));
        Assert.Throws<ArgumentNullException>(() => domain.Top(null!));
        Assert.Throws<ArgumentNullException>(() => domain.DefaultValue(null!));
        Assert.Throws<ArgumentException>(() => domain.DefaultValue(TypeSig.Void));
        Assert.Throws<ArgumentException>(() => domain.ConstNull(TypeSig.Int32));
        Assert.Throws<ArgumentNullException>(() => domain.ConstString(null!));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => domain.ObjectReference(0, TypeSig.CreateTypeDefinition(
                new ModuleHandle(1, 2), 0x02000001, "Fixture.Object")));
        Assert.Throws<ArgumentNullException>(() => domain.ObjectReference(1, null!));
        Assert.Throws<ArgumentException>(() => domain.ObjectReference(1, TypeSig.Object));
        Assert.Throws<ArgumentNullException>(() => domain.IsBottom(null!));
        Assert.Throws<ArgumentNullException>(() => domain.GetStaticType(null!));
        Assert.Throws<ArgumentNullException>(() => domain.GetStackKind(null!));
        Assert.Throws<ArgumentNullException>(() => domain.GetPrecision(null!));
        Assert.Throws<ArgumentNullException>(() => domain.TryGetConstInt32(null!, out _));
        Assert.Throws<ArgumentException>(
            () => domain.Join(domain.ConstInt32(1), domain.ConstInt64(1)));
        Assert.Throws<ArgumentException>(
            () => domain.Meet(domain.ConstInt32(1), domain.ConstInt64(1)));
        Assert.Throws<ArgumentException>(
            () => domain.Widen(domain.ConstInt32(1), domain.ConstInt64(1)));
        Assert.Throws<ArgumentException>(
            () => domain.IsLessThanOrEqual(domain.ConstInt32(1), domain.ConstInt64(1)));
        Assert.Throws<ArgumentException>(
            () => domain.ApplyBinary(BinaryOp.Add, domain.ConstInt32(1), domain.ConstInt64(1)));
        var invalidOperation = (BinaryOp)int.MaxValue;
        Assert.Throws<ArgumentOutOfRangeException>(
            () => domain.ApplyBinary(invalidOperation, domain.ConstInt32(1), domain.ConstInt32(2)));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => domain.ApplyBinary(invalidOperation, domain.Bottom(TypeSig.Int32), domain.ConstInt32(2)));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => domain.ApplyBinary(invalidOperation, domain.Top(TypeSig.Int32), domain.ConstInt32(2)));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => domain.ApplyBinary(
                invalidOperation,
                Unknown("invalid-operation", 0, EvaluationEvidenceStatus.Partial),
                domain.ConstInt32(2)));
    }

    private ProvenanceConcreteValue[] Values() =>
    [
        domain.Bottom(TypeSig.Int32),
        domain.ConstInt32(-1),
        domain.ConstInt32(0),
        domain.ConstInt32(1),
        Unknown("partial", 0, EvaluationEvidenceStatus.Partial),
        Unknown("unavailable", 1, EvaluationEvidenceStatus.Unavailable),
        domain.Top(TypeSig.Int32),
    ];

    private ProvenanceConcreteValue Unknown(
        string source,
        int originIndex,
        EvaluationEvidenceStatus evidence) =>
        domain.CreateInputUnknown(new ProvenanceInputOrigin(
            ProvenanceInputKind.RequestArgument,
            originIndex,
            evidence,
            ProvenanceSourceKey.Hash(Encoding.UTF8.GetBytes(source)),
            evidence == EvaluationEvidenceStatus.Partial
                ? "W4.Evidence.Partial"
                : "W4.Evidence.Unavailable",
            TypeSig.Int32));

    private void AssertUnknownWithLineage(ProvenanceConcreteValue value)
    {
        Assert.Equal(ConcreteValueKind.Unknown, value.SemanticValue.Kind);
        Assert.Equal(TypeSig.Int32, domain.GetStaticType(value));
        Assert.Equal(StackKind.I4, domain.GetStackKind(value));
        Assert.Equal(ValuePrecisionKind.ExplainedUnknown, domain.GetPrecision(value));
        Assert.True(value.TryGetLineageRoot(out _));
    }
}
