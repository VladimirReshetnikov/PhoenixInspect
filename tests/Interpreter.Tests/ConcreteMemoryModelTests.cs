using Interpreter.Core.Abstractions;
using Interpreter.Domain.Concrete;
using Xunit;

namespace Interpreter.Tests;

/// <summary>
/// Exercises snapshot persistence, deterministic allocation, scoped managed references, and stable memory hashing.
/// </summary>
public sealed class ConcreteMemoryModelTests
{
    private readonly ConcreteDomain domain = new();
    private readonly ConcreteMemoryModel memoryModel;

    /// <summary>Creates a test fixture with one concrete domain shared by the memory model.</summary>
    public ConcreteMemoryModelTests()
    {
        memoryModel = new ConcreteMemoryModel(domain);
    }

    /// <summary>Checks that object and array writes create isolated descendant snapshots after a fork.</summary>
    [Fact]
    public void ObjectAndArrayStoresPreserveEarlierSnapshotsAndForks()
    {
        var objectType = new TypeSig("Fixture.Object");
        var field = new FieldHandle(0x04000001);
        var (objectReference, allocated) = memoryModel.NewObject(ConcreteMemory.Empty, objectType);
        var fork = allocated.Fork();
        var left = memoryModel.StoreField(allocated, objectReference, field, domain.ConstInt32(11));
        var right = memoryModel.StoreField(fork, objectReference, field, domain.ConstInt32(22));

        Assert.Equal(ConcreteValueKind.Unknown, memoryModel.LoadField(allocated, objectReference, field).Kind);
        AssertInt32(11, memoryModel.LoadField(left, objectReference, field));
        AssertInt32(22, memoryModel.LoadField(right, objectReference, field));

        var (arrayReference, withArray) = memoryModel.NewArray(left, ConcreteDomain.Int32Type, domain.ConstInt32(2));
        var changedArray = memoryModel.StoreElement(
            withArray,
            arrayReference,
            domain.ConstInt32(1),
            domain.ConstInt32(99));

        AssertInt32(0, memoryModel.LoadElement(withArray, arrayReference, domain.ConstInt32(1)));
        AssertInt32(99, memoryModel.LoadElement(changedArray, arrayReference, domain.ConstInt32(1)));
    }

    /// <summary>Checks deterministic reference assignment for equal allocations replayed from one ancestor.</summary>
    [Fact]
    public void EqualAllocationBranchesProduceEqualReferencesAndSnapshots()
    {
        var type = new TypeSig("Fixture.Object");
        var (leftReference, left) = memoryModel.NewObject(ConcreteMemory.Empty, type);
        var (rightReference, right) = memoryModel.NewObject(ConcreteMemory.Empty.Fork(), type);

        Assert.Equal(leftReference, rightReference);
        Assert.Equal(left, right);
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
    }

    /// <summary>Checks content equality and stable hash equality for independently replayed operation sequences.</summary>
    [Fact]
    public void EqualOperationSequencesProduceEqualSnapshotsAndStableHashes()
    {
        var left = BuildSnapshot();
        var right = BuildSnapshot();

        Assert.NotSame(left, right);
        Assert.Equal(left, right);
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
    }

    /// <summary>Checks that the self-typed persistent-memory interface is usable as an executable generic boundary.</summary>
    [Fact]
    public void PersistentMemoryBoundaryIsExecutableThroughSelfTypedConstraint()
    {
        var fork = ForkGeneric(ConcreteMemory.Empty);

        Assert.Same(ConcreteMemory.Empty, fork);
    }

    /// <summary>Checks that hostile concrete lengths cannot trigger an unbounded host allocation.</summary>
    [Fact]
    public void ArrayAllocationRejectsLengthsBeyondDeterministicCap()
    {
        var exception = Assert.Throws<ArgumentException>(() => memoryModel.NewArray(
            ConcreteMemory.Empty,
            ConcreteDomain.Int32Type,
            domain.ConstInt32(ConcreteMemoryModel.MaxArrayLength + 1)));

        Assert.Equal("length", exception.ParamName);
        Assert.Equal(0, ConcreteMemory.Empty.ArrayCount);
    }

    /// <summary>Checks that the validation heap cannot store a value with the wrong static element type.</summary>
    [Fact]
    public void ArrayStoreRejectsStaticElementTypeMismatch()
    {
        var (arrayReference, memory) = memoryModel.NewArray(
            ConcreteMemory.Empty,
            ConcreteDomain.Int32Type,
            domain.ConstInt32(1));

        var exception = Assert.Throws<ArgumentException>(() => memoryModel.StoreElement(
            memory,
            arrayReference,
            domain.ConstInt32(0),
            domain.ConstString("secret payload")));

        Assert.Equal("value", exception.ParamName);
        AssertInt32(0, memoryModel.LoadElement(memory, arrayReference, domain.ConstInt32(0)));
    }

    private ConcreteMemory BuildSnapshot()
    {
        var type = new TypeSig("Fixture.Object");
        var (reference, memory) = memoryModel.NewObject(ConcreteMemory.Empty, type);
        memory = memoryModel.StoreField(memory, reference, new FieldHandle(1), domain.ConstString("same"));
        var (arrayReference, withArray) = memoryModel.NewArray(memory, ConcreteDomain.Int32Type, domain.ConstInt32(1));
        return memoryModel.StoreElement(withArray, arrayReference, domain.ConstInt32(0), domain.ConstInt32(42));
    }

    private static TMemory ForkGeneric<TMemory>(TMemory memory)
        where TMemory : IPersistentMemoryState<TMemory> => memory.Fork();

    private void AssertInt32(int expected, ConcreteValue actual)
    {
        Assert.True(domain.TryGetConstInt32(actual, out var value));
        Assert.Equal(expected, value);
    }
}
