using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Interpreter.Core.Abstractions;
using Interpreter.Core.Execution;
using ModuleHandle = Interpreter.Core.Abstractions.ModuleHandle;
using Xunit;

namespace Interpreter.Tests;

/// <summary>Exercises the backend-neutral evidence carried by approximate field loads.</summary>
public sealed class FieldLoadEvidenceTests
{
    private const string SourceSha256 =
        "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private const string ImportedObjectSha256 =
        "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789";

    private static readonly ModuleHandle Module = ModuleHandle.FromContentIdentity(
        ModuleContentIdentity.FromMetadata(
            new Guid("00000000-0000-0000-0000-000000000431"),
            "FieldLoadEvidenceTests-W4.3"u8),
        43,
        86);

    private static readonly TypeSig DeclaringType = TypeSig.CreateTypeDefinition(
        Module,
        0x02000001,
        "Interpreter.Tests.FieldEvidenceFixture");

    private static readonly ResolvedField Int32Field = CreateField();

    /// <summary>Checks every admitted partial prefix is retained exactly and copied from mutable caller storage.</summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void PartialEvidenceRetainsEveryFactAndDefensivelyCopiesObservedBytes(int observedLength)
    {
        var source = Enumerable.Range(1, observedLength).Select(static value => (byte)value).ToArray();
        var expected = source.ToArray();

        var evidence = CreateEvidence(observedBytes: source);
        source[0] = 0xFF;

        Assert.Equal(0, evidence.DependencyOrdinal);
        Assert.Equal(Int32Field, evidence.Field);
        Assert.Equal(EvaluationEvidenceStatus.Partial, evidence.EvidenceStatus);
        Assert.Equal("W4.Field.Partial", evidence.ReasonCode);
        Assert.Equal(SourceSha256, evidence.SourceSha256);
        Assert.Equal(ImportedObjectSha256, evidence.ImportedObjectSha256);
        Assert.Equal(0x0000_0001_2345_6780UL, evidence.Address);
        Assert.Equal(sizeof(int), evidence.RequestedLength);
        Assert.Equal(observedLength, evidence.ObservedLength);
        Assert.Equal(expected, evidence.ObservedBytes);
        Assert.Equal(64, evidence.Sha256.Length);
        Assert.Equal(
            Convert.ToHexString(SHA256.HashData(evidence.CanonicalBytes.AsSpan())).ToLowerInvariant(),
            evidence.Sha256);
    }

    /// <summary>
    /// Proves mutation of arrays recovered from both public byte projections cannot alter retained evidence,
    /// equality, or canonical identity.
    /// </summary>
    [Fact]
    public void PublicByteProjectionBackingCannotMutateEvidenceOrCanonicalIdentity()
    {
        var evidence = CreateEvidence(observedBytes: [0x10, 0x20, 0x30]);
        var equalEvidence = CreateEvidence(observedBytes: [0x10, 0x20, 0x30]);
        var expectedObserved = evidence.ObservedBytes.ToArray();
        var expectedCanonical = evidence.CanonicalBytes.ToArray();
        var expectedSha256 = evidence.Sha256;
        var expectedHashCode = evidence.GetHashCode();

        var visibleObserved = evidence.ObservedBytes;
        var visibleObservedBacking = ImmutableCollectionsMarshal.AsArray(visibleObserved)!;
        visibleObservedBacking[0] ^= 0xff;

        var visibleCanonical = evidence.CanonicalBytes;
        var visibleCanonicalBacking = ImmutableCollectionsMarshal.AsArray(visibleCanonical)!;
        visibleCanonicalBacking[0] ^= 0xff;

        Assert.Equal(expectedObserved, evidence.ObservedBytes);
        Assert.Equal(expectedCanonical, evidence.CanonicalBytes);
        Assert.Equal(expectedSha256, evidence.Sha256);
        Assert.Equal(expectedHashCode, evidence.GetHashCode());
        Assert.Equal(equalEvidence, evidence);
        Assert.Equal(
            Convert.ToHexString(SHA256.HashData(evidence.CanonicalBytes.AsSpan())).ToLowerInvariant(),
            evidence.Sha256);
    }

