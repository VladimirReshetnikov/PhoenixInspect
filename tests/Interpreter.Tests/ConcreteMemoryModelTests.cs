using Interpreter.Core.Abstractions;
using Interpreter.Domain.Concrete;
using Xunit;
using ModuleHandle = Interpreter.Core.Abstractions.ModuleHandle;

namespace Interpreter.Tests;

/// <summary>
/// Exercises allocated-versus-imported field semantics, typed load outcomes, persistence, deterministic evidence
/// identity, arrays, and stable memory hashing.
/// </summary>
public sealed class ConcreteMemoryModelTests
{
    private static readonly ModuleHandle FixtureModule = new(0x0102030405060708, 0x1112131415161718);
    private static readonly ModuleHandle OtherModule = new(0x2122232425262728, 0x3132333435363738);

    private readonly ConcreteDomain domain = new();
    private readonly ConcreteMemoryModel memoryModel;

    /// <summary>Creates a test fixture with one concrete domain shared by the memory model.</summary>
    public ConcreteMemoryModelTests()
    {
        memoryModel = new ConcreteMemoryModel(domain);
    }

    /// <summary>
    /// Checks that absent fields on allocated objects use typed CLI defaults while stores preserve ancestor
    /// snapshots and sibling forks.
    /// </summary>
    [Fact]
    public void AllocatedFieldsDefaultToExactZeroAndStoresPreserveSnapshotsAndForks()
    {
        var objectType = ObjectType();
        var field = Field(objectType);
        var (objectReference, allocated) = memoryModel.NewObject(ConcreteMemory.Empty, objectType);
        var fork = allocated.Fork();
        var left = memoryModel.StoreField(allocated, objectReference, field, domain.ConstInt32(11));
        var right = memoryModel.StoreField(fork, objectReference, field, domain.ConstInt32(22));

        AssertExactInt32(0, memoryModel.LoadField(allocated, objectReference, field));
        AssertExactInt32(11, memoryModel.LoadField(left, objectReference, field));
        AssertExactInt32(22, memoryModel.LoadField(right, objectReference, field));
        Assert.Equal(allocated, fork);
        Assert.NotEqual(left, right);
    }

    /// <summary>
    /// Checks that an absent imported field remains unavailable and only explicit exact import makes it readable.
    /// </summary>
    [Fact]
    public void ImportedFieldAbsenceNeverFallsBackToAllocatedDefault()
    {
        var objectType = ObjectType();
        var field = Field(objectType);
        var evidence = new ImportedObjectEvidenceIdentity("snapshot=alpha|root=primary|object=fixture-1");
        var (objectReference, imported) = memoryModel.ImportObject(ConcreteMemory.Empty, objectType, evidence);

        var missing = memoryModel.LoadField(imported, objectReference, field);
        var populated = memoryModel.ImportField(imported, objectReference, field, domain.ConstInt32(37));
        var exact = memoryModel.LoadField(populated, objectReference, field);

        Assert.Equal(MemoryLoadKind.Unavailable, missing.Kind);
        Assert.Equal("MEMORY_IMPORTED_FIELD_UNAVAILABLE", missing.FailureCode);
        Assert.Null(missing.Exception);
        Assert.Throws<InvalidOperationException>(() => _ = missing.Value);
        AssertExactInt32(37, exact);
        Assert.Equal(MemoryLoadKind.Unavailable, memoryModel.LoadField(imported, objectReference, field).Kind);
        Assert.Equal(1, populated.ImportedObjectCount);
    }

