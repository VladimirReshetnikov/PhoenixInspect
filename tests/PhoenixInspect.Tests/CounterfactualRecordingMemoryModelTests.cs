using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Runtime.InteropServices;
using PhoenixInspect.Core.Abstractions;
using PhoenixInspect.Domain.Concrete;
using PhoenixInspect.Product.DumpDebugging;
using Xunit;

namespace PhoenixInspect.Tests;

/// <summary>Exercises the W4.8 plan-validating, read-only field-observation memory boundary.</summary>
public sealed class CounterfactualRecordingMemoryModelTests
{
    private const string SourceSha256 =
        "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private const string ImportedObjectSha256 =
        "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789";

    private static readonly TypeSig Owner = MethodGraphPlannerTests.RootType;
    private static readonly ResolvedField FirstField = MethodGraphPlannerTests.Field(1, Owner);
    private static readonly ResolvedField SecondField = MethodGraphPlannerTests.Field(2, Owner);

    /// <summary>Confirms exact observations admit only the planned scalar and record the reached dependency.</summary>
    [Fact]
    public void ExactObservationValidatesScalarAndRecordsReach()
    {
        var domain = new ProvenanceConcreteDomain();
        var observation = Exact(0, FirstField, 0x1234_5678);
        var inner = new TestMemoryModel(
            (_, _, _) => MemoryLoadResult<ProvenanceConcreteValue>.Exact(
                domain.ConstInt32(0x1234_5678)));
        var subject = CreateSubject(domain, inner, observation);

        var result = subject.LoadField(TestMemory.Instance, Root(domain), FirstField);

        Assert.Equal(MemoryLoadKind.Exact, result.Kind);
        Assert.True(domain.TryGetConstInt32(result.Value, out var scalar));
        Assert.Equal(0x1234_5678, scalar);
        Assert.Equal(1, inner.LoadFieldCallCount);
        Assert.True(subject.ReachedObservations.SequenceEqual([observation]));
        Assert.True(subject.ReachedLoadOrdinals.SequenceEqual([0]));
    }

    /// <summary>Confirms partial and unavailable evidence must match completely and is freshly reconstituted.</summary>
    [Theory]
    [InlineData(EvaluationEvidenceStatus.Partial)]
    [InlineData(EvaluationEvidenceStatus.Unavailable)]
    public void DegradedObservationRequiresCanonicalEvidenceAndReturnsFreshEvidence(
        EvaluationEvidenceStatus status)
    {
        var domain = new ProvenanceConcreteDomain();
        var observation = NonExact(0, FirstField, status, $"W4.Field.{status}");
        var innerEvidence = CloneEvidence(observation);
        var inner = new TestMemoryModel(
            (_, _, _) => MemoryLoadResult<ProvenanceConcreteValue>.FromFieldEvidence(innerEvidence));
        var subject = CreateSubject(domain, inner, observation);

        var result = subject.LoadField(TestMemory.Instance, Root(domain), FirstField);

        Assert.Equal(
            status == EvaluationEvidenceStatus.Partial ? MemoryLoadKind.Partial : MemoryLoadKind.Unavailable,
            result.Kind);
        Assert.NotNull(result.FieldEvidence);
        Assert.NotSame(innerEvidence, result.FieldEvidence);
        Assert.NotSame(observation.RuntimeFieldEvidence, result.FieldEvidence);
        Assert.Equal(observation.RuntimeFieldEvidence, result.FieldEvidence);
        Assert.Equal(observation.ApproximationEvidenceSha256, result.FieldEvidence!.Sha256);
        Assert.True(result.FieldEvidence.CanonicalBytes.AsSpan().SequenceEqual(
            observation.RuntimeFieldEvidence!.CanonicalBytes.AsSpan()));
        Assert.Equal(1, inner.LoadFieldCallCount);
    }