    /// <summary>Checks unavailable evidence retains a truthful empty observation rather than inventing bytes.</summary>
    [Fact]
    public void UnavailableEvidenceRequiresAndRetainsAnEmptyObservation()
    {
        var evidence = CreateEvidence(
            evidenceStatus: EvaluationEvidenceStatus.Unavailable,
            reasonCode: "W4.Field.Unavailable",
            observedBytes: []);

        Assert.Equal(EvaluationEvidenceStatus.Unavailable, evidence.EvidenceStatus);
        Assert.Equal("W4.Field.Unavailable", evidence.ReasonCode);
        Assert.Equal(0, evidence.ObservedLength);
        Assert.Empty(evidence.ObservedBytes);
        Assert.False(evidence.CanonicalBytes.IsDefaultOrEmpty);
    }

    /// <summary>Checks every semantic evidence axis contributes to identity while diagnostic type names do not.</summary>
    [Fact]
    public void CanonicalIdentityIncludesEverySemanticAxisAndExcludesDisplayNames()
    {
        var baseline = CreateEvidence(observedBytes: [0x10, 0x20]);
        var variants = new[]
        {
            CreateEvidence(dependencyOrdinal: 1, observedBytes: [0x10, 0x20]),
            CreateEvidence(field: CreateField(metadataToken: 0x04000002), observedBytes: [0x10, 0x20]),
            CreateEvidence(field: CreateField(declaringTypeToken: 0x02000002), observedBytes: [0x10, 0x20]),
            CreateEvidence(
                evidenceStatus: EvaluationEvidenceStatus.Unavailable,
                reasonCode: "W4.Field.Unavailable",
                observedBytes: []),
            CreateEvidence(reasonCode: "W4.Field.ShortRead", observedBytes: [0x10, 0x20]),
            CreateEvidence(sourceSha256: HashOf('1'), observedBytes: [0x10, 0x20]),
            CreateEvidence(importedObjectSha256: HashOf('2'), observedBytes: [0x10, 0x20]),
            CreateEvidence(address: 0x0000_0001_2345_6790UL, observedBytes: [0x10, 0x20]),
            CreateEvidence(observedBytes: [0x11, 0x20]),
            CreateEvidence(observedBytes: [0x10, 0x20, 0x30]),
        };

        Assert.All(variants, variant => Assert.NotEqual(baseline.Sha256, variant.Sha256));
        Assert.Equal(variants.Length, variants.Select(static item => item.Sha256).Distinct().Count());

        var renamedType = TypeSig.CreateTypeDefinition(
            Module,
            DeclaringType.MetadataToken,
            "A.Different.Diagnostic.Name");
        var renamedField = new ResolvedField(
            Int32Field.Handle,
            renamedType,
            TypeSig.Int32,
            isStatic: false,
            isLiteral: false,
            hasRva: false);
        var renamed = CreateEvidence(field: renamedField, observedBytes: [0x10, 0x20]);

        Assert.Equal(baseline.Sha256, renamed.Sha256);
        Assert.True(baseline.CanonicalBytes.AsSpan().SequenceEqual(renamed.CanonicalBytes.AsSpan()));
    }