    /// <summary>
    /// Checks that imported object identity is retained, bounded, idempotent, and part of semantic memory equality.
    /// </summary>
    [Fact]
    public void ImportedObjectEvidenceIdentityIsDeterministicRetainedAndBounded()
    {
        var objectType = ObjectType();
        var evidence = new ImportedObjectEvidenceIdentity("snapshot=alpha|runtime=0|root=primary");
        var (firstReference, first) = memoryModel.ImportObject(ConcreteMemory.Empty, objectType, evidence);
        var (replayedReference, replayed) = memoryModel.ImportObject(
            ConcreteMemory.Empty.Fork(),
            ObjectType("Fixture.Renamed"),
            new ImportedObjectEvidenceIdentity(evidence.Value));
        var (idempotentReference, idempotent) = memoryModel.ImportObject(first, objectType, evidence);
        var (_, otherEvidence) = memoryModel.ImportObject(
            ConcreteMemory.Empty,
            objectType,
            new ImportedObjectEvidenceIdentity("snapshot=beta|runtime=0|root=primary"));

        Assert.Equal(firstReference, replayedReference);
        Assert.Equal(firstReference, idempotentReference);
        Assert.Same(first, idempotent);
        Assert.Equal(first, replayed);
        Assert.Equal(first.GetHashCode(), replayed.GetHashCode());
        Assert.NotEqual(first, otherEvidence);
        Assert.True(memoryModel.TryGetImportedObjectEvidenceIdentity(first, firstReference, out var retained));
        Assert.Equal(evidence, retained);
        Assert.Throws<ArgumentException>(() => new ImportedObjectEvidenceIdentity(" "));
        Assert.Throws<ArgumentException>(() => new ImportedObjectEvidenceIdentity("line1\nline2"));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new ImportedObjectEvidenceIdentity(
                new string('e', ImportedObjectEvidenceIdentity.MaximumLength + 1)));
    }

    /// <summary>
    /// Checks that conflicting imports and non-exact imported values cannot overwrite prepared evidence.
    /// </summary>
    [Fact]
    public void ImportRejectsTypeConflictsValueConflictsAndNonExactEvidence()
    {
        var objectType = ObjectType();
        var otherType = TypeSig.CreateTypeDefinition(FixtureModule, 0x02000002, "Fixture.Other");
        var field = Field(objectType);
        var evidence = new ImportedObjectEvidenceIdentity("snapshot=alpha|object=fixture-1");
        var (objectReference, imported) = memoryModel.ImportObject(ConcreteMemory.Empty, objectType, evidence);
        var populated = memoryModel.ImportField(imported, objectReference, field, domain.ConstInt32(1));

        Assert.Throws<InvalidOperationException>(
            () => memoryModel.ImportObject(imported, otherType, evidence));
        Assert.Throws<InvalidOperationException>(
            () => memoryModel.ImportField(populated, objectReference, field, domain.ConstInt32(2)));
        Assert.Throws<ArgumentException>(
            () => memoryModel.ImportField(
                imported,
                objectReference,
                field,
                domain.Top(TypeSig.Int32)));
        Assert.Same(
            populated,
            memoryModel.ImportField(populated, objectReference, field, domain.ConstInt32(1)));
    }

    /// <summary>
    /// Checks that field stores require an exact declaring owner, exact value type, and ordinary instance storage.
    /// </summary>
    [Fact]
    public void FieldStoresValidateOwnerValueTypeAndStorageDisposition()
    {
        var objectType = ObjectType();
        var otherOwner = TypeSig.CreateTypeDefinition(OtherModule, 0x02000001, "Fixture.Object");
        var (objectReference, memory) = memoryModel.NewObject(ConcreteMemory.Empty, objectType);
        var wrongOwner = Field(otherOwner);
        var wrongValueType = Field(objectType, row: 2, fieldType: TypeSig.String);
        var staticField = Field(objectType, row: 3, isStatic: true);

        Assert.Equal(
            "field",
            Assert.Throws<ArgumentException>(
                () => memoryModel.StoreField(
                    memory,
                    objectReference,
                    wrongOwner,
                    domain.ConstInt32(1))).ParamName);
        Assert.Equal(
            "value",
            Assert.Throws<ArgumentException>(
                () => memoryModel.StoreField(
                    memory,
                    objectReference,
                    wrongValueType,
                    domain.ConstInt32(1))).ParamName);
        Assert.Equal(
            "field",
            Assert.Throws<ArgumentException>(
                () => memoryModel.StoreField(
                    memory,
                    objectReference,
                    staticField,
                    domain.ConstInt32(1))).ParamName);
        AssertExactInt32(0, memoryModel.LoadField(memory, objectReference, Field(objectType)));
    }

    /// <summary>
    /// Checks that a typed null field receiver produces structured target-exception information without a value.
    /// </summary>
    [Fact]
    public void TypedNullFieldLoadReturnsStructuredUnlocatedTargetException()
    {
        var objectType = ObjectType();
        var field = Field(objectType);
        var result = memoryModel.LoadField(
            ConcreteMemory.Empty,
            domain.ConstNull(objectType),
            field);

        Assert.Equal(MemoryLoadKind.TargetException, result.Kind);
        Assert.Equal("TARGET_NULL_REFERENCE", result.FailureCode);
        Assert.NotNull(result.Exception);
        Assert.Equal(TargetExceptionKind.NullReference, result.Exception.Kind);
        Assert.Null(result.Exception.Method);
        Assert.Null(result.Exception.IlOffset);
        Assert.Throws<InvalidOperationException>(() => _ = result.Value);

        var method = new MethodHandle(FixtureModule, 0x06000001);
        var located = result.Exception.WithLocation(method, 7);
        Assert.Equal(method, located.Method);
        Assert.Equal(7, located.IlOffset);
        Assert.Throws<InvalidOperationException>(() => located.WithLocation(method, 8));
    }

    /// <summary>Checks that a differently typed null is invalid rather than misreported as the admitted target throw.</summary>
    [Fact]
    public void FieldLoadRejectsNullWhoseStructuralOwnerTypeDoesNotMatch()
    {
        var objectType = ObjectType();
        var otherType = TypeSig.CreateTypeDefinition(OtherModule, 0x02000001, "Fixture.Object");
        var result = memoryModel.LoadField(
            ConcreteMemory.Empty,
            domain.ConstNull(otherType),
            Field(objectType));

        Assert.Equal(MemoryLoadKind.Invalid, result.Kind);
        Assert.Equal("MEMORY_FIELD_OWNER_MISMATCH", result.FailureCode);
        Assert.Null(result.Exception);
    }

    /// <summary>Checks factory invariants for every typed memory-load outcome family.</summary>
    [Fact]
    public void MemoryLoadResultsKeepValuesExceptionsAndCodesMutuallyExclusive()
    {
        var exact = MemoryLoadResult<ConcreteValue>.Exact(domain.ConstInt32(5));
        var partial = MemoryLoadResult<ConcreteValue>.NonExact(MemoryLoadKind.Partial, "FIELD_BYTES_PARTIAL");
        var unavailable = MemoryLoadResult<ConcreteValue>.NonExact(
            MemoryLoadKind.Unavailable,
            "FIELD_BYTES_UNAVAILABLE");
        var conflict = MemoryLoadResult<ConcreteValue>.NonExact(
            MemoryLoadKind.Conflict,
            "FIELD_BYTES_CONFLICT");
        var invalid = MemoryLoadResult<ConcreteValue>.NonExact(MemoryLoadKind.Invalid, "FIELD_REQUEST_INVALID");
        var targetException = MemoryLoadResult<ConcreteValue>.ForTargetException(
            new TargetExceptionInfo(TargetExceptionKind.NullReference, "TARGET_NULL_REFERENCE"));

        Assert.Equal(MemoryLoadKind.Exact, exact.Kind);
        Assert.Null(exact.FailureCode);
        Assert.Null(exact.Exception);
        AssertInt32(5, exact.Value);
        Assert.All(
            new[] { partial, unavailable, conflict, invalid },
            result =>
            {
                Assert.NotEqual(MemoryLoadKind.Exact, result.Kind);
                Assert.NotNull(result.FailureCode);
                Assert.Null(result.Exception);
                Assert.Throws<InvalidOperationException>(() => _ = result.Value);
            });
        Assert.Equal(MemoryLoadKind.TargetException, targetException.Kind);
        Assert.NotNull(targetException.Exception);
        Assert.Throws<ArgumentOutOfRangeException>(
            () => MemoryLoadResult<ConcreteValue>.NonExact(MemoryLoadKind.Exact, "INVALID_FACTORY_KIND"));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => MemoryLoadResult<ConcreteValue>.NonExact(
                MemoryLoadKind.TargetException,
                "INVALID_FACTORY_KIND"));
        Assert.Throws<ArgumentException>(
            () => MemoryLoadResult<ConcreteValue>.NonExact(MemoryLoadKind.Invalid, " "));
    }

    /// <summary>Checks deterministic reference assignment for equal allocations replayed from one ancestor.</summary>
    [Fact]
    public void EqualAllocationBranchesProduceEqualReferencesAndSnapshots()
    {
        var type = ObjectType();
        var (leftReference, left) = memoryModel.NewObject(ConcreteMemory.Empty, type);
        var (rightReference, right) = memoryModel.NewObject(
            ConcreteMemory.Empty.Fork(),
            ObjectType("Fixture.DiagnosticRename"));

        Assert.Equal(leftReference, rightReference);
        Assert.Equal(left, right);
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
        Assert.False(memoryModel.TryGetImportedObjectEvidenceIdentity(left, leftReference, out var evidence));
        Assert.Null(evidence);
    }

    /// <summary>Checks content equality and stable hash equality for independently replayed operation sequences.</summary>
    [Fact]
    public void EqualOperationSequencesProduceEqualSnapshotsAndStableHashes()
    {
        var left = BuildSnapshot("Fixture.Object");
        var right = BuildSnapshot("Fixture.Renamed");

        Assert.NotSame(left, right);
        Assert.Equal(left, right);
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
    }

    /// <summary>Checks that object and array writes create isolated descendant snapshots after a fork.</summary>
    [Fact]
    public void ArrayStoresPreserveEarlierSnapshotsAndUseExactDefaults()
    {
        var (arrayReference, memory) = memoryModel.NewArray(
            ConcreteMemory.Empty,
            TypeSig.Int32,
            domain.ConstInt32(2));
        var changed = memoryModel.StoreElement(
            memory.Fork(),
            arrayReference,
            domain.ConstInt32(1),
            domain.ConstInt32(99));

        AssertInt32(0, memoryModel.LoadElement(memory, arrayReference, domain.ConstInt32(1)));
        AssertInt32(99, memoryModel.LoadElement(changed, arrayReference, domain.ConstInt32(1)));
        Assert.NotEqual(memory, changed);
    }

    /// <summary>Checks that lengths and values outside the bounded exact array contract are rejected.</summary>
    [Fact]
    public void ArrayOperationsRejectUnboundedLengthsAndWrongStaticElementTypes()
    {
        var lengthException = Assert.Throws<ArgumentException>(() => memoryModel.NewArray(
            ConcreteMemory.Empty,
            TypeSig.Int32,
            domain.ConstInt32(ConcreteMemoryModel.MaxArrayLength + 1)));
        var (arrayReference, memory) = memoryModel.NewArray(
            ConcreteMemory.Empty,
            TypeSig.Int32,
            domain.ConstInt32(1));
        var valueException = Assert.Throws<ArgumentException>(() => memoryModel.StoreElement(
            memory,
            arrayReference,
            domain.ConstInt32(0),
            domain.ConstString("redacted payload")));

        Assert.Equal("length", lengthException.ParamName);
        Assert.Equal("value", valueException.ParamName);
        AssertInt32(0, memoryModel.LoadElement(memory, arrayReference, domain.ConstInt32(0)));
    }

    /// <summary>Checks that the self-typed persistent-memory interface remains an executable generic boundary.</summary>
    [Fact]
    public void PersistentMemoryBoundaryIsExecutableThroughSelfTypedConstraint()
    {
        var fork = ForkGeneric(ConcreteMemory.Empty);

        Assert.Same(ConcreteMemory.Empty, fork);
    }

    private ConcreteMemory BuildSnapshot(string diagnosticTypeName)
    {
        var type = ObjectType(diagnosticTypeName);
        var field = Field(type);
        var (reference, memory) = memoryModel.NewObject(ConcreteMemory.Empty, type);
        memory = memoryModel.StoreField(memory, reference, field, domain.ConstInt32(42));
        var (arrayReference, withArray) = memoryModel.NewArray(memory, TypeSig.Int32, domain.ConstInt32(1));
        return memoryModel.StoreElement(withArray, arrayReference, domain.ConstInt32(0), domain.ConstInt32(7));
    }

    private static TypeSig ObjectType(string displayName = "Fixture.Object") =>
        TypeSig.CreateTypeDefinition(FixtureModule, 0x02000001, displayName);

    private static ResolvedField Field(
        TypeSig owner,
        int row = 1,
        TypeSig? fieldType = null,
        bool isStatic = false,
        bool isLiteral = false,
        bool hasRva = false) =>
        new(
            new FieldHandle(owner.Module!.Value, 0x04000000 | row),
            owner,
            fieldType ?? TypeSig.Int32,
            isStatic,
            isLiteral,
            hasRva);

    private static TMemory ForkGeneric<TMemory>(TMemory memory)
        where TMemory : IPersistentMemoryState<TMemory> => memory.Fork();

    private void AssertExactInt32(int expected, MemoryLoadResult<ConcreteValue> result)
    {
        Assert.Equal(MemoryLoadKind.Exact, result.Kind);
        Assert.Null(result.FailureCode);
        Assert.Null(result.Exception);
        AssertInt32(expected, result.Value);
    }

    private void AssertInt32(int expected, ConcreteValue actual)
    {
        Assert.True(domain.TryGetConstInt32(actual, out var value));
        Assert.Equal(expected, value);
    }
}