    /// <summary>Confirms conflict and invalid observations require their exact planned kind and stable reason.</summary>
    [Theory]
    [InlineData(EvaluationEvidenceStatus.Conflict, MemoryLoadKind.Conflict, "W4.Field.Conflict")]
    [InlineData(EvaluationEvidenceStatus.Invalid, MemoryLoadKind.Invalid, "W4.Field.Invalid")]
    public void TerminalEvidenceDispositionValidatesKindAndReason(
        EvaluationEvidenceStatus status,
        MemoryLoadKind kind,
        string reason)
    {
        var domain = new ProvenanceConcreteDomain();
        var observation = NonExact(0, FirstField, status, reason);
        var inner = new TestMemoryModel(
            (_, _, _) => MemoryLoadResult<ProvenanceConcreteValue>.NonExact(kind, reason));
        var subject = CreateSubject(domain, inner, observation);

        var result = subject.LoadField(TestMemory.Instance, Root(domain), FirstField);

        Assert.Equal(kind, result.Kind);
        Assert.Equal(reason, result.FailureCode);
        Assert.Null(result.FieldEvidence);
        Assert.Equal(1, inner.LoadFieldCallCount);
    }

    /// <summary>Confirms every inner result inconsistent with frozen evidence becomes one fixed invalidity.</summary>
    [Fact]
    public void EvidenceMismatchAndTargetExceptionBecomeFixedInvalidity()
    {
        var domain = new ProvenanceConcreteDomain();
        AssertMismatch(
            domain,
            Exact(0, FirstField, 17),
            MemoryLoadResult<ProvenanceConcreteValue>.Exact(domain.ConstInt32(18)));
        AssertMismatch(
            domain,
            Exact(0, FirstField, 17),
            MemoryLoadResult<ProvenanceConcreteValue>.Exact(domain.Top(TypeSig.Int32)));
        AssertMismatch(
            domain,
            Exact(0, FirstField, 17),
            MemoryLoadResult<ProvenanceConcreteValue>.ForTargetException(
                new TargetExceptionInfo(TargetExceptionKind.NullReference, "TARGET_NULL_REFERENCE")));

        var partial = NonExact(0, FirstField, EvaluationEvidenceStatus.Partial, "W4.Field.Partial");
        var differentEvidence = new FieldLoadEvidence(
            partial.DependencyOrdinal,
            partial.Field,
            partial.EvidenceStatus,
            partial.ReasonCode!,
            partial.SourceSha256,
            partial.ImportedObjectSha256,
            partial.Address + sizeof(int),
            partial.RequestedLength,
            partial.ObservedBytes.AsSpan());
        AssertMismatch(
            domain,
            partial,
            MemoryLoadResult<ProvenanceConcreteValue>.FromFieldEvidence(differentEvidence));
        AssertMismatch(
            domain,
            partial,
            MemoryLoadResult<ProvenanceConcreteValue>.NonExact(
                MemoryLoadKind.Partial,
                partial.ReasonCode!));
        AssertMismatch(
            domain,
            NonExact(0, FirstField, EvaluationEvidenceStatus.Conflict, "W4.Field.Conflict"),
            MemoryLoadResult<ProvenanceConcreteValue>.NonExact(
                MemoryLoadKind.Conflict,
                "W4.Field.OtherConflict"));
    }

    /// <summary>Confirms an unplanned receiver or structural field cannot reach the inner memory capability.</summary>
    [Fact]
    public void WrongReceiverOrFieldIsRejectedBeforeCapabilityUse()
    {
        var domain = new ProvenanceConcreteDomain();
        var observation = Exact(0, FirstField, 17);
        var inner = new TestMemoryModel(
            (_, _, _) => MemoryLoadResult<ProvenanceConcreteValue>.Exact(domain.ConstInt32(17)));
        var subject = CreateSubject(domain, inner, observation);

        AssertMismatch(subject.LoadField(TestMemory.Instance, domain.ObjectReference(2, Owner), FirstField));
        AssertMismatch(subject.LoadField(TestMemory.Instance, Root(domain), SecondField));

        Assert.Equal(0, inner.LoadFieldCallCount);
        Assert.Empty(subject.ReachedObservations);
        Assert.Empty(subject.ReachedLoadOrdinals);
    }