    /// <summary>Checks fresh equal evidence obeys equality, operator, hashing, and diagnostic contracts.</summary>
    [Fact]
    public void FreshCanonicalEvidenceHasValueEqualityAndStableHashing()
    {
        var first = CreateEvidence(observedBytes: [0x10, 0x20]);
        var second = CreateEvidence(
            field: CreateField(),
            sourceSha256: SourceSha256.ToUpperInvariant(),
            importedObjectSha256: ImportedObjectSha256.ToUpperInvariant(),
            observedBytes: [0x10, 0x20]);
        var different = CreateEvidence(reasonCode: "W4.Field.Other", observedBytes: [0x10, 0x20]);

        Assert.NotSame(first, second);
        Assert.Equal(first, second);
        Assert.True(first == second);
        Assert.False(first != second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
        Assert.Contains(second, new HashSet<FieldLoadEvidence> { first });
        Assert.Equal(first.Sha256, first.ToString());
        Assert.True(first.CanonicalBytes.AsSpan().SequenceEqual(second.CanonicalBytes.AsSpan()));

        Assert.NotEqual(first, different);
        Assert.False(first == different);
        Assert.True(first != different);
        Assert.False(first == null);
        Assert.True(first != null);
        Assert.True((FieldLoadEvidence?)null == null);
    }

    /// <summary>Checks field evidence is required exactly for precision-loss debug events.</summary>
    [Fact]
    public void DebugEventEnforcesItsFieldEvidenceUnionAndBothDeconstructionShapes()
    {
        var evidence = CreateEvidence(observedBytes: [0x10, 0x20]);
        var equalEvidence = CreateEvidence(observedBytes: [0x10, 0x20]);
        var method = new MethodHandle(Module, 0x06000001);
        var precisionLost = new DebugEvent(
            DebugEventKind.ValuePrecisionLost,
            method,
            1,
            "LoadField",
            evidence);

        Assert.Equal(
            precisionLost,
            new DebugEvent(DebugEventKind.ValuePrecisionLost, method, 1, "LoadField", equalEvidence));
        Assert.Same(evidence, precisionLost.FieldEvidence);
        var (kind, eventMethod, offset, instruction, fieldEvidence) = precisionLost;
        Assert.Equal(DebugEventKind.ValuePrecisionLost, kind);
        Assert.Equal(method, eventMethod);
        Assert.Equal(1, offset);
        Assert.Equal("LoadField", instruction);
        Assert.Same(evidence, fieldEvidence);

        precisionLost.Deconstruct(
            out var legacyKind,
            out var legacyMethod,
            out var legacyOffset,
            out var legacyInstruction);
        Assert.Equal(kind, legacyKind);
        Assert.Equal(eventMethod, legacyMethod);
        Assert.Equal(offset, legacyOffset);
        Assert.Equal(instruction, legacyInstruction);

        Assert.Throws<ArgumentException>(() => new DebugEvent(
            DebugEventKind.ValuePrecisionLost,
            method,
            1,
            "LoadField"));
        foreach (var ordinaryKind in Enum.GetValues<DebugEventKind>()
                     .Where(static item => item != DebugEventKind.ValuePrecisionLost))
        {
            var ordinary = new DebugEvent(ordinaryKind, method, 1, "LoadField");
            Assert.Null(ordinary.FieldEvidence);
            Assert.Throws<ArgumentException>(() => new DebugEvent(
                ordinaryKind,
                method,
                1,
                "LoadField",
                evidence));
        }

        Assert.Throws<ArgumentOutOfRangeException>(() => new DebugEvent(
            (DebugEventKind)int.MaxValue,
            method,
            1,
            "LoadField"));
    }

    /// <summary>Checks ordinals and evidence classifications outside the approximate-field vocabulary are rejected.</summary>
    [Fact]
    public void ConstructorRejectsInvalidDependencyOrdinalsAndEvidenceStatuses()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateEvidence(dependencyOrdinal: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateEvidence(
            evidenceStatus: (EvaluationEvidenceStatus)int.MaxValue));

        foreach (var status in new[]
                 {
                     EvaluationEvidenceStatus.Exact,
                     EvaluationEvidenceStatus.Conflict,
                     EvaluationEvidenceStatus.Invalid,
                 })
        {
            Assert.Throws<ArgumentException>(() => CreateEvidence(evidenceStatus: status));
        }
    }

