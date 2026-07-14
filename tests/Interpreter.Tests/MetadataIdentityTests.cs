using System.Buffers.Binary;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Interpreter.Core.Abstractions;
using Interpreter.Metadata.SRM;
using Xunit;

namespace Interpreter.Tests;

/// <summary>
/// Verifies path-independent SRM module identity, structural MethodDef handles, and explainable lookup failures.
/// </summary>
public sealed class MetadataIdentityTests
{
    /// <summary>Checks that byte-identical copies produce equal module and method identities at different paths.</summary>
    [Fact]
    public void CopiedImageHasSameModuleAndMethodDefinitionIdentity()
    {
        var sourcePath = Assembly.GetExecutingAssembly().Location;
        var temporaryDirectory = Path.Combine(Path.GetTempPath(), $"interpreter-metadata-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporaryDirectory);
        var firstPath = Path.Combine(temporaryDirectory, "first.dll");
        var secondPath = Path.Combine(temporaryDirectory, "renamed-copy.dll");
        File.Copy(sourcePath, firstPath);
        File.Copy(sourcePath, secondPath);

        try
        {
            using var first = SrmMetadataModule.LoadFromFile(firstPath);
            using var second = SrmMetadataModule.LoadFromFile(secondPath);
            var firstToken = first.FindMethodDefinition(nameof(MetadataIdentityTests), nameof(UniqueFixtureMethod));
            var secondToken = second.FindMethodDefinition(nameof(MetadataIdentityTests), nameof(UniqueFixtureMethod));

            Assert.True(firstToken.IsSuccess);
            Assert.True(secondToken.IsSuccess);
            Assert.Equal(first.Id, second.Id);
            Assert.Equal(first.Id.ContentIdentity, second.Id.ContentIdentity);
            Assert.NotNull(first.Id.ArtifactIdentity);
            Assert.Equal(first.Id.ArtifactIdentity, second.Id.ArtifactIdentity);
            Assert.Equal(first.ModuleHandle, second.ModuleHandle);
            Assert.NotEqual(first.Descriptor.PathHint, second.Descriptor.PathHint);

            var firstMethod = first.GetMethodHandle(firstToken.Value);
            var secondMethod = second.GetMethodHandle(secondToken.Value);
            Assert.True(firstMethod.IsSuccess);
            Assert.True(secondMethod.IsSuccess);
            Assert.Equal(firstMethod.Value, secondMethod.Value);
            Assert.Equal(firstToken.Value, firstMethod.Value.MetadataToken);

            var body = first.GetMethodBody(firstMethod.Value);
            Assert.True(body.IsSuccess);
            Assert.NotEmpty(body.Value.CodeBytes);
        }
        finally
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }

    /// <summary>Checks that patched IL cannot alias the original artifact, module handle, or method handle.</summary>
    [Fact]
    public void PatchedMethodBodyChangesCompleteArtifactAndExecutionIdentity()
    {
        var sourcePath = Assembly.GetExecutingAssembly().Location;
        var temporaryDirectory = Path.Combine(Path.GetTempPath(), $"interpreter-patched-pe-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporaryDirectory);
        var originalPath = Path.Combine(temporaryDirectory, "original.dll");
        var patchedPath = Path.Combine(temporaryDirectory, "patched.dll");
        File.Copy(sourcePath, originalPath);
        File.Copy(sourcePath, patchedPath);

        try
        {
            PatchUniqueFixtureConstant(patchedPath);
            using var original = SrmMetadataModule.LoadFromFile(originalPath);
            using var patched = SrmMetadataModule.LoadFromFile(patchedPath);

            Assert.Equal(original.Id.ContentIdentity, patched.Id.ContentIdentity);
            Assert.Equal(original.Id.PeStamp, patched.Id.PeStamp);
            Assert.NotEqual(original.Id.ArtifactIdentity, patched.Id.ArtifactIdentity);
            Assert.NotEqual(original.Id, patched.Id);
            Assert.NotEqual(original.ModuleHandle, patched.ModuleHandle);

            var originalToken = original.FindMethodDefinition(
                nameof(MetadataIdentityTests),
                nameof(UniqueFixtureMethod)).Value;
            var patchedToken = patched.FindMethodDefinition(
                nameof(MetadataIdentityTests),
                nameof(UniqueFixtureMethod)).Value;
            Assert.Equal(originalToken, patchedToken);
            Assert.NotEqual(
                original.GetMethodHandle(originalToken).Value,
                patched.GetMethodHandle(patchedToken).Value);
            Assert.Equal(
                new byte[] { 0x1F, 0x2A, 0x2A },
                original.GetMethodBody(original.GetMethodHandle(originalToken).Value).Value.CodeBytes.ToArray());
            Assert.Equal(
                new byte[] { 0x1F, 0x2B, 0x2A },
                patched.GetMethodBody(patched.GetMethodHandle(patchedToken).Value).Value.CodeBytes.ToArray());
        }
        finally
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }

    /// <summary>Checks that fixture lookup distinguishes absent types, absent methods, and overload ambiguity.</summary>
    [Fact]
    public void MethodDefinitionLookupDistinguishesMissingAndAmbiguousEvidence()
    {
        using var module = SrmMetadataModule.LoadFromFile(Assembly.GetExecutingAssembly().Location);

        const string missingTypeCanary = "secret-type-name-canary";
        const string missingMethodCanary = "secret-method-name-canary";
        var missingType = module.FindMethodDefinition(missingTypeCanary, missingMethodCanary);
        var missingMethod = module.FindMethodDefinition(nameof(MetadataIdentityTests), missingMethodCanary);
        var ambiguous = module.FindMethodDefinition(nameof(MetadataIdentityTests), nameof(OverloadedFixture));

        Assert.Equal(ResolutionFailureKind.Unavailable, missingType.Failure!.Kind);
        Assert.Equal("META_TYPE_NOT_FOUND", missingType.Failure.Code);
        Assert.Equal(ResolutionFailureKind.Unavailable, missingMethod.Failure!.Kind);
        Assert.Equal("META_METHOD_NOT_FOUND", missingMethod.Failure.Code);
        Assert.Equal(ResolutionFailureKind.Conflict, ambiguous.Failure!.Kind);
        Assert.Equal("META_AMBIGUOUS_METHOD", ambiguous.Failure.Code);
        Assert.DoesNotContain(missingTypeCanary, missingType.Failure.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(missingMethodCanary, missingType.Failure.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(missingMethodCanary, missingMethod.Failure.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(MetadataIdentityTests), missingMethod.Failure.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(OverloadedFixture), ambiguous.Failure.Message, StringComparison.Ordinal);
    }

    /// <summary>Checks lookup-order independence and structured rejection of non-MethodDef tokens.</summary>
    [Fact]
    public void MethodHandlesAreIndependentOfLookupOrderAndInvalidTokensAreStructured()
    {
        var path = Assembly.GetExecutingAssembly().Location;
        using var first = SrmMetadataModule.LoadFromFile(path);
        using var second = SrmMetadataModule.LoadFromFile(path);

        var uniqueToken = first.FindMethodDefinition(nameof(MetadataIdentityTests), nameof(UniqueFixtureMethod)).Value;
        var otherToken = first.FindMethodDefinition(nameof(MetadataIdentityTests), nameof(OtherFixtureMethod)).Value;
        var uniqueFirst = first.GetMethodHandle(uniqueToken).Value;
        _ = first.GetMethodHandle(otherToken).Value;
        _ = second.GetMethodHandle(otherToken).Value;
        var uniqueSecond = second.GetMethodHandle(uniqueToken).Value;
        var invalid = first.GetMethodHandle(unchecked((int)0x0AFFFFFF));

        Assert.Equal(uniqueFirst, uniqueSecond);
        Assert.False(invalid.IsSuccess);
        Assert.Equal(ResolutionFailureKind.Invalid, invalid.Failure!.Kind);
        Assert.Equal("META_INVALID_METHOD_TOKEN", invalid.Failure.Code);
    }

    /// <summary>Checks that metadata operations cannot outlive their artifact stream and disposal is idempotent.</summary>
    [Fact]
    public void MetadataOperationsRejectUseAfterDispose()
    {
        var module = SrmMetadataModule.LoadFromFile(Assembly.GetExecutingAssembly().Location);
        var token = module.FindMethodDefinition(nameof(MetadataIdentityTests), nameof(UniqueFixtureMethod)).Value;

        module.Dispose();
        module.Dispose();

        Assert.Throws<ObjectDisposedException>(() => module.FindMethodDefinition(
            nameof(MetadataIdentityTests),
            nameof(UniqueFixtureMethod)));
        Assert.Throws<ObjectDisposedException>(() => module.GetMethodHandle(token));
        Assert.Throws<ObjectDisposedException>(() => module.GetMethodBody(
            new MethodHandle(module.ModuleHandle, token)));
    }

    /// <summary>Checks that artifact binding distinguishes MVID, length, and exact metadata-byte conflicts.</summary>
    [Fact]
    public void ModuleContentBindingRejectsEveryIdentityConflict()
    {
        var mvid = new Guid("10000000-0000-0000-0000-000000000001");
        var observed = ModuleContentIdentity.FromMetadata(mvid, [1, 2, 3, 4]);
        var same = ModuleContentIdentity.FromDigest(
            mvid,
            observed.MetadataLength,
            observed.MetadataSha256.ToUpperInvariant());
        var differentMvid = ModuleContentIdentity.FromMetadata(Guid.NewGuid(), [1, 2, 3, 4]);
        var differentLength = ModuleContentIdentity.FromMetadata(mvid, [1, 2, 3, 4, 5]);
        var differentHash = ModuleContentIdentity.FromMetadata(mvid, [4, 3, 2, 1]);

        Assert.True(observed.VerifyMatches(same).IsSuccess);
        Assert.Equal("MODULE_MVID_CONFLICT", observed.VerifyMatches(differentMvid).Failure!.Code);
        Assert.Equal("MODULE_METADATA_LENGTH_CONFLICT", observed.VerifyMatches(differentLength).Failure!.Code);
        Assert.Equal("MODULE_METADATA_HASH_CONFLICT", observed.VerifyMatches(differentHash).Failure!.Code);
        Assert.Throws<ArgumentException>(() => ModuleContentIdentity.FromDigest(mvid, 4, "not-a-digest"));
    }

    /// <summary>Checks that missing and malformed external PE artifacts produce typed, payload-safe outcomes.</summary>
    [Fact]
    public void ExternalArtifactOpenClassifiesMissingAndMalformedPeContent()
    {
        var missingPath = Path.Combine(Path.GetTempPath(), $"missing-pe-{Guid.NewGuid():N}.dll");
        var missing = SrmMetadataModule.Open(missingPath);
        Assert.False(missing.IsSuccess);
        Assert.Equal(ResolutionFailureKind.Unavailable, missing.Failure!.Kind);
        Assert.Equal("META_ARTIFACT_UNAVAILABLE", missing.Failure.Code);
        Assert.DoesNotContain(missingPath, missing.Failure.Message, StringComparison.Ordinal);

        const string invalidPathCanary = "secret-path-canary\0.dll";
        var invalidPath = SrmMetadataModule.Open(invalidPathCanary);
        Assert.False(invalidPath.IsSuccess);
        Assert.Equal(ResolutionFailureKind.Invalid, invalidPath.Failure!.Kind);
        Assert.Equal("META_ARTIFACT_PATH_INVALID", invalidPath.Failure.Code);
        Assert.DoesNotContain("secret-path-canary", invalidPath.Failure.Message, StringComparison.Ordinal);

        var malformedPath = Path.Combine(Path.GetTempPath(), $"malformed-pe-{Guid.NewGuid():N}.dll");
        var truncatedPath = Path.Combine(Path.GetTempPath(), $"truncated-pe-{Guid.NewGuid():N}.dll");
        try
        {
            File.WriteAllBytes(malformedPath, "not a managed PE"u8.ToArray());
            var malformed = SrmMetadataModule.Open(malformedPath);
            Assert.False(malformed.IsSuccess);
            Assert.Equal(ResolutionFailureKind.Invalid, malformed.Failure!.Kind);
            Assert.Equal("META_ARTIFACT_INVALID", malformed.Failure.Code);
            Assert.DoesNotContain(malformedPath, malformed.Failure.Message, StringComparison.Ordinal);

            var source = File.ReadAllBytes(Assembly.GetExecutingAssembly().Location);
            File.WriteAllBytes(truncatedPath, source.AsSpan(0, Math.Min(512, source.Length)).ToArray());
            var truncated = SrmMetadataModule.Open(truncatedPath);
            Assert.False(truncated.IsSuccess);
            Assert.Equal(ResolutionFailureKind.Invalid, truncated.Failure!.Kind);
            Assert.Equal("META_ARTIFACT_INVALID", truncated.Failure.Code);
            Assert.DoesNotContain(truncatedPath, truncated.Failure.Message, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(malformedPath);
            File.Delete(truncatedPath);
        }
    }

    /// <summary>Checks the PE size gate against the actually opened stream before hashing or parsing.</summary>
    [Fact]
    public void ExternalArtifactOpenRejectsOversizedPeBeforeHashing()
    {
        var oversizedPath = Path.Combine(Path.GetTempPath(), $"oversized-pe-{Guid.NewGuid():N}.dll");
        try
        {
            using (var stream = new FileStream(oversizedPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.SetLength((512L * 1024 * 1024) + 1);
            }

            var result = SrmMetadataModule.Open(oversizedPath);

            Assert.False(result.IsSuccess);
            Assert.Equal(ResolutionFailureKind.Unsupported, result.Failure!.Kind);
            Assert.Equal("META_ARTIFACT_SIZE_LIMIT", result.Failure.Code);
            Assert.DoesNotContain(oversizedPath, result.Failure.Message, StringComparison.Ordinal);
            Assert.ThrowsAny<NotSupportedException>(() => SrmMetadataModule.LoadFromFile(oversizedPath));
        }
        finally
        {
            File.Delete(oversizedPath);
        }
    }

    /// <summary>Checks bounded fixture lookup rejects oversized caller-controlled names before table traversal.</summary>
    [Fact]
    public void MethodDefinitionLookupBoundsUntrustedNames()
    {
        using var module = SrmMetadataModule.LoadFromFile(Assembly.GetExecutingAssembly().Location);

        var result = module.FindMethodDefinition(new string('T', 1025), "Method");

        Assert.False(result.IsSuccess);
        Assert.Equal(ResolutionFailureKind.Invalid, result.Failure!.Kind);
        Assert.Equal("META_METHOD_LOOKUP_TOO_LONG", result.Failure.Code);
    }

    private static int UniqueFixtureMethod() => 42;

    private static int OtherFixtureMethod() => 43;

    private static int OverloadedFixture(int value) => value;

    private static string OverloadedFixture(string value) => value;

    private static void PatchUniqueFixtureConstant(string path)
    {
        var image = File.ReadAllBytes(path);
        using var peReader = new PEReader(new MemoryStream(image, writable: false));
        var metadata = peReader.GetMetadataReader();
        var methodHandle = metadata.TypeDefinitions
            .Select(metadata.GetTypeDefinition)
            .Where(type => string.Equals(
                metadata.GetString(type.Name),
                nameof(MetadataIdentityTests),
                StringComparison.Ordinal))
            .SelectMany(type => type.GetMethods())
            .Single(handle => string.Equals(
                metadata.GetString(metadata.GetMethodDefinition(handle).Name),
                nameof(UniqueFixtureMethod),
                StringComparison.Ordinal));
        var method = metadata.GetMethodDefinition(methodHandle);
        var body = peReader.GetMethodBody(method.RelativeVirtualAddress);
        Assert.Equal(new byte[] { 0x1F, 0x2A, 0x2A }, body.GetILBytes());

        var section = peReader.PEHeaders.SectionHeaders.Single(candidate =>
            method.RelativeVirtualAddress >= candidate.VirtualAddress &&
            method.RelativeVirtualAddress < candidate.VirtualAddress + Math.Max(candidate.VirtualSize, candidate.SizeOfRawData));
        var headerOffset = checked(
            section.PointerToRawData + method.RelativeVirtualAddress - section.VirtualAddress);
        var firstHeaderByte = image[headerOffset];
        var headerSize = (firstHeaderByte & 0x03) == 0x02
            ? 1
            : checked((BinaryPrimitives.ReadUInt16LittleEndian(image.AsSpan(headerOffset, sizeof(ushort))) >> 12) * 4);
        var codeOffset = checked(headerOffset + headerSize);
        Assert.Equal(0x1F, image[codeOffset]);
        Assert.Equal(42, image[codeOffset + 1]);
        image[codeOffset + 1] = 43;
        File.WriteAllBytes(path, image);
    }
}