    /// <summary>Confirms every repeated load invokes the capability once while unique reach stays in plan order.</summary>
    [Fact]
    public void RepeatedLoadsRetainTemporalOrderAndUniquePlanOrder()
    {
        var domain = new ProvenanceConcreteDomain();
        var first = Exact(0, FirstField, 11);
        var second = Exact(1, SecondField, 22);
        var inner = new TestMemoryModel(
            (_, _, field) => MemoryLoadResult<ProvenanceConcreteValue>.Exact(
                domain.ConstInt32(field == FirstField ? 11 : 22)));
        var subject = CreateSubject(domain, inner, first, second);

        Assert.Equal(MemoryLoadKind.Exact, subject.LoadField(TestMemory.Instance, Root(domain), SecondField).Kind);
        Assert.Equal(MemoryLoadKind.Exact, subject.LoadField(TestMemory.Instance, Root(domain), FirstField).Kind);
        Assert.Equal(MemoryLoadKind.Exact, subject.LoadField(TestMemory.Instance, Root(domain), SecondField).Kind);

        Assert.Equal(3, inner.LoadFieldCallCount);
        Assert.True(subject.ReachedObservations.SequenceEqual([first, second]));
        Assert.True(subject.ReachedLoadOrdinals.SequenceEqual([1, 0, 1]));
    }

    /// <summary>Confirms reach is retained before an inner capability exception is propagated unchanged.</summary>
    [Fact]
    public void CapabilityThrowRetainsReachedObservation()
    {
        var domain = new ProvenanceConcreteDomain();
        var observation = Exact(0, FirstField, 17);
        var failure = new InvalidOperationException("fixture capability failure");
        var inner = new TestMemoryModel((_, _, _) => throw failure);
        var subject = CreateSubject(domain, inner, observation);

        var thrown = Assert.Throws<InvalidOperationException>(
            () => subject.LoadField(TestMemory.Instance, Root(domain), FirstField));

        Assert.Same(failure, thrown);
        Assert.Equal(1, inner.LoadFieldCallCount);
        Assert.True(subject.ReachedObservations.SequenceEqual([observation]));
        Assert.True(subject.ReachedLoadOrdinals.SequenceEqual([0]));
    }

    /// <summary>Confirms allocation, mutation, and array operations are absent and never delegated.</summary>
    [Fact]
    public void NonFieldMemoryCapabilitiesAreUnsupportedWithoutDelegation()
    {
        var domain = new ProvenanceConcreteDomain();
        var inner = new TestMemoryModel(
            (_, _, _) => throw new InvalidOperationException("No field was planned."));
        var subject = CreateSubject(domain, inner);
        var receiver = Root(domain);
        var scalar = domain.ConstInt32(0);

        Assert.False(subject.CanAllocate);
        Assert.Throws<NotSupportedException>(() => subject.NewObject(TestMemory.Instance, Owner));
        Assert.Throws<NotSupportedException>(() => subject.NewArray(TestMemory.Instance, TypeSig.Int32, scalar));
        Assert.Throws<NotSupportedException>(
            () => subject.StoreField(TestMemory.Instance, receiver, FirstField, scalar));
        Assert.Throws<NotSupportedException>(() => subject.LoadElement(TestMemory.Instance, receiver, scalar));
        Assert.Throws<NotSupportedException>(
            () => subject.StoreElement(TestMemory.Instance, receiver, scalar, scalar));

        Assert.Equal(0, inner.CanAllocateReadCount);
        Assert.Equal(0, inner.LoadFieldCallCount);
        Assert.Equal(0, inner.NonLoadCallCount);
    }