    /// <summary>Checks source and imported-object identities must be complete canonical SHA-256 values.</summary>
    [Fact]
    public void ConstructorRejectsMissingTruncatedAndMalformedHashes()
    {
        var invalidHashes = new[]
        {
            string.Empty,
            " ",
            new string('0', 63),
            new string('0', 65),
            new string('g', 64),
        };

        foreach (var invalid in invalidHashes)
        {
            Assert.Throws<ArgumentException>(() => CreateEvidence(sourceSha256: invalid));
            Assert.Throws<ArgumentException>(() => CreateEvidence(importedObjectSha256: invalid));
        }

        var uppercase = CreateEvidence(
            sourceSha256: SourceSha256.ToUpperInvariant(),
            importedObjectSha256: ImportedObjectSha256.ToUpperInvariant());
        Assert.Equal(SourceSha256, uppercase.SourceSha256);
        Assert.Equal(ImportedObjectSha256, uppercase.ImportedObjectSha256);
    }

    /// <summary>Checks reason codes are bounded stable identifiers rather than host prose.</summary>
    [Fact]
    public void ConstructorRejectsNonCanonicalReasonCodes()
    {
        var invalidCodes = new[]
        {
            string.Empty,
            " ",
            ".leading",
            "trailing.",
            "double..separator",
            "contains space",
            "contains/slash",
            "contains\ncontrol",
            new string('a', 129),
        };

        foreach (var invalid in invalidCodes)
        {
            Assert.Throws<ArgumentException>(() => CreateEvidence(reasonCode: invalid));
        }
    }

    /// <summary>Checks W4.3 admits only ordinary instance Int32 FieldDefs for approximate transfer.</summary>
    [Fact]
    public void ConstructorRejectsWrongFieldTypeAndStorageGeometry()
    {
        var invalidFields = new[]
        {
            CreateField(fieldType: TypeSig.Int64),
            CreateField(isStatic: true),
            CreateField(isLiteral: true),
            CreateField(hasRva: true),
        };

        foreach (var field in invalidFields)
        {
            Assert.Throws<ArgumentException>(() => CreateEvidence(field: field));
        }

        Assert.Throws<ArgumentNullException>(() => new FieldLoadEvidence(
            0,
            null!,
            EvaluationEvidenceStatus.Partial,
            "W4.Field.Partial",
            SourceSha256,
            ImportedObjectSha256,
            0x1000,
            sizeof(int),
            [0x01]));
    }

    /// <summary>Checks addresses, ranges, requested width, and status-specific observed lengths cannot conflict.</summary>
    [Fact]
    public void ConstructorRejectsInvalidAddressRangeAndReadGeometry()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateEvidence(address: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateEvidence(address: ulong.MaxValue - 2));

