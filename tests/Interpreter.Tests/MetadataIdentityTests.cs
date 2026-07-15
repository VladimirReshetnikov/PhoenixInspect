using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using Interpreter.Core.Abstractions;
using Interpreter.Metadata.SRM;
using Xunit;
using IlMethodBody = Interpreter.Core.Abstractions.MethodBody;
using ModuleHandle = Interpreter.Core.Abstractions.ModuleHandle;

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

            var firstDefinition = first.GetMethodDefinition(firstMethod.Value);
            var secondDefinition = second.GetMethodDefinition(secondMethod.Value);
            Assert.True(firstDefinition.IsSuccess);
            Assert.True(secondDefinition.IsSuccess);
            Assert.Equal(
                firstDefinition.Value.Signature.DeclaringType,
                secondDefinition.Value.Signature.DeclaringType);
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

            var originalDefinition = original.GetMethodDefinition(original.GetMethodHandle(originalToken).Value);
            var patchedDefinition = patched.GetMethodDefinition(patched.GetMethodHandle(patchedToken).Value);
            Assert.True(originalDefinition.IsSuccess);
            Assert.True(patchedDefinition.IsSuccess);
            Assert.NotEqual(
                originalDefinition.Value.Signature.DeclaringType,
                patchedDefinition.Value.Signature.DeclaringType);
            Assert.Equal(
                originalDefinition.Value.Signature.DeclaringType.DisplayName,
                patchedDefinition.Value.Signature.DeclaringType.DisplayName);
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

    /// <summary>
    /// Checks content equality for body-independent call signatures and managed-IL targets across independent arrays.
    /// </summary>
    [Fact]
    public void MethodCallSignaturesAndTargetsUseStructuralContentEquality()
    {
        var module = new ModuleHandle(0x4400000000000001, 0x4400000000000002);
        var declaringType = TypeSig.CreateTypeDefinition(module, 0x02000002, "Fixture.CallOwner");
        var firstSignature = new MethodCallSignatureShape(
            declaringType,
            MethodCallingConventionKind.Default,
            hasImplicitThis: false,
            hasExplicitThis: false,
            genericParameterCount: 0,
            ImmutableArray.Create(TypeSig.Int32, TypeSig.Int32),
            TypeSig.Int32);
        var secondSignature = new MethodCallSignatureShape(
            TypeSig.CreateTypeDefinition(module, 0x02000002, "Renamed.DiagnosticsOnly"),
            MethodCallingConventionKind.Default,
            hasImplicitThis: false,
            hasExplicitThis: false,
            genericParameterCount: 0,
            ImmutableArray.CreateRange(new[] { TypeSig.Int32, TypeSig.Int32 }),
            TypeSig.Int32);
        var method = new MethodHandle(module, 0x06000002);
        var firstTarget = new ResolvedMethodCallTarget(method, firstSignature);
        var secondTarget = new ResolvedMethodCallTarget(method, secondSignature);

        Assert.Equal(firstSignature, secondSignature);
        Assert.True(firstSignature == secondSignature);
        Assert.False(firstSignature != secondSignature);
        Assert.Equal(firstSignature.GetHashCode(), secondSignature.GetHashCode());
        Assert.Equal(firstTarget, secondTarget);
        Assert.Equal(firstTarget.GetHashCode(), secondTarget.GetHashCode());
        Assert.True(firstTarget.IsManagedIl);
        Assert.Throws<ArgumentException>(() => new ResolvedMethodCallTarget(
            new MethodHandle(new ModuleHandle(7, 8), 0x06000002),
            firstSignature));
    }

    /// <summary>
    /// Checks exact contextual MethodDef resolution and correlation with the independently acquired full definition.
    /// </summary>
    [Fact]
    public void DirectMethodResolutionReturnsExactBodyIndependentTarget()
    {
        using var module = SrmMetadataModule.LoadFromFile(Assembly.GetExecutingAssembly().Location);
        var contextToken = module.FindMethodDefinition(
            nameof(MetadataIdentityTests),
            nameof(UniqueFixtureMethod)).Value;
        var targetToken = module.FindMethodDefinition(
            nameof(MetadataIdentityTests),
            nameof(ExactDirectCallTargetFixture)).Value;
        var context = module.GetMethodHandle(contextToken).Value;

        var resolved = module.ResolveMethod(context, targetToken);

        Assert.True(resolved.IsSuccess, resolved.Failure?.Code);
        Assert.Equal(new MethodHandle(module.ModuleHandle, targetToken), resolved.Value.Method);
        Assert.True(resolved.Value.IsManagedIl);
        Assert.Equal(MethodCallingConventionKind.Default, resolved.Value.Signature.CallingConvention);
        Assert.False(resolved.Value.Signature.HasImplicitThis);
        Assert.False(resolved.Value.Signature.HasExplicitThis);
        Assert.Equal(0, resolved.Value.Signature.GenericParameterCount);
        Assert.Equal(new[] { TypeSig.Int32, TypeSig.Int32 }, resolved.Value.Signature.ParameterTypes);
        Assert.Equal(TypeSig.Int32, resolved.Value.Signature.ReturnType);

        var definition = module.GetMethodDefinition(resolved.Value.Method);
        Assert.True(definition.IsSuccess, definition.Failure?.Code);
        Assert.Equal(resolved.Value.Signature, definition.Value.Signature.CallSignature);

        IResolutionServices forwarded = new MetadataResolutionServices(module);
        var forwardedResult = forwarded.ResolveMethod(context, targetToken);
        Assert.True(forwardedResult.IsSuccess, forwardedResult.Failure?.Code);
        Assert.Equal(resolved.Value, forwardedResult.Value);
    }

    /// <summary>
    /// Checks stable classifications for forbidden token kinds, nil/out-of-range operands, foreign or generic
    /// contexts, and valid MethodDefs whose invocation shapes are outside the direct-call profile.
    /// </summary>
    [Fact]
    public void DirectMethodResolutionRejectsInvalidIdentityAndUnsupportedShapeMatrix()
    {
        var path = Assembly.GetExecutingAssembly().Location;
        using var module = SrmMetadataModule.LoadFromFile(path);
        using var stream = File.OpenRead(path);
        using var peReader = new PEReader(stream);
        var metadataReader = peReader.GetMetadataReader();
        Assert.True(metadataReader.MemberReferences.Count > 0);
        Assert.True(metadataReader.GetTableRowCount(TableIndex.MethodSpec) > 0);

        var contextToken = module.FindMethodDefinition(
            nameof(MetadataIdentityTests),
            nameof(UniqueFixtureMethod)).Value;
        var context = module.GetMethodHandle(contextToken).Value;
        var exactTargetToken = module.FindMethodDefinition(
            nameof(MetadataIdentityTests),
            nameof(ExactDirectCallTargetFixture)).Value;

        AssertFailure(
            module.ResolveMethod(context, 0x06000000),
            ResolutionFailureKind.Invalid,
            "META_INVALID_CALL_METHOD_TOKEN");
        AssertFailure(
            module.ResolveMethod(context, 0x0A000001),
            ResolutionFailureKind.Unsupported,
            "META_CALL_TOKEN_KIND_UNSUPPORTED");
        AssertFailure(
            module.ResolveMethod(context, 0x2B000001),
            ResolutionFailureKind.Unsupported,
            "META_CALL_TOKEN_KIND_UNSUPPORTED");
        AssertFailure(
            module.ResolveMethod(context, 0x0A000000),
            ResolutionFailureKind.Invalid,
            "META_INVALID_CALL_METHOD_TOKEN");
        AssertFailure(
            module.ResolveMethod(context, 0x0AFFFFFF),
            ResolutionFailureKind.Invalid,
            "META_INVALID_CALL_METHOD_TOKEN");
        AssertFailure(
            module.ResolveMethod(context, 0x2B000000),
            ResolutionFailureKind.Invalid,
            "META_INVALID_CALL_METHOD_TOKEN");
        AssertFailure(
            module.ResolveMethod(context, 0x2BFFFFFF),
            ResolutionFailureKind.Invalid,
            "META_INVALID_CALL_METHOD_TOKEN");
        AssertFailure(
            module.ResolveMethod(context, 0x06FFFFFF),
            ResolutionFailureKind.Invalid,
            "META_INVALID_CALL_METHOD_TOKEN");
        AssertFailure(
            module.ResolveMethod(context, 0x04000001),
            ResolutionFailureKind.Invalid,
            "META_INVALID_CALL_METHOD_TOKEN");
        AssertFailure(
            module.ResolveMethod(new MethodHandle(module.ModuleHandle, 0x06FFFFFF), exactTargetToken),
            ResolutionFailureKind.Invalid,
            "META_INVALID_CONTEXT_METHOD_TOKEN");
        AssertFailure(
            module.ResolveMethod(new MethodHandle(new ModuleHandle(3, 4), contextToken), exactTargetToken),
            ResolutionFailureKind.Conflict,
            "META_METHOD_CONTEXT_MODULE_CONFLICT");

        var instanceTarget = module.FindMethodDefinition(
            nameof(ProjectionFixture),
            nameof(ProjectionFixture.InstanceMethodWithLocal)).Value;
        AssertFailure(
            module.ResolveMethod(context, instanceTarget),
            ResolutionFailureKind.Unsupported,
            "META_CALL_TARGET_INSTANCE_UNSUPPORTED");

        AssertFailure(
            module.ResolveMethod(context, contextToken),
            ResolutionFailureKind.Unsupported,
            "META_CALL_TARGET_SIGNATURE_UNSUPPORTED");
        var voidTarget = module.FindMethodDefinition(
            nameof(MetadataIdentityTests),
            nameof(StaticVoidProjectionFixture)).Value;
        AssertFailure(
            module.ResolveMethod(context, voidTarget),
            ResolutionFailureKind.Unsupported,
            "META_CALL_TARGET_SIGNATURE_UNSUPPORTED");
        var optionalTarget = module.FindMethodDefinition(
            nameof(MetadataIdentityTests),
            nameof(OptionalDirectCallTargetFixture)).Value;
        AssertFailure(
            module.ResolveMethod(context, optionalTarget),
            ResolutionFailureKind.Unsupported,
            "META_OPTIONAL_PARAMETERS_UNSUPPORTED");

        var genericTarget = module.FindMethodDefinition(
            nameof(MetadataIdentityTests),
            nameof(GenericMethodProjectionFixture)).Value;
        AssertFailure(
            module.ResolveMethod(context, genericTarget),
            ResolutionFailureKind.Unsupported,
            "META_GENERIC_METHOD_UNSUPPORTED");
        var varArgTarget = module.FindMethodDefinition(
            nameof(MetadataIdentityTests),
            nameof(VarArgProjectionFixture)).Value;
        AssertFailure(
            module.ResolveMethod(context, varArgTarget),
            ResolutionFailureKind.Unsupported,
            "META_VARARGS_METHOD_UNSUPPORTED");

        var genericMethodContext = module.GetMethodHandle(genericTarget).Value;
        AssertFailure(
            module.ResolveMethod(genericMethodContext, exactTargetToken),
            ResolutionFailureKind.Unsupported,
            "META_GENERIC_METHOD_CONTEXT_UNSUPPORTED");
        var genericOwnerContextToken = module.FindMethodDefinition(
            "GenericProjectionFixture`1",
            nameof(GenericProjectionFixture<int>.Identity)).Value;
        AssertFailure(
            module.ResolveMethod(module.GetMethodHandle(genericOwnerContextToken).Value, exactTargetToken),
            ResolutionFailureKind.Unsupported,
            "META_GENERIC_METHOD_CONTEXT_UNSUPPORTED");
    }

    /// <summary>
    /// Proves that direct-call resolution needs no RVA while both body-free and supplied-body projection reject
    /// every excluded CLR implementation-flag family rather than admitting it as executable managed IL.
    /// </summary>
    [Fact]
    public void MethodProjectionsRequireManagedIlFlagsAndDirectResolutionNeverRequiresBodyOrRva()
    {
        var bodyFree = ProjectSyntheticCallTarget(
            MethodAttributes.Public | MethodAttributes.Static,
            MethodImplAttributes.IL | MethodImplAttributes.Managed);
        Assert.Equal(0, bodyFree.RelativeVirtualAddress);
        Assert.True(bodyFree.Result.IsSuccess, bodyFree.Result.Failure?.Code);
        Assert.True(bodyFree.Result.Value.IsManagedIl);
        Assert.Equal(TypeSig.Int32, bodyFree.Result.Value.Signature.ReturnType);
        Assert.Equal(2, bodyFree.Result.Value.Signature.ParameterTypes.Length);
        Assert.True(bodyFree.DefinitionResult.IsSuccess, bodyFree.DefinitionResult.Failure?.Code);

        var excludedFlags = new[]
        {
            (
                MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.PinvokeImpl,
                MethodImplAttributes.IL | MethodImplAttributes.Managed),
            (
                MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.Abstract,
                MethodImplAttributes.IL | MethodImplAttributes.Managed),
            (
                MethodAttributes.Public | MethodAttributes.Static,
                MethodImplAttributes.IL | MethodImplAttributes.Unmanaged),
            (
                MethodAttributes.Public | MethodAttributes.Static,
                MethodImplAttributes.Native | MethodImplAttributes.Managed),
            (
                MethodAttributes.Public | MethodAttributes.Static,
                MethodImplAttributes.OPTIL | MethodImplAttributes.Managed),
            (
                MethodAttributes.Public | MethodAttributes.Static,
                MethodImplAttributes.Runtime | MethodImplAttributes.Managed),
            (
                MethodAttributes.Public | MethodAttributes.Static,
                MethodImplAttributes.IL | MethodImplAttributes.Managed | MethodImplAttributes.InternalCall),
            (
                MethodAttributes.Public | MethodAttributes.Static,
                MethodImplAttributes.IL | MethodImplAttributes.Managed | MethodImplAttributes.ForwardRef),
            (
                MethodAttributes.Public | MethodAttributes.Static,
                MethodImplAttributes.IL | MethodImplAttributes.Managed | MethodImplAttributes.Synchronized),
        };
        foreach (var (attributes, implementationAttributes) in excludedFlags)
        {
            var rejected = ProjectSyntheticCallTarget(attributes, implementationAttributes);
            Assert.Equal(0, rejected.RelativeVirtualAddress);
            AssertFailure(
                rejected.Result,
                ResolutionFailureKind.Unsupported,
                "META_CALL_TARGET_IMPLEMENTATION_UNSUPPORTED");
            AssertFailure(
                rejected.DefinitionResult,
                ResolutionFailureKind.Unsupported,
                "META_METHOD_IMPLEMENTATION_UNSUPPORTED");
        }
    }

    /// <summary>
    /// Checks atomic SRM projection of receiver, explicit parameters, return disposition, and initialized locals.
    /// </summary>
    [Fact]
    public void MethodDefinitionProjectionDerivesStaticInstanceReturnParameterAndLocalShapes()
    {
        using var module = SrmMetadataModule.LoadFromFile(Assembly.GetExecutingAssembly().Location);

        var staticDefinition = GetDefinition(
            module,
            nameof(MetadataIdentityTests),
            nameof(StaticVoidProjectionFixture));
        Assert.False(staticDefinition.Signature.HasImplicitThis);
        Assert.False(staticDefinition.Signature.HasExplicitThis);
        Assert.Equal(MethodCallingConventionKind.Default, staticDefinition.Signature.CallingConvention);
        Assert.Equal(0, staticDefinition.Signature.GenericParameterCount);
        Assert.Equal(new[] { TypeSig.Int32 }, staticDefinition.Signature.ParameterTypes);
        Assert.Equal(TypeSig.Void, staticDefinition.Signature.ReturnType);
        Assert.Empty(staticDefinition.Signature.LocalTypes);

        var instanceDefinition = GetDefinition(
            module,
            nameof(ProjectionFixture),
            nameof(ProjectionFixture.InstanceMethodWithLocal));
        Assert.True(instanceDefinition.Signature.HasImplicitThis);
        Assert.Equal(new[] { TypeSig.Int32 }, instanceDefinition.Signature.ParameterTypes);
        Assert.Equal(TypeSig.Int32, instanceDefinition.Signature.ReturnType);
        Assert.Equal(new[] { TypeSig.Int32 }, instanceDefinition.Signature.LocalTypes);
        Assert.True(instanceDefinition.Body.LocalVariablesInitialized);
        Assert.NotEqual(0, instanceDefinition.Body.LocalSignatureToken);

        var receiverType = instanceDefinition.Signature.DeclaringType;
        Assert.True(receiverType.IsMetadataTypeDefinition);
        Assert.Equal(module.ModuleHandle, receiverType.Module);
        Assert.Equal(0x02000000, receiverType.MetadataToken & unchecked((int)0xFF000000));
    }

    /// <summary>
    /// Checks that module conflicts and unsupported generic, vararg, and non-W3 type shapes remain typed failures.
    /// </summary>
    [Fact]
    public void MethodDefinitionProjectionRejectsConflictingAndUnsupportedShapes()
    {
        using var module = SrmMetadataModule.LoadFromFile(Assembly.GetExecutingAssembly().Location);
        var uniqueToken = module.FindMethodDefinition(
            nameof(MetadataIdentityTests),
            nameof(UniqueFixtureMethod)).Value;
        var conflictingMethod = new MethodHandle(new ModuleHandle(1, 2), uniqueToken);

        AssertFailure(
            module.GetMethodDefinition(conflictingMethod),
            ResolutionFailureKind.Conflict,
            "META_METHOD_MODULE_CONFLICT");
        AssertMethodFailure(
            module,
            nameof(MetadataIdentityTests),
            nameof(GenericMethodProjectionFixture),
            "META_GENERIC_METHOD_UNSUPPORTED");
        AssertMethodFailure(
            module,
            nameof(MetadataIdentityTests),
            nameof(VarArgProjectionFixture),
            "META_VARARGS_METHOD_UNSUPPORTED");
        AssertMethodFailure(
            module,
            nameof(MetadataIdentityTests),
            nameof(UnsupportedReturnProjectionFixture),
            "META_RETURN_TYPE_UNSUPPORTED");
        AssertMethodFailure(
            module,
            nameof(MetadataIdentityTests),
            nameof(UnsupportedParameterProjectionFixture),
            "META_PARAMETER_TYPE_UNSUPPORTED");
        AssertMethodFailure(
            module,
            nameof(MetadataIdentityTests),
            nameof(UnsupportedLocalProjectionFixture),
            "META_LOCAL_TYPE_UNSUPPORTED");
        AssertMethodFailure(
            module,
            "GenericProjectionFixture`1",
            nameof(GenericProjectionFixture<int>.Identity),
            "META_GENERIC_DECLARING_TYPE_UNSUPPORTED");
        AssertMethodFailure(
            module,
            nameof(ValueTypeProjectionFixture),
            nameof(ValueTypeProjectionFixture.Identity),
            "META_RECEIVER_TYPE_UNSUPPORTED");
        AssertMethodFailure(
            module,
            nameof(ValueTypeProjectionFixture),
            nameof(ValueTypeProjectionFixture.StaticIdentity),
            "META_RECEIVER_TYPE_UNSUPPORTED");
    }

    /// <summary>
    /// Checks bounded ancestry classification admits a local reference-base chain but rejects enum, value-type,
    /// and unvalidated lookalike <c>System.Object</c> roots without loading an external assembly.
    /// </summary>
    [Fact]
    public void ReceiverProjectionRequiresPositivelyEstablishedReferenceTypeAncestry()
    {
        using var module = SrmMetadataModule.LoadFromFile(Assembly.GetExecutingAssembly().Location);
        var derivedReferenceDefinition = GetDefinition(
            module,
            nameof(DerivedReferenceProjectionFixture),
            nameof(DerivedReferenceProjectionFixture.Identity));
        Assert.True(derivedReferenceDefinition.Signature.HasImplicitThis);
        Assert.Equal(TypeSig.Int32, derivedReferenceDefinition.Signature.ReturnType);

        AssertFailure(
            ProjectSyntheticReceiver("System", "ValueType", "System.Runtime"),
            ResolutionFailureKind.Unsupported,
            "META_RECEIVER_TYPE_UNSUPPORTED");
        AssertFailure(
            ProjectSyntheticReceiver("System", "Enum", "System.Runtime"),
            ResolutionFailureKind.Unsupported,
            "META_RECEIVER_TYPE_UNSUPPORTED");
        AssertFailure(
            ProjectSyntheticReceiver("System", "Object", "Unvalidated.CoreLookalike"),
            ResolutionFailureKind.Unsupported,
            "META_RECEIVER_ANCESTRY_UNRESOLVED");

        var knownObjectRoot = ProjectSyntheticReceiver("System", "Object", "System.Runtime");
        Assert.True(knownObjectRoot.IsSuccess, knownObjectRoot.Failure?.Code);
        Assert.True(knownObjectRoot.Value.Signature.DeclaringType.IsMetadataTypeDefinition);
    }

    /// <summary>Checks invalid body-local tokens are rejected by the reusable metadata-only projection seam.</summary>
    [Fact]
    public void MethodDefinitionProjectionRejectsInvalidLocalSignatureToken()
    {
        var path = Assembly.GetExecutingAssembly().Location;
        using var module = SrmMetadataModule.LoadFromFile(path);
        var methodToken = module.FindMethodDefinition(
            nameof(ProjectionFixture),
            nameof(ProjectionFixture.InstanceMethodWithLocal)).Value;
        var method = module.GetMethodHandle(methodToken).Value;
        var body = module.GetMethodBody(method).Value;
        var invalidBody = IlMethodBody.Create(
            body.MaxStack,
            body.CodeBytes.ToArray(),
            body.LocalVariablesInitialized,
            localSignatureToken: 0x1100FFFF,
            exceptionRegionCount: body.ExceptionRegionCount);

        using var stream = File.OpenRead(path);
        using var peReader = new PEReader(stream);
        var result = SrmMetadataProjection.ProjectMethodDefinition(
            peReader.GetMetadataReader(),
            module.ModuleHandle,
            method,
            invalidBody);

        AssertFailure(result, ResolutionFailureKind.Invalid, "META_LOCAL_SIGNATURE_TOKEN_INVALID");
    }

    /// <summary>
    /// Checks exact FieldDef projection and stable rejection of every readily compiler-emitted excluded field shape.
    /// </summary>
    [Fact]
    public void FieldProjectionRequiresDirectInstanceInt32FieldDefinition()
    {
        var path = Assembly.GetExecutingAssembly().Location;
        using var module = SrmMetadataModule.LoadFromFile(path);
        var getterToken = module.FindMethodDefinition(
            nameof(ProjectionFixture),
            nameof(ProjectionFixture.GetInstanceInt32)).Value;
        var getter = module.GetMethodHandle(getterToken).Value;
        var genericGetterToken = module.FindMethodDefinition(
            nameof(ProjectionFixture),
            nameof(ProjectionFixture.GenericGetter)).Value;
        var genericGetter = module.GetMethodHandle(genericGetterToken).Value;

        var instanceToken = FindFieldDefinitionToken(path, nameof(ProjectionFixture), "InstanceInt32");
        var staticToken = FindFieldDefinitionToken(path, nameof(ProjectionFixture), "StaticInt32");
        var literalToken = FindFieldDefinitionToken(path, nameof(ProjectionFixture), "LiteralInt32");
        var int64Token = FindFieldDefinitionToken(path, nameof(ProjectionFixture), "InstanceInt64");
        var otherOwnerToken = FindFieldDefinitionToken(path, nameof(OtherProjectionFixture), "OtherValue");

        var resolved = module.ResolveField(getter, instanceToken);
        Assert.True(resolved.IsSuccess);
        Assert.Equal(new FieldHandle(module.ModuleHandle, instanceToken), resolved.Value.Handle);
        Assert.Equal(TypeSig.Int32, resolved.Value.FieldType);
        Assert.Equal(
            module.GetMethodDefinition(getter).Value.Signature.DeclaringType,
            resolved.Value.DeclaringType);
        Assert.False(resolved.Value.IsStatic);
        Assert.False(resolved.Value.IsLiteral);
        Assert.False(resolved.Value.HasRva);

        AssertFailure(
            module.ResolveField(getter, staticToken),
            ResolutionFailureKind.Unsupported,
            "META_STATIC_FIELD_UNSUPPORTED");
        AssertFailure(
            module.ResolveField(getter, literalToken),
            ResolutionFailureKind.Unsupported,
            "META_LITERAL_FIELD_UNSUPPORTED");
        AssertFailure(
            module.ResolveField(getter, int64Token),
            ResolutionFailureKind.Unsupported,
            "META_FIELD_TYPE_UNSUPPORTED");
        AssertFailure(
            module.ResolveField(getter, otherOwnerToken),
            ResolutionFailureKind.Unsupported,
            "META_FIELD_OWNER_UNSUPPORTED");
        AssertFailure(
            module.ResolveField(genericGetter, instanceToken),
            ResolutionFailureKind.Unsupported,
            "META_GENERIC_FIELD_CONTEXT_UNSUPPORTED");
        AssertFailure(
            module.ResolveField(getter, unchecked((int)0x0A000001)),
            ResolutionFailureKind.Invalid,
            "META_INVALID_FIELD_TOKEN");
        AssertFailure(
            module.ResolveField(new MethodHandle(new ModuleHandle(3, 4), getterToken), instanceToken),
            ResolutionFailureKind.Conflict,
            "META_FIELD_CONTEXT_MODULE_CONFLICT");
    }

    /// <summary>Checks that metadata operations cannot outlive their artifact stream and disposal is idempotent.</summary>
    [Fact]
    public void MetadataOperationsRejectUseAfterDispose()
    {
        var module = SrmMetadataModule.LoadFromFile(Assembly.GetExecutingAssembly().Location);
        var token = module.FindMethodDefinition(nameof(MetadataIdentityTests), nameof(UniqueFixtureMethod)).Value;
        var method = new MethodHandle(module.ModuleHandle, token);
        var projected = module.GetMethodDefinition(method);
        Assert.True(projected.IsSuccess);

        module.Dispose();
        module.Dispose();

        Assert.Equal(TypeSig.Int32, projected.Value.Signature.ReturnType);

        Assert.Throws<ObjectDisposedException>(() => module.FindMethodDefinition(
            nameof(MetadataIdentityTests),
            nameof(UniqueFixtureMethod)));
        Assert.Throws<ObjectDisposedException>(() => module.GetMethodHandle(token));
        Assert.Throws<ObjectDisposedException>(() => module.GetMethodDefinition(method));
        Assert.Throws<ObjectDisposedException>(() => module.ResolveMethod(method, token));
        Assert.Throws<ObjectDisposedException>(() => module.GetMethodBody(method));
        Assert.Throws<ObjectDisposedException>(() => module.ResolveField(method, 0x04000001));
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

    private static int ExactDirectCallTargetFixture(int left, int right) => unchecked(left + right);

    private static int OptionalDirectCallTargetFixture(int left, int right = 0) => unchecked(left + right);

    private static int OverloadedFixture(int value) => value;

    private static string OverloadedFixture(string value) => value;

    private static void StaticVoidProjectionFixture(int value) => GC.KeepAlive(value);

    private static T GenericMethodProjectionFixture<T>(T value) => value;

    private static int VarArgProjectionFixture(__arglist) => 0;

    private static string UnsupportedReturnProjectionFixture() => string.Empty;

    private static int UnsupportedParameterProjectionFixture(long value) => unchecked((int)value);

    private static int UnsupportedLocalProjectionFixture(int value)
    {
        AssignLongOut(out var local);
        return value + unchecked((int)local);
    }

    private static void AssignLongOut(out long value) => value = 41;

    private static ResolvedMethodDefinition GetDefinition(
        SrmMetadataModule module,
        string typeName,
        string methodName)
    {
        var token = module.FindMethodDefinition(typeName, methodName);
        Assert.True(token.IsSuccess);
        var handle = module.GetMethodHandle(token.Value);
        Assert.True(handle.IsSuccess);
        var definition = module.GetMethodDefinition(handle.Value);
        Assert.True(definition.IsSuccess, definition.Failure?.Code);
        return definition.Value;
    }

    private static void AssertMethodFailure(
        SrmMetadataModule module,
        string typeName,
        string methodName,
        string expectedCode)
    {
        var token = module.FindMethodDefinition(typeName, methodName);
        Assert.True(token.IsSuccess);
        var method = module.GetMethodHandle(token.Value);
        Assert.True(method.IsSuccess);
        AssertFailure(module.GetMethodDefinition(method.Value), ResolutionFailureKind.Unsupported, expectedCode);
    }

    private static void AssertFailure<T>(
        ResolutionResult<T> result,
        ResolutionFailureKind expectedKind,
        string expectedCode)
    {
        Assert.False(result.IsSuccess);
        Assert.Equal(expectedKind, result.Failure!.Kind);
        Assert.Equal(expectedCode, result.Failure.Code);
    }

    private static int FindFieldDefinitionToken(string path, string typeName, string fieldName)
    {
        using var stream = File.OpenRead(path);
        using var peReader = new PEReader(stream);
        var metadata = peReader.GetMetadataReader();
        return metadata.TypeDefinitions
            .Where(handle => string.Equals(
                metadata.GetString(metadata.GetTypeDefinition(handle).Name),
                typeName,
                StringComparison.Ordinal))
            .SelectMany(handle => metadata.GetTypeDefinition(handle).GetFields())
            .Where(handle => string.Equals(
                metadata.GetString(metadata.GetFieldDefinition(handle).Name),
                fieldName,
                StringComparison.Ordinal))
            .Select(handle => MetadataTokens.GetToken(handle))
            .Single();
    }

    private static ResolutionResult<ResolvedMethodDefinition> ProjectSyntheticReceiver(
        string baseTypeNamespace,
        string baseTypeName,
        string baseAssemblyName)
    {
        var metadata = new MetadataBuilder();
        _ = metadata.AddModule(
            generation: 0,
            metadata.GetOrAddString("SyntheticReceiver.dll"),
            metadata.GetOrAddGuid(new Guid("90000000-0000-0000-0000-000000000001")),
            encId: default,
            encBaseId: default);
        var baseAssembly = metadata.AddAssemblyReference(
            metadata.GetOrAddString(baseAssemblyName),
            new Version(10, 0, 0, 0),
            culture: default,
            publicKeyOrToken: default,
            (AssemblyFlags)0,
            hashValue: default);
        var baseType = metadata.AddTypeReference(
            baseAssembly,
            metadata.GetOrAddString(baseTypeNamespace),
            metadata.GetOrAddString(baseTypeName));

        var firstField = MetadataTokens.FieldDefinitionHandle(1);
        var firstMethod = MetadataTokens.MethodDefinitionHandle(1);
        _ = metadata.AddTypeDefinition(
            TypeAttributes.NotPublic,
            metadata.GetOrAddString(string.Empty),
            metadata.GetOrAddString("<Module>"),
            baseType: default,
            firstField,
            firstMethod);
        _ = metadata.AddTypeDefinition(
            TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.BeforeFieldInit,
            metadata.GetOrAddString("Synthetic"),
            metadata.GetOrAddString("Receiver"),
            baseType,
            firstField,
            firstMethod);

        var signatureBuilder = new BlobBuilder();
        new BlobEncoder(signatureBuilder)
            .MethodSignature(
                SignatureCallingConvention.Default,
                genericParameterCount: 0,
                isInstanceMethod: true)
            .Parameters(
                parameterCount: 1,
                returnType => returnType.Type(isByRef: false).Int32(),
                parameters => parameters.AddParameter().Type(isByRef: false).Int32());
        var methodDefinition = metadata.AddMethodDefinition(
            MethodAttributes.Public | MethodAttributes.HideBySig,
            MethodImplAttributes.IL | MethodImplAttributes.Managed,
            metadata.GetOrAddString("Identity"),
            metadata.GetOrAddBlob(signatureBuilder),
            bodyOffset: 0,
            MetadataTokens.ParameterHandle(1));
        _ = metadata.AddParameter(
            ParameterAttributes.None,
            metadata.GetOrAddString("value"),
            sequenceNumber: 1);

        var metadataImage = new BlobBuilder();
        new MetadataRootBuilder(metadata).Serialize(
            metadataImage,
            methodBodyStreamRva: 0,
            0);
        using var provider = MetadataReaderProvider.FromMetadataImage(metadataImage.ToImmutableArray());
        var module = new ModuleHandle(0x9000000000000001, 0x9000000000000002);
        var method = new MethodHandle(module, MetadataTokens.GetToken(methodDefinition));
        return SrmMetadataProjection.ProjectMethodDefinition(
            provider.GetMetadataReader(),
            module,
            method,
            IlMethodBody.Create(maxStack: 1, [0x16, 0x2A]));
    }

    private static SyntheticCallProjection ProjectSyntheticCallTarget(
        MethodAttributes targetAttributes,
        MethodImplAttributes targetImplementationAttributes)
    {
        var metadata = new MetadataBuilder();
        _ = metadata.AddModule(
            generation: 0,
            metadata.GetOrAddString("SyntheticCallTarget.dll"),
            metadata.GetOrAddGuid(new Guid("94000000-0000-0000-0000-000000000001")),
            encId: default,
            encBaseId: default);
        var baseAssembly = metadata.AddAssemblyReference(
            metadata.GetOrAddString("System.Runtime"),
            new Version(10, 0, 0, 0),
            culture: default,
            publicKeyOrToken: default,
            (AssemblyFlags)0,
            hashValue: default);
        var objectType = metadata.AddTypeReference(
            baseAssembly,
            metadata.GetOrAddString("System"),
            metadata.GetOrAddString("Object"));
        var firstField = MetadataTokens.FieldDefinitionHandle(1);
        var firstMethod = MetadataTokens.MethodDefinitionHandle(1);
        _ = metadata.AddTypeDefinition(
            TypeAttributes.NotPublic,
            metadata.GetOrAddString(string.Empty),
            metadata.GetOrAddString("<Module>"),
            baseType: default,
            firstField,
            firstMethod);
        _ = metadata.AddTypeDefinition(
            TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.BeforeFieldInit,
            metadata.GetOrAddString("Synthetic"),
            metadata.GetOrAddString("CallOwner"),
            objectType,
            firstField,
            firstMethod);

        var contextSignatureBuilder = new BlobBuilder();
        new BlobEncoder(contextSignatureBuilder)
            .MethodSignature(
                SignatureCallingConvention.Default,
                genericParameterCount: 0,
                isInstanceMethod: false)
            .Parameters(
                parameterCount: 0,
                returnType => returnType.Void(),
                _ => { });
        var contextDefinition = metadata.AddMethodDefinition(
            MethodAttributes.Public | MethodAttributes.Static,
            MethodImplAttributes.IL | MethodImplAttributes.Managed,
            metadata.GetOrAddString("Context"),
            metadata.GetOrAddBlob(contextSignatureBuilder),
            bodyOffset: 0,
            MetadataTokens.ParameterHandle(1));

        var targetSignatureBuilder = new BlobBuilder();
        new BlobEncoder(targetSignatureBuilder)
            .MethodSignature(
                SignatureCallingConvention.Default,
                genericParameterCount: 0,
                isInstanceMethod: false)
            .Parameters(
                parameterCount: 2,
                returnType => returnType.Type(isByRef: false).Int32(),
                parameters =>
                {
                    parameters.AddParameter().Type(isByRef: false).Int32();
                    parameters.AddParameter().Type(isByRef: false).Int32();
                });
        var targetDefinition = metadata.AddMethodDefinition(
            targetAttributes,
            targetImplementationAttributes,
            metadata.GetOrAddString("Target"),
            metadata.GetOrAddBlob(targetSignatureBuilder),
            bodyOffset: 0,
            MetadataTokens.ParameterHandle(1));
        _ = metadata.AddParameter(
            ParameterAttributes.None,
            metadata.GetOrAddString("left"),
            sequenceNumber: 1);
        _ = metadata.AddParameter(
            ParameterAttributes.None,
            metadata.GetOrAddString("right"),
            sequenceNumber: 2);

        var metadataImage = new BlobBuilder();
        new MetadataRootBuilder(metadata).Serialize(metadataImage, methodBodyStreamRva: 0, mappedFieldDataStreamRva: 0);
        using var provider = MetadataReaderProvider.FromMetadataImage(metadataImage.ToImmutableArray());
        var reader = provider.GetMetadataReader();
        var module = new ModuleHandle(0x9400000000000001, 0x9400000000000002);
        var context = new MethodHandle(module, MetadataTokens.GetToken(contextDefinition));
        var targetToken = MetadataTokens.GetToken(targetDefinition);
        return new SyntheticCallProjection(
            SrmMetadataProjection.ProjectMethodCallTarget(reader, module, context, targetToken),
            SrmMetadataProjection.ProjectMethodDefinition(
                reader,
                module,
                new MethodHandle(module, targetToken),
                IlMethodBody.Create(maxStack: 1, [0x16, 0x2A])),
            reader.GetMethodDefinition(targetDefinition).RelativeVirtualAddress);
    }

    private sealed class ProjectionFixture
    {
        private const int LiteralInt32 = 41;
        private static int StaticInt32 = 17;
        private int InstanceInt32 = 19;
        private long InstanceInt64 = 23;

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal int InstanceMethodWithLocal(int value)
        {
            AssignOut(out var local);
            return local + value + InstanceInt32;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal int GetInstanceInt32() => InstanceInt32;

        internal int GenericGetter<T>() => InstanceInt32;

        internal long GetInstanceInt64() => InstanceInt64;

        internal static int GetStaticInt32() => StaticInt32;

        internal static int GetLiteralInt32() => LiteralInt32;

        private static void AssignOut(out int value) => value = 41;
    }

    private sealed class OtherProjectionFixture
    {
        private int OtherValue = 29;

        internal int GetOtherValue() => OtherValue;
    }

    private sealed class GenericProjectionFixture<T>
    {
        internal T Identity(T value) => value;
    }

    private readonly struct ValueTypeProjectionFixture
    {
        internal int Identity(int value) => value;

        internal static int StaticIdentity(int value) => value;
    }

    private class ReferenceBaseProjectionFixture
    {
    }

    private sealed class DerivedReferenceProjectionFixture : ReferenceBaseProjectionFixture
    {
        internal int Identity(int value) => value;
    }

    private sealed record SyntheticCallProjection(
        ResolutionResult<ResolvedMethodCallTarget> Result,
        ResolutionResult<ResolvedMethodDefinition> DefinitionResult,
        int RelativeVirtualAddress);

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