    /// <summary>Confirms construction requires an exact root and a copied complete ordinal-aligned observation set.</summary>
    [Fact]
    public void ConstructionValidatesRootAndObservationSetAndCopiesItsBackingArray()
    {
        var domain = new ProvenanceConcreteDomain();
        var inner = new TestMemoryModel(
            (_, _, _) => MemoryLoadResult<ProvenanceConcreteValue>.Exact(domain.ConstInt32(11)));
        Assert.Throws<ArgumentException>(
            () => new CounterfactualRecordingMemoryModel<TestMemory>(
                inner,
                domain.Top(Owner),
                domain,
                ImmutableArray<CounterfactualFieldObservation>.Empty));
        Assert.Throws<ArgumentException>(
            () => new CounterfactualRecordingMemoryModel<TestMemory>(
                inner,
                Root(domain),
                domain,
                default));
        Assert.Throws<ArgumentException>(
            () => CreateSubject(domain, inner, Exact(1, FirstField, 11)));
        Assert.Throws<ArgumentException>(
            () => CreateSubject(
                domain,
                inner,
                Exact(0, FirstField, 11),
                Exact(1, FirstField, 11)));
        Assert.Throws<ArgumentException>(
            () => new CounterfactualRecordingMemoryModel<TestMemory>(
                inner,
                domain.ObjectReference(1, MethodGraphPlannerTests.HelperType),
                domain,
                ImmutableArray.Create(Exact(0, FirstField, 11))));

        var backing = new[] { Exact(0, FirstField, 11) };
        var subject = new CounterfactualRecordingMemoryModel<TestMemory>(
            inner,
            Root(domain),
            domain,
            ImmutableCollectionsMarshal.AsImmutableArray(backing));
        backing[0] = Exact(0, SecondField, 22);

        var result = subject.LoadField(TestMemory.Instance, Root(domain), FirstField);
        Assert.Equal(MemoryLoadKind.Exact, result.Kind);
        Assert.Equal(FirstField, Assert.Single(subject.ReachedObservations).Field);
    }

    private static CounterfactualRecordingMemoryModel<TestMemory> CreateSubject(
        ProvenanceConcreteDomain domain,
        TestMemoryModel inner,
        params CounterfactualFieldObservation[] observations) =>
        new(inner, Root(domain), domain, ImmutableArray.CreateRange(observations));

    private static ProvenanceConcreteValue Root(ProvenanceConcreteDomain domain) =>
        domain.ObjectReference(1, Owner);