        foreach (var requestedLength in new[] { -1, 0, 1, 3, 5 })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => CreateEvidence(requestedLength: requestedLength));
        }

        Assert.Throws<ArgumentException>(() => CreateEvidence(observedBytes: []));
        Assert.Throws<ArgumentException>(() => CreateEvidence(observedBytes: [1, 2, 3, 4]));
        Assert.Throws<ArgumentException>(() => CreateEvidence(observedBytes: [1, 2, 3, 4, 5]));
        Assert.Throws<ArgumentException>(() => CreateEvidence(
            evidenceStatus: EvaluationEvidenceStatus.Unavailable,
            reasonCode: "W4.Field.Unavailable",
            observedBytes: [1]));
    }

    /// <summary>Checks structured evidence occupies only the Partial or Unavailable branch of the load-result union.</summary>
    [Fact]
    public void FieldEvidenceFactoryPreservesUnionExclusivity()
    {
        var partialEvidence = CreateEvidence(observedBytes: [0x01, 0x02, 0x03]);
        var unavailableEvidence = CreateEvidence(
            evidenceStatus: EvaluationEvidenceStatus.Unavailable,
            reasonCode: "W4.Field.Unavailable",
            observedBytes: []);

        foreach (var item in new[]
                 {
                     (Evidence: partialEvidence, Kind: MemoryLoadKind.Partial),
                     (Evidence: unavailableEvidence, Kind: MemoryLoadKind.Unavailable),
                 })
        {
            var result = MemoryLoadResult<object>.FromFieldEvidence(item.Evidence);

            Assert.Equal(item.Kind, result.Kind);
            Assert.Same(item.Evidence, result.FieldEvidence);
            Assert.Equal(item.Evidence.ReasonCode, result.FailureCode);
            Assert.Null(result.Exception);
            Assert.Throws<InvalidOperationException>(() => _ = result.Value);
        }

        Assert.Throws<ArgumentNullException>(() =>
            MemoryLoadResult<object>.FromFieldEvidence(null!));
    }

    /// <summary>Checks exact, code-only, invalid, conflict, target, and default results never expose field evidence.</summary>
    [Fact]
    public void OtherMemoryLoadBranchesRemainCompatibleAndEvidenceFree()
    {
        var exactValue = new object();
        var exact = MemoryLoadResult<object>.Exact(exactValue);
        Assert.Equal(MemoryLoadKind.Exact, exact.Kind);
        Assert.Same(exactValue, exact.Value);
        Assert.Null(exact.FailureCode);
        Assert.Null(exact.Exception);
        Assert.Null(exact.FieldEvidence);

        foreach (var kind in new[]
                 {
                     MemoryLoadKind.Partial,
                     MemoryLoadKind.Unavailable,
                     MemoryLoadKind.Conflict,
                     MemoryLoadKind.Invalid,
                 })
        {
            var result = MemoryLoadResult<object>.NonExact(kind, $"MEM_{kind.ToString().ToUpperInvariant()}");
            Assert.Equal(kind, result.Kind);
            Assert.Null(result.FieldEvidence);
            Assert.Null(result.Exception);
            Assert.Throws<InvalidOperationException>(() => _ = result.Value);
        }

        var exception = new TargetExceptionInfo(TargetExceptionKind.NullReference, "MEM_NULL_RECEIVER");
        var target = MemoryLoadResult<object>.ForTargetException(exception);
        Assert.Equal(MemoryLoadKind.TargetException, target.Kind);
        Assert.Same(exception, target.Exception);
        Assert.Null(target.FieldEvidence);
        Assert.Throws<InvalidOperationException>(() => _ = target.Value);

        var invalidDefault = default(MemoryLoadResult<object>);
        Assert.Equal(MemoryLoadKind.Invalid, invalidDefault.Kind);
        Assert.Null(invalidDefault.FailureCode);
        Assert.Null(invalidDefault.Exception);
        Assert.Null(invalidDefault.FieldEvidence);
        Assert.Throws<InvalidOperationException>(() => _ = invalidDefault.Value);
    }

    private static FieldLoadEvidence CreateEvidence(
        int dependencyOrdinal = 0,
        ResolvedField? field = null,
        EvaluationEvidenceStatus evidenceStatus = EvaluationEvidenceStatus.Partial,
        string reasonCode = "W4.Field.Partial",
        string sourceSha256 = SourceSha256,
        string importedObjectSha256 = ImportedObjectSha256,
        ulong address = 0x0000_0001_2345_6780UL,
        int requestedLength = sizeof(int),
        byte[]? observedBytes = null) =>
        new(
            dependencyOrdinal,
            field ?? Int32Field,
            evidenceStatus,
            reasonCode,
            sourceSha256,
            importedObjectSha256,
            address,
            requestedLength,
            observedBytes ?? [0x01]);

    private static ResolvedField CreateField(
        int metadataToken = 0x04000001,
        int declaringTypeToken = 0x02000001,
        TypeSig? fieldType = null,
        bool isStatic = false,
        bool isLiteral = false,
        bool hasRva = false)
    {
        var declaringType = TypeSig.CreateTypeDefinition(
            Module,
            declaringTypeToken,
            $"Interpreter.Tests.FieldEvidenceFixture{declaringTypeToken}");
        return new ResolvedField(
            new FieldHandle(Module, metadataToken),
            declaringType,
            fieldType ?? TypeSig.Int32,
            isStatic,
            isLiteral,
            hasRva);
    }

    private static string HashOf(char character) => new(character, 64);
}