    private static CounterfactualFieldObservation Exact(
        int ordinal,
        ResolvedField field,
        int value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, value);
        return CounterfactualFieldObservation.CreateExactInt32(
            ordinal,
            field,
            SourceSha256,
            ImportedObjectSha256,
            checked(0x1000UL + ((ulong)ordinal * sizeof(int))),
            sizeof(int),
            bytes);
    }

    private static CounterfactualFieldObservation NonExact(
        int ordinal,
        ResolvedField field,
        EvaluationEvidenceStatus status,
        string reason)
    {
        ReadOnlySpan<byte> bytes = status switch
        {
            EvaluationEvidenceStatus.Partial => [0x12, 0x34],
            EvaluationEvidenceStatus.Unavailable => [],
            EvaluationEvidenceStatus.Conflict => [0x11, 0x22, 0x33, 0x44],
            EvaluationEvidenceStatus.Invalid => [],
            _ => throw new ArgumentOutOfRangeException(nameof(status)),
        };
        return CounterfactualFieldObservation.CreateNonExactInt32(
            ordinal,
            field,
            status,
            reason,
            SourceSha256,
            ImportedObjectSha256,
            checked(0x1000UL + ((ulong)ordinal * sizeof(int))),
            sizeof(int),
            bytes);
    }

    private static FieldLoadEvidence CloneEvidence(CounterfactualFieldObservation observation) =>
        new(
            observation.DependencyOrdinal,
            observation.Field,
            observation.EvidenceStatus,
            observation.ReasonCode!,
            observation.SourceSha256,
            observation.ImportedObjectSha256,
            observation.Address,
            observation.RequestedLength,
            observation.ObservedBytes.AsSpan());

    private static void AssertMismatch(
        ProvenanceConcreteDomain domain,
        CounterfactualFieldObservation observation,
        MemoryLoadResult<ProvenanceConcreteValue> result)
    {
        var inner = new TestMemoryModel((_, _, _) => result);
        var subject = CreateSubject(domain, inner, observation);
        AssertMismatch(subject.LoadField(TestMemory.Instance, Root(domain), observation.Field));
        Assert.Equal(1, inner.LoadFieldCallCount);
        Assert.True(subject.ReachedObservations.SequenceEqual([observation]));
        Assert.True(subject.ReachedLoadOrdinals.SequenceEqual([0]));
    }

    private static void AssertMismatch(MemoryLoadResult<ProvenanceConcreteValue> result)
    {
        Assert.Equal(MemoryLoadKind.Invalid, result.Kind);
        Assert.Equal("W4.Evidence.FieldObservationMismatch", result.FailureCode);
        Assert.Equal(CounterfactualRecordingMemoryModel<TestMemory>.ObservationMismatchCode, result.FailureCode);
        Assert.Null(result.FieldEvidence);
        Assert.Null(result.Exception);
    }

    private sealed record TestMemory : IPersistentMemoryState<TestMemory>
    {
        internal static TestMemory Instance { get; } = new();

        public TestMemory Fork() => this;
    }

    private sealed class TestMemoryModel(
        Func<TestMemory, ProvenanceConcreteValue, ResolvedField,
            MemoryLoadResult<ProvenanceConcreteValue>> load) :
        IMemoryModel<ProvenanceConcreteValue, TestMemory>
    {
        internal int CanAllocateReadCount { get; private set; }

        internal int LoadFieldCallCount { get; private set; }

        internal int NonLoadCallCount { get; private set; }

        public bool CanAllocate
        {
            get
            {
                CanAllocateReadCount++;
                return true;
            }
        }

        public (ProvenanceConcreteValue objRef, TestMemory mem) NewObject(TestMemory mem, TypeSig type)
        {
            NonLoadCallCount++;
            throw new InvalidOperationException("The wrapper delegated object allocation.");
        }

        public (ProvenanceConcreteValue arrRef, TestMemory mem) NewArray(
            TestMemory mem,
            TypeSig elemType,
            ProvenanceConcreteValue length)
        {
            NonLoadCallCount++;
            throw new InvalidOperationException("The wrapper delegated array allocation.");
        }

        public MemoryLoadResult<ProvenanceConcreteValue> LoadField(
            TestMemory mem,
            ProvenanceConcreteValue objRef,
            ResolvedField field)
        {
            LoadFieldCallCount++;
            return load(mem, objRef, field);
        }

        public TestMemory StoreField(
            TestMemory mem,
            ProvenanceConcreteValue objRef,
            ResolvedField field,
            ProvenanceConcreteValue value)
        {
            NonLoadCallCount++;
            throw new InvalidOperationException("The wrapper delegated a field store.");
        }

        public ProvenanceConcreteValue LoadElement(
            TestMemory mem,
            ProvenanceConcreteValue arrRef,
            ProvenanceConcreteValue index)
        {
            NonLoadCallCount++;
            throw new InvalidOperationException("The wrapper delegated an array load.");
        }

        public TestMemory StoreElement(
            TestMemory mem,
            ProvenanceConcreteValue arrRef,
            ProvenanceConcreteValue index,
            ProvenanceConcreteValue value)
        {
            NonLoadCallCount++;
            throw new InvalidOperationException("The wrapper delegated an array store.");
        }
    }
}
