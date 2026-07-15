using System.Collections.Immutable;
using Interpreter.Core.Abstractions;
using Interpreter.Core.Execution;
using Xunit;
using IlBody = Interpreter.Core.Abstractions.MethodBody;
using ModuleHandle = Interpreter.Core.Abstractions.ModuleHandle;

namespace Interpreter.Tests;

/// <summary>Exercises complete, deterministic W4 direct-call graph preparation without activating an IL machine.</summary>
public sealed class MethodGraphPlannerTests
{
    internal static readonly ModuleHandle Module = new(
        0xA401020304050607,
        0xB408091011121314);
    internal static readonly TypeSig RootType = TypeSig.CreateTypeDefinition(
        Module,
        0x02000001,
        "Interpreter.Tests.W4Root");
    internal static readonly TypeSig HelperType = TypeSig.CreateTypeDefinition(
        Module,
        0x02000002,
        "Interpreter.Tests.W4Helper");

    /// <summary>Proves the exact W4 caller/helper admission boundary and its frozen dependency facts.</summary>
    [Fact]
    public void ExactBoundaryFreezesRootHelperFieldsCallsAndTypedEntries()
    {
        var root = Method(1);
        var helper = Method(2);
        var firstField = Field(1, RootType);
        var secondField = Field(2, RootType);
        var rootDefinition = RootDefinition(
            root,
            ExactRootBody(firstField.Handle.MetadataToken, secondField.Handle.MetadataToken, helper.MetadataToken),
            maxStack: 2);
        var helperDefinition = HelperDefinition(helper, LeafBody(), maxStack: 2);
        var resolver = Resolver(rootDefinition, helperDefinition);
        resolver.Fields[(root, firstField.Handle.MetadataToken)] = firstField;
        resolver.Fields[(root, secondField.Handle.MetadataToken)] = secondField;
        resolver.Calls[(root, helper.MetadataToken)] = Target(helper);

        var result = new MethodGraphPlanner(resolver).Prepare(root);

        Assert.True(result.IsSuccess, result.Failure?.Code);
        var plan = Assert.IsType<FrozenMethodGraphPlan>(result.Plan);
        Assert.Equal(root, plan.Root);
        Assert.Equal(2, plan.Nodes.Length);
        Assert.True(plan.Fields.SequenceEqual([firstField, secondField]));
        var call = Assert.Single(plan.CallSites);
        Assert.Equal((root, 12, helper.MetadataToken, helper),
            (call.Caller, call.IlOffset, call.MetadataToken, call.Target.Method));
        Assert.Equal(2, plan.RequiredLogicalDepth);
        Assert.Equal(5, plan.TraversalUnitCount);
        Assert.True(plan.TryGetNode(root, out var rootNode));
        Assert.NotNull(rootNode);
        Assert.True(plan.TryGetNode(helper, out var helperNode));
        Assert.NotNull(helperNode);

        Assert.Collection(
            rootNode!.Admission.InstructionBoundaries,
            item => AssertBoundary(item, 0),
            item => AssertBoundary(item, 1, RootType),
            item => AssertBoundary(item, 6, TypeSig.Int32),
            item => AssertBoundary(item, 7, TypeSig.Int32, RootType),
            item => AssertBoundary(item, 12, TypeSig.Int32, TypeSig.Int32),
            item => AssertBoundary(item, 17, TypeSig.Int32));
        Assert.Collection(
            helperNode!.Admission.InstructionBoundaries,
            item => AssertBoundary(item, 0),
            item => AssertBoundary(item, 1, TypeSig.Int32),
            item => AssertBoundary(item, 2, TypeSig.Int32, TypeSig.Int32),
            item => AssertBoundary(item, 3, TypeSig.Int32));
        Assert.Equal(1, resolver.DefinitionCount(root));
        Assert.Equal(1, resolver.DefinitionCount(helper));
        Assert.Equal(1, resolver.FieldCount(root, firstField.Handle.MetadataToken));
        Assert.Equal(1, resolver.FieldCount(root, secondField.Handle.MetadataToken));
        Assert.Equal(1, resolver.CallCount(root, helper.MetadataToken));
        Assert.True(
            resolver.Operations.IndexOf($"call:{root.MetadataToken:X8}:{helper.MetadataToken:X8}") <
            resolver.Operations.IndexOf($"definition:{helper.MetadataToken:X8}"));
    }

    /// <summary>Proves depth-first discovery is separated from canonical public node ordering.</summary>
    [Fact]
    public void ChainUsesRootFirstDiscoveryButCanonicalNodesAndLongestDepth()
    {
        var root = Method(50);
        var first = Method(20);
        var second = Method(7);
        var resolver = Resolver(
            RootDefinition(root, RootCallsBody(first), maxStack: 2),
            HelperDefinition(first, CallsBody(second), maxStack: 2),
            HelperDefinition(second, LeafBody(), maxStack: 2));
        resolver.Calls[(root, first.MetadataToken)] = Target(first);
        resolver.Calls[(first, second.MetadataToken)] = Target(second);

        var result = new MethodGraphPlanner(resolver).Prepare(root);

        Assert.True(result.IsSuccess, result.Failure?.Code);
        var plan = result.Plan!;
        Assert.Equal([second, first, root], plan.Nodes.Select(node => node.Method));
        Assert.Equal(3, plan.RequiredLogicalDepth);
        Assert.Equal(5, plan.TraversalUnitCount);
        Assert.Equal([(first, 2), (root, 2)], plan.CallSites.Select(site => (site.Caller, site.IlOffset)));
    }

    /// <summary>Proves shared reachable methods are deduplicated while all incoming edges remain frozen.</summary>
    [Fact]
    public void DiamondRetainsEveryEdgeAndResolvesSharedCalleeDefinitionOnce()
    {
        var root = Method(1);
        var left = Method(2);
        var right = Method(3);
        var shared = Method(4);
        var resolver = Resolver(
            RootDefinition(root, RootCallsBody(left, right), maxStack: 3),
            HelperDefinition(left, CallsBody(shared), maxStack: 2),
            HelperDefinition(right, CallsBody(shared), maxStack: 2),
            HelperDefinition(shared, LeafBody(), maxStack: 2));
        resolver.Calls[(root, left.MetadataToken)] = Target(left);
        resolver.Calls[(root, right.MetadataToken)] = Target(right);
        resolver.Calls[(left, shared.MetadataToken)] = Target(shared);
        resolver.Calls[(right, shared.MetadataToken)] = Target(shared);

        var result = new MethodGraphPlanner(resolver).Prepare(root);

        Assert.True(result.IsSuccess, result.Failure?.Code);
        Assert.Equal(4, result.Plan!.Nodes.Length);
        Assert.Equal(4, result.Plan.CallSites.Length);
        Assert.Equal(3, result.Plan.RequiredLogicalDepth);
        Assert.Equal(8, result.Plan.TraversalUnitCount);
        Assert.Equal(1, resolver.DefinitionCount(shared));
        Assert.Equal(2, result.Plan.CallSites.Count(site => site.Target.Method == shared));
    }

    /// <summary>Proves self and mutual recursion reject deterministically without exposing a prefix graph.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SelfAndMutualRecursionRejectTheFirstBackEdgeWithoutPartialPlan(bool mutual)
    {
        var root = Method(1);
        var first = Method(2);
        var second = Method(3);
        var resolver = mutual
            ? Resolver(
                RootDefinition(root, RootCallsBody(first), maxStack: 2),
                HelperDefinition(first, CallsBody(second), maxStack: 2),
                HelperDefinition(second, CallsBody(first), maxStack: 2))
            : Resolver(
                RootDefinition(root, RootCallsBody(first), maxStack: 2),
                HelperDefinition(first, CallsBody(first), maxStack: 2));
        resolver.Calls[(root, first.MetadataToken)] = Target(first);
        resolver.Calls[(first, mutual ? second.MetadataToken : first.MetadataToken)] =
            Target(mutual ? second : first);
        if (mutual)
        {
            resolver.Calls[(second, first.MetadataToken)] = Target(first);
        }

        var result = new MethodGraphPlanner(resolver).Prepare(root);

        AssertFailure(result, MachineRunStatus.Blocked, "EXEC_CALL_CYCLE_UNSUPPORTED");
        Assert.Equal(mutual ? second : first, result.Failure!.Method);
        Assert.Equal(2, result.Failure.IlOffset);
    }

    /// <summary>Proves only a non-nil MethodDef token reaches body-independent method resolution.</summary>
    [Theory]
    [InlineData(0x06000000, MachineRunStatus.InvalidProgram, "EXEC_CALL_TOKEN_INVALID")]
    [InlineData(0x0A000001, MachineRunStatus.Blocked, "EXEC_CALL_TOKEN_UNSUPPORTED")]
    [InlineData(0x2B000001, MachineRunStatus.Blocked, "EXEC_CALL_TOKEN_UNSUPPORTED")]
    public void InvalidOrDispatchTokensRejectBeforeMethodResolution(
        int token,
        MachineRunStatus status,
        string code)
    {
        var root = Method(1);
        var resolver = Resolver(RootDefinition(root, RootCallsBody(token), maxStack: 2));

        var result = new MethodGraphPlanner(resolver).Prepare(root);

        AssertFailure(result, status, code);
        Assert.Empty(resolver.CallCounts);
        Assert.Single(resolver.DefinitionCounts);
    }

    /// <summary>Proves malformed InlineMethod operands and indirect dispatch fail whole-body admission.</summary>
    [Fact]
    public void TruncatedCallAndIndirectDispatchRemainWholeBodyFailures()
    {
        var root = Method(1);
        var truncatedResolver = Resolver(
            RootDefinition(root, [0x16, 0x16, 0x28, 0x01, 0x00], maxStack: 2));

        var truncated = new MethodGraphPlanner(truncatedResolver).Prepare(root);

        AssertFailure(truncated, MachineRunStatus.InvalidProgram, "EXEC_TRUNCATED_INSTRUCTION");
        Assert.Empty(truncatedResolver.CallCounts);

        var indirectResolver = Resolver(
            RootDefinition(root, TokenInstructionBody(0x6F, Method(2).MetadataToken), maxStack: 2));

        var indirect = new MethodGraphPlanner(indirectResolver).Prepare(root);

        AssertFailure(indirect, MachineRunStatus.Blocked, "EXEC_UNSUPPORTED_OPCODE");
        Assert.Empty(indirectResolver.CallCounts);
    }

    /// <summary>Proves a target signature is validated before any target body can be acquired.</summary>
    [Fact]
    public void UnsupportedCallSignatureRejectsBeforeCalleeDefinitionAcquisition()
    {
        var root = Method(1);
        var helper = Method(2);
        var rootDefinition = RootDefinition(root, RootCallsBody(helper), maxStack: 2);
        var resolver = Resolver(rootDefinition);
        resolver.Calls[(root, helper.MetadataToken)] = new ResolvedMethodCallTarget(
            helper,
            new MethodCallSignatureShape(
                HelperType,
                MethodCallingConventionKind.Default,
                hasImplicitThis: true,
                hasExplicitThis: false,
                genericParameterCount: 0,
                ImmutableArray<TypeSig>.Empty,
                TypeSig.Int32));

        var result = new MethodGraphPlanner(resolver).Prepare(root);

        AssertFailure(result, MachineRunStatus.Blocked, "EXEC_CALL_TARGET_SIGNATURE_UNSUPPORTED");
        Assert.Equal(0, resolver.DefinitionCount(helper));
        Assert.Equal(1, resolver.CallCount(root, helper.MetadataToken));
    }

    /// <summary>Proves a target identity conflict is terminal before target body acquisition.</summary>
    [Fact]
    public void CallIdentityConflictRejectsBeforeCalleeDefinitionAcquisition()
    {
        var root = Method(1);
        var operandTarget = Method(2);
        var conflictingTarget = Method(3);
        var resolver = Resolver(RootDefinition(root, RootCallsBody(operandTarget), maxStack: 2));
        resolver.Calls[(root, operandTarget.MetadataToken)] = Target(conflictingTarget);

        var result = new MethodGraphPlanner(resolver).Prepare(root);

        AssertConflictFailure(result, "EXEC_CALL_TARGET_IDENTITY_CONFLICT");
        Assert.Equal(0, resolver.DefinitionCount(operandTarget));
        Assert.Equal(0, resolver.DefinitionCount(conflictingTarget));
    }

    /// <summary>Proves call stack validation is atomic and does not trigger target body acquisition.</summary>
    [Fact]
    public void CallStackUnderflowOrWrongTypeRejectsBeforeCalleeDefinitionAcquisition()
    {
        var root = Method(1);
        var helper = Method(2);
        var underflowCode = new List<byte> { 0x16 };
        EmitToken(underflowCode, 0x28, helper.MetadataToken);
        underflowCode.Add(0x2A);
        var wrongTypeCode = new List<byte> { 0x02, 0x16 };
        EmitToken(wrongTypeCode, 0x28, helper.MetadataToken);
        wrongTypeCode.Add(0x2A);

        foreach (var body in new[] { underflowCode.ToArray(), wrongTypeCode.ToArray() })
        {
            var resolver = Resolver(RootDefinition(root, body, maxStack: 2));
            resolver.Calls[(root, helper.MetadataToken)] = Target(helper);

            var result = new MethodGraphPlanner(resolver).Prepare(root);

            AssertFailure(result, MachineRunStatus.InvalidProgram, "EXEC_INVALID_STACK");
            Assert.Equal(0, resolver.DefinitionCount(helper));
            Assert.Equal(1, resolver.CallCount(root, helper.MetadataToken));
        }
    }

    /// <summary>Proves every resolver failure class is mapped deterministically and never yields a partial plan.</summary>
    [Theory]
    [InlineData(0, MachineRunStatus.Blocked, "TEST_CALL_UNSUPPORTED")]
    [InlineData(1, MachineRunStatus.InvalidProgram, "TEST_CALL_INVALID")]
    [InlineData(2, MachineRunStatus.Blocked, "TEST_CALL_CONFLICT")]
    [InlineData(3, MachineRunStatus.InvalidProgram, "RESOLUTION_INVALID_RESULT")]
    [InlineData(4, MachineRunStatus.Blocked, "EXEC_RESOLVER_FAILURE")]
    public void MethodResolverFailuresAreStructuredAndAtomic(
        int behavior,
        MachineRunStatus status,
        string code)
    {
        var root = Method(1);
        var helper = Method(2);
        var resolver = Resolver(RootDefinition(root, RootCallsBody(helper), maxStack: 2));
        resolver.CallFactory = behavior switch
        {
            0 => (_, _) => ResolutionResult<ResolvedMethodCallTarget>.Failed(
                ResolutionFailureKind.Unsupported,
                "TEST_CALL_UNSUPPORTED",
                "Synthetic unsupported call."),
            1 => (_, _) => ResolutionResult<ResolvedMethodCallTarget>.Failed(
                ResolutionFailureKind.Invalid,
                "TEST_CALL_INVALID",
                "Synthetic invalid call."),
            2 => (_, _) => ResolutionResult<ResolvedMethodCallTarget>.Failed(
                ResolutionFailureKind.Conflict,
                "TEST_CALL_CONFLICT",
                "Synthetic conflicting call."),
            3 => (_, _) => default,
            _ => (_, _) => throw new InvalidOperationException("synthetic resolver failure"),
        };

        var result = new MethodGraphPlanner(resolver).Prepare(root);

        AssertFailure(result, status, code);
        Assert.Equal(0, resolver.DefinitionCount(helper));
    }

    /// <summary>Proves representative constructor, function-pointer, and call-prefix families never resolve a call.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void UnsupportedCallFamiliesRejectBeforeMethodResolution(int family)
    {
        var root = Method(1);
        var helper = Method(2);
        var code = family switch
        {
            0 => TokenInstructionBody(0x73, helper.MetadataToken), // newobj
            1 => PrefixedTokenBody(0x06, helper.MetadataToken), // ldftn
            2 => PrefixThenCallBody(0x14, helper.MetadataToken), // tail.
            _ => PrefixThenCallBody(0x16, helper.MetadataToken, prefixHasToken: true), // constrained.
        };
        var resolver = Resolver(RootDefinition(root, code, maxStack: 2));

        var result = new MethodGraphPlanner(resolver).Prepare(root);

        AssertFailure(result, MachineRunStatus.Blocked, "EXEC_UNSUPPORTED_OPCODE");
        Assert.Empty(resolver.CallCounts);
        Assert.Equal(1, resolver.DefinitionCount(root));
    }

    /// <summary>Proves the loaded definition must agree with the call edge's frozen identity and signature.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LoadedDefinitionIdentityOrSignatureConflictRejectsAtCaller(bool identityConflict)
    {
        var root = Method(1);
        var helper = Method(2);
        var targetSignature = HelperSignature(HelperType);
        ResolvedMethodDefinition loaded;
        if (identityConflict)
        {
            loaded = HelperDefinition(Method(3), LeafBody(), maxStack: 2);
        }
        else
        {
            var conflictingType = TypeSig.CreateTypeDefinition(Module, 0x02000003, "Conflicting.Helper");
            loaded = Definition(helper, HelperSignature(conflictingType), LeafBody(), maxStack: 2);
        }

        var resolver = Resolver(RootDefinition(root, RootCallsBody(helper), maxStack: 2));
        resolver.Definitions[helper] = loaded;
        resolver.Calls[(root, helper.MetadataToken)] = new ResolvedMethodCallTarget(helper, targetSignature);

        var result = new MethodGraphPlanner(resolver).Prepare(root);

        AssertConflictFailure(result, "EXEC_CALL_TARGET_DEFINITION_CONFLICT");
        Assert.Equal(root, result.Failure!.Method);
        Assert.Equal(2, result.Failure.IlOffset);
        Assert.Equal(1, resolver.DefinitionCount(helper));
    }

    /// <summary>Proves a missing reachable definition is attributed to the incoming call instruction.</summary>
    [Fact]
    public void CalleeDefinitionResolutionFailureIsAttributedToIncomingCallerOffset()
    {
        var root = Method(1);
        var helper = Method(2);
        var resolver = Resolver(RootDefinition(root, RootCallsBody(helper), maxStack: 2));
        resolver.Calls[(root, helper.MetadataToken)] = Target(helper);

        var result = new MethodGraphPlanner(resolver).Prepare(root);

        AssertFailure(result, MachineRunStatus.Blocked, "TEST_METHOD_UNAVAILABLE");
        Assert.Equal(root, result.Failure!.Method);
        Assert.Equal(2, result.Failure.IlOffset);
    }

    /// <summary>Proves failures in any reachable callee preserve their original whole-body diagnostic.</summary>
    [Theory]
    [InlineData(false, "EXEC_UNSUPPORTED_OPCODE", 3)]
    [InlineData(true, "EXEC_EH_UNSUPPORTED", 0)]
    public void ReachableCalleeSuffixOrEhFailurePreservesBodyDiagnostic(
        bool hasExceptionRegion,
        string code,
        int offset)
    {
        var root = Method(1);
        var helper = Method(2);
        var helperBody = hasExceptionRegion
            ? LeafBody()
            : new byte[] { 0x02, 0x03, 0x58, 0x6F, 0x01, 0x00, 0x00, 0x06 };
        var resolver = Resolver(
            RootDefinition(root, RootCallsBody(helper), maxStack: 2),
            HelperDefinition(
                helper,
                helperBody,
                maxStack: 2,
                exceptionRegionCount: hasExceptionRegion ? 1 : 0));
        resolver.Calls[(root, helper.MetadataToken)] = Target(helper);

        var result = new MethodGraphPlanner(resolver).Prepare(root);

        AssertFailure(result, MachineRunStatus.Blocked, code);
        Assert.Equal(helper, result.Failure!.Method);
        Assert.Equal(offset, result.Failure.IlOffset);
    }

    /// <summary>Proves first-result caching and distinct-edge retention for repeated structural operands.</summary>
    [Fact]
    public void RepeatedStructuralRequestsUseFirstResultsWhileEveryCallEdgeIsRetained()
    {
        var root = Method(1);
        var helper = Method(2);
        var field = Field(1, RootType);
        var rootCode = new List<byte>();
        rootCode.Add(0x02);
        EmitToken(rootCode, 0x7B, field.Handle.MetadataToken);
        rootCode.Add(0x02);
        EmitToken(rootCode, 0x7B, field.Handle.MetadataToken);
        EmitToken(rootCode, 0x28, helper.MetadataToken);
        rootCode.Add(0x16);
        rootCode.Add(0x16);
        EmitToken(rootCode, 0x28, helper.MetadataToken);
        rootCode.Add(0x58);
        rootCode.Add(0x2A);
        var resolver = Resolver(
            RootDefinition(root, rootCode.ToArray(), maxStack: 3),
            HelperDefinition(helper, LeafBody(), maxStack: 2));
        resolver.Fields[(root, field.Handle.MetadataToken)] = field;
        var callFactoryCount = 0;
        resolver.CallFactory = (_, _) => ResolutionResult<ResolvedMethodCallTarget>.Success(
            ++callFactoryCount == 1 ? Target(helper) : Target(Method(3)));

        var result = new MethodGraphPlanner(resolver).Prepare(root);

        Assert.True(result.IsSuccess, result.Failure?.Code);
        Assert.Equal(2, result.Plan!.CallSites.Length);
        Assert.Single(result.Plan.Fields);
        Assert.Equal(1, resolver.FieldCount(root, field.Handle.MetadataToken));
        Assert.Equal(1, resolver.CallCount(root, helper.MetadataToken));
        Assert.Equal(1, callFactoryCount);
        Assert.Equal(1, resolver.DefinitionCount(helper));
        Assert.Equal(5, result.Plan.TraversalUnitCount);
    }

    /// <summary>Proves the fixed method cardinality cap rejects the sixty-fifth method before its body read.</summary>
    [Fact]
    public void SixtyFifthDistinctMethodHitsFixedMethodCapBeforeBodyAcquisition()
    {
        var handles = Enumerable.Range(1, 65).Select(Method).ToArray();
        var definitions = new List<ResolvedMethodDefinition>
        {
            RootDefinition(handles[0], RootCallsBody(handles[1]), maxStack: 2),
        };
        for (var index = 1; index < handles.Length; index++)
        {
            definitions.Add(HelperDefinition(
                handles[index],
                index == handles.Length - 1 ? LeafBody() : CallsBody(handles[index + 1]),
                maxStack: 2));
        }

        var resolver = Resolver(definitions.ToArray());
        for (var index = 0; index < handles.Length - 1; index++)
        {
            resolver.Calls[(handles[index], handles[index + 1].MetadataToken)] = Target(handles[index + 1]);
        }

        var result = new MethodGraphPlanner(resolver).Prepare(handles[0]);

        AssertFailure(result, MachineRunStatus.Blocked, "EXEC_CALL_GRAPH_METHOD_LIMIT");
        Assert.Equal(0, resolver.DefinitionCount(handles[^1]));
        Assert.Equal(64, resolver.DefinitionCounts.Values.Sum());
    }

    /// <summary>Proves traversal unit 1,025 is blocked atomically by the fixed preparation safety cap.</summary>
    [Fact]
    public void FixedTraversalCapBlocksAtUnit1025WithoutReturningPartialGraph()
    {
        var handles = Enumerable.Range(1, 5).Select(Method).ToArray();
        var definitions = new List<ResolvedMethodDefinition>
        {
            RootDefinition(handles[0], RootRepeatedCallsBody(handles[1], 256), maxStack: 3),
        };
        for (var index = 1; index < handles.Length - 1; index++)
        {
            definitions.Add(HelperDefinition(
                handles[index],
                RepeatedCallsBody(handles[index + 1], 256),
                maxStack: 3));
        }

        definitions.Add(HelperDefinition(handles[^1], LeafBody(), maxStack: 2));
        var resolver = Resolver(definitions.ToArray());
        for (var index = 0; index < handles.Length - 1; index++)
        {
            resolver.Calls[(handles[index], handles[index + 1].MetadataToken)] = Target(handles[index + 1]);
        }

        var result = new MethodGraphPlanner(resolver).Prepare(handles[0]);

        AssertFailure(result, MachineRunStatus.Blocked, "EXEC_CALL_GRAPH_TRAVERSAL_LIMIT");
        Assert.Equal(ExecutionFailureKind.ResourceLimit, result.Failure!.Kind);
        Assert.Equal(0, resolver.DefinitionCount(handles[^1]));
    }

    /// <summary>Proves equal field identities cannot carry conflicting frozen descriptors across the graph.</summary>
    [Fact]
    public void StructuralFieldConflictMapsToGraphInvalidAndReturnsNoPartialPlan()
    {
        var root = Method(1);
        var helper = Method(2);
        var field = Field(1, RootType);
        var conflicting = new ResolvedField(
            field.Handle,
            RootType,
            TypeSig.Int32,
            isStatic: true,
            isLiteral: false,
            hasRva: false);
        var resolver = Resolver(
            RootDefinition(
                root,
                ExactRootBody(field.Handle.MetadataToken, field.Handle.MetadataToken, helper.MetadataToken),
                maxStack: 2),
            HelperDefinition(helper, FieldThenReturnBody(field.Handle.MetadataToken), maxStack: 2));
        resolver.Calls[(root, helper.MetadataToken)] = Target(helper);
        resolver.Fields[(root, field.Handle.MetadataToken)] = field;
        resolver.Fields[(helper, field.Handle.MetadataToken)] = conflicting;

        var result = new MethodGraphPlanner(resolver).Prepare(root);

        AssertConflictFailure(result, "EXEC_FIELD_DESCRIPTOR_CONFLICT");
        Assert.Equal(helper, result.Failure!.Method);
        Assert.Equal(1, result.Failure.IlOffset);
    }

    /// <summary>Proves root request/result identity disagreement is blocked with structured conflict evidence.</summary>
    [Fact]
    public void RootDefinitionIdentityMismatchIsAConflict()
    {
        var root = Method(1);
        var different = Method(2);
        var resolver = new GraphResolver();
        resolver.Definitions[root] = RootDefinition(different, [0x16, 0x2A], maxStack: 1);

        var result = new MethodGraphPlanner(resolver).Prepare(root);

        AssertConflictFailure(result, "EXEC_METHOD_IDENTITY_CONFLICT");
        Assert.Equal(root, result.Failure!.Method);
    }

    /// <summary>Proves a W4 field descriptor disagreeing with its operand is conflict, while legacy behavior is untouched.</summary>
    [Fact]
    public void GraphFieldOperandIdentityMismatchIsAConflict()
    {
        var root = Method(1);
        var helper = Method(2);
        var operandField = Field(1, RootType);
        var differentField = Field(2, RootType);
        var resolver = Resolver(
            RootDefinition(
                root,
                ExactRootBody(
                    operandField.Handle.MetadataToken,
                    operandField.Handle.MetadataToken,
                    helper.MetadataToken),
                maxStack: 2));
        resolver.Fields[(root, operandField.Handle.MetadataToken)] = differentField;
        resolver.Calls[(root, helper.MetadataToken)] = Target(helper);

        var result = new MethodGraphPlanner(resolver).Prepare(root);

        AssertConflictFailure(result, "EXEC_FIELD_IDENTITY_CONFLICT");
        Assert.Equal(1, result.Failure!.IlOffset);
        Assert.Empty(resolver.CallCounts);
    }

    /// <summary>Proves fresh equivalent preparations are content-equal and canonically ordered.</summary>
    [Fact]
    public void FreshPreparationsFromDifferentInsertionOrdersAreContentEqualAndCanonical()
    {
        var root = Method(9);
        var left = Method(2);
        var right = Method(7);
        var definitions = new[]
        {
            RootDefinition(root, RootCallsBody(right, left), maxStack: 3),
            HelperDefinition(left, LeafBody(), maxStack: 2),
            HelperDefinition(right, LeafBody(), maxStack: 2),
        };
        var firstResolver = Resolver(definitions);
        firstResolver.Calls[(root, right.MetadataToken)] = Target(right);
        firstResolver.Calls[(root, left.MetadataToken)] = Target(left);
        var secondResolver = Resolver(definitions.Reverse().ToArray());
        secondResolver.Calls[(root, left.MetadataToken)] = Target(left);
        secondResolver.Calls[(root, right.MetadataToken)] = Target(right);

        var first = new MethodGraphPlanner(firstResolver).Prepare(root);
        var second = new MethodGraphPlanner(secondResolver).Prepare(root);

        Assert.True(first.IsSuccess, first.Failure?.Code);
        Assert.True(second.IsSuccess, second.Failure?.Code);
        Assert.NotSame(first.Plan, second.Plan);
        Assert.Equal(first.Plan, second.Plan);
        Assert.Equal(first.Plan!.GetHashCode(), second.Plan!.GetHashCode());
        Assert.Equal([left, right, root], first.Plan.Nodes.Select(node => node.Method));
        Assert.Equal([(root, 2), (root, 9)], first.Plan.CallSites.Select(site => (site.Caller, site.IlOffset)));
    }

    /// <summary>Proves the W4 root profile requires a direct call and exact receiver-owned fields.</summary>
    [Fact]
    public void RootWithoutDirectCallAndWrongFieldOwnerAreRejectedAtomically()
    {
        var root = Method(1);
        var noCallResolver = Resolver(RootDefinition(root, [0x16, 0x2A], maxStack: 1));

        var noCall = new MethodGraphPlanner(noCallResolver).Prepare(root);

        AssertFailure(noCall, MachineRunStatus.Blocked, "EXEC_W4_ROOT_CALL_REQUIRED");

        var helper = Method(2);
        var wrongOwner = Field(1, HelperType);
        var ownerResolver = Resolver(
            RootDefinition(
                root,
                ExactRootBody(wrongOwner.Handle.MetadataToken, wrongOwner.Handle.MetadataToken, helper.MetadataToken),
                maxStack: 2),
            HelperDefinition(helper, LeafBody(), maxStack: 2));
        ownerResolver.Fields[(root, wrongOwner.Handle.MetadataToken)] = wrongOwner;
        ownerResolver.Calls[(root, helper.MetadataToken)] = Target(helper);

        var owner = new MethodGraphPlanner(ownerResolver).Prepare(root);

        AssertFailure(owner, MachineRunStatus.Blocked, "EXEC_FIELD_OWNER_UNSUPPORTED");
        Assert.Equal(0, ownerResolver.CallCount(root, helper.MetadataToken));
    }

    /// <summary>Proves definition-only root profile failures precede all field and method operand resolution.</summary>
    [Fact]
    public void WrongRootSignatureRejectsBeforeOperandResolution()
    {
        var root = Method(1);
        var helper = Method(2);
        var field = Field(1, RootType);
        var resolver = Resolver(Definition(
            root,
            HelperSignature(HelperType),
            ExactRootBody(field.Handle.MetadataToken, field.Handle.MetadataToken, helper.MetadataToken),
            maxStack: 2));
        resolver.Fields[(root, field.Handle.MetadataToken)] = field;
        resolver.Calls[(root, helper.MetadataToken)] = Target(helper);

        var result = new MethodGraphPlanner(resolver).Prepare(root);

        AssertFailure(result, MachineRunStatus.Blocked, "EXEC_W4_ROOT_SIGNATURE_UNSUPPORTED");
        Assert.Empty(resolver.FieldCounts);
        Assert.Empty(resolver.CallCounts);
    }

    internal static GraphResolver Resolver(params ResolvedMethodDefinition[] definitions)
    {
        var resolver = new GraphResolver();
        foreach (var definition in definitions)
        {
            resolver.Definitions.Add(definition.Method, definition);
        }

        return resolver;
    }

    internal static MethodHandle Method(int row) => new(Module, 0x06000000 | row);

    internal static ResolvedField Field(int row, TypeSig declaringType) => new(
        new FieldHandle(Module, 0x04000000 | row),
        declaringType,
        TypeSig.Int32,
        isStatic: false,
        isLiteral: false,
        hasRva: false);

    internal static ResolvedMethodCallTarget Target(MethodHandle method) =>
        new(method, HelperSignature(HelperType));

    private static MethodCallSignatureShape RootSignature() => new(
        RootType,
        MethodCallingConventionKind.Default,
        hasImplicitThis: true,
        hasExplicitThis: false,
        genericParameterCount: 0,
        ImmutableArray<TypeSig>.Empty,
        TypeSig.Int32);

    private static MethodCallSignatureShape HelperSignature(TypeSig declaringType) => new(
        declaringType,
        MethodCallingConventionKind.Default,
        hasImplicitThis: false,
        hasExplicitThis: false,
        genericParameterCount: 0,
        ImmutableArray.Create(TypeSig.Int32, TypeSig.Int32),
        TypeSig.Int32);

    internal static ResolvedMethodDefinition RootDefinition(
        MethodHandle method,
        byte[] code,
        int maxStack) =>
        Definition(method, RootSignature(), code, maxStack);

    internal static ResolvedMethodDefinition HelperDefinition(
        MethodHandle method,
        byte[] code,
        int maxStack,
        int exceptionRegionCount = 0) =>
        Definition(
            method,
            HelperSignature(HelperType),
            code,
            maxStack,
            exceptionRegionCount);

    private static ResolvedMethodDefinition Definition(
        MethodHandle method,
        MethodCallSignatureShape signature,
        byte[] code,
        int maxStack,
        int exceptionRegionCount = 0) =>
        new(
            method,
            IlBody.Create(
                maxStack,
                code,
                exceptionRegionCount: exceptionRegionCount),
            new MethodSignatureShape(signature, ImmutableArray<TypeSig>.Empty));

    internal static byte[] ExactRootBody(int firstFieldToken, int secondFieldToken, int helperToken)
    {
        var code = new List<byte> { 0x02 };
        EmitToken(code, 0x7B, firstFieldToken);
        code.Add(0x02);
        EmitToken(code, 0x7B, secondFieldToken);
        EmitToken(code, 0x28, helperToken);
        code.Add(0x2A);
        return code.ToArray();
    }

    private static byte[] CallsBody(params MethodHandle[] targets) =>
        BuildCallsBody(
            targets.Select(target => target.MetadataToken).ToArray(),
            useArguments: true);

    private static byte[] CallsBody(params int[] targetTokens)
        => BuildCallsBody(targetTokens, useArguments: true);

    private static byte[] RootCallsBody(params MethodHandle[] targets) =>
        BuildCallsBody(
            targets.Select(target => target.MetadataToken).ToArray(),
            useArguments: false);

    private static byte[] RootCallsBody(params int[] targetTokens) =>
        BuildCallsBody(targetTokens, useArguments: false);

    private static byte[] BuildCallsBody(int[] targetTokens, bool useArguments)
    {
        var code = new List<byte>();
        for (var index = 0; index < targetTokens.Length; index++)
        {
            code.Add(useArguments ? (byte)0x02 : (byte)0x16);
            code.Add(useArguments ? (byte)0x03 : (byte)0x16);
            EmitToken(code, 0x28, targetTokens[index]);
            if (index != 0)
            {
                code.Add(0x58);
            }
        }

        code.Add(0x2A);
        return code.ToArray();
    }

    private static byte[] RepeatedCallsBody(MethodHandle target, int callCount)
    {
        var tokens = Enumerable.Repeat(target.MetadataToken, callCount).ToArray();
        return CallsBody(tokens);
    }

    internal static byte[] RootRepeatedCallsBody(MethodHandle target, int callCount)
    {
        var tokens = Enumerable.Repeat(target.MetadataToken, callCount).ToArray();
        return RootCallsBody(tokens);
    }

    private static byte[] LeafBody() => [0x02, 0x03, 0x58, 0x2A];

    private static byte[] FieldThenReturnBody(int fieldToken)
    {
        var code = new List<byte> { 0x02 };
        EmitToken(code, 0x7B, fieldToken);
        code.Add(0x03);
        code.Add(0x58);
        code.Add(0x2A);
        return code.ToArray();
    }

    private static byte[] TokenInstructionBody(byte opcode, int token)
    {
        var code = new List<byte> { 0x16, 0x16 };
        EmitToken(code, opcode, token);
        code.Add(0x2A);
        return code.ToArray();
    }

    private static byte[] PrefixedTokenBody(byte secondOpcode, int token)
    {
        var code = new List<byte> { 0xFE, secondOpcode };
        code.Add((byte)token);
        code.Add((byte)(token >> 8));
        code.Add((byte)(token >> 16));
        code.Add((byte)(token >> 24));
        code.Add(0x2A);
        return code.ToArray();
    }

    private static byte[] PrefixThenCallBody(byte secondOpcode, int token, bool prefixHasToken = false)
    {
        var code = new List<byte> { 0xFE, secondOpcode };
        if (prefixHasToken)
        {
            code.Add((byte)token);
            code.Add((byte)(token >> 8));
            code.Add((byte)(token >> 16));
            code.Add((byte)(token >> 24));
        }

        EmitToken(code, 0x28, token);
        code.Add(0x2A);
        return code.ToArray();
    }

    private static void EmitToken(List<byte> code, byte opcode, int token)
    {
        code.Add(opcode);
        code.Add((byte)token);
        code.Add((byte)(token >> 8));
        code.Add((byte)(token >> 16));
        code.Add((byte)(token >> 24));
    }

    private static void AssertBoundary(MethodInstructionBoundary boundary, int offset, params TypeSig[] stack)
    {
        Assert.Equal(offset, boundary.IlOffset);
        Assert.Equal(stack, boundary.ExpectedStackTypes);
    }

    private static void AssertFailure(
        MethodGraphPreparationResult result,
        MachineRunStatus status,
        string code)
    {
        Assert.False(result.IsSuccess);
        Assert.Null(result.Plan);
        Assert.Equal(status, result.Status);
        Assert.Equal(code, Assert.IsType<ExecutionFailure>(result.Failure).Code);
    }

    private static void AssertConflictFailure(MethodGraphPreparationResult result, string code)
    {
        AssertFailure(result, MachineRunStatus.Blocked, code);
        Assert.Equal(
            ResolutionFailureKind.Conflict,
            Assert.IsType<ExecutionFailure>(result.Failure).ResolutionFailure?.Kind);
    }

    internal sealed class GraphResolver : IResolutionServices
    {
        internal Dictionary<MethodHandle, ResolvedMethodDefinition> Definitions { get; } = [];

        internal Dictionary<(MethodHandle Context, int Token), ResolvedField> Fields { get; } = [];

        internal Dictionary<(MethodHandle Context, int Token), ResolvedMethodCallTarget> Calls { get; } = [];

        internal Dictionary<MethodHandle, int> DefinitionCounts { get; } = [];

        internal Dictionary<(MethodHandle Context, int Token), int> FieldCounts { get; } = [];

        internal Dictionary<(MethodHandle Context, int Token), int> CallCounts { get; } = [];

        internal List<string> Operations { get; } = [];

        internal Func<MethodHandle, int, ResolutionResult<ResolvedMethodCallTarget>>? CallFactory { get; set; }

        public ResolutionResult<ResolvedMethodDefinition> GetMethodDefinition(MethodHandle method)
        {
            Operations.Add($"definition:{method.MetadataToken:X8}");
            Increment(DefinitionCounts, method);
            return Definitions.TryGetValue(method, out var definition)
                ? ResolutionResult<ResolvedMethodDefinition>.Success(definition)
                : ResolutionResult<ResolvedMethodDefinition>.Failed(
                    ResolutionFailureKind.Unavailable,
                    "TEST_METHOD_UNAVAILABLE",
                    "Synthetic method definition was not configured.");
        }

        public ResolutionResult<ResolvedField> ResolveField(MethodHandle contextMethod, int metadataToken)
        {
            Operations.Add($"field:{contextMethod.MetadataToken:X8}:{metadataToken:X8}");
            var key = (contextMethod, metadataToken);
            Increment(FieldCounts, key);
            return Fields.TryGetValue(key, out var field)
                ? ResolutionResult<ResolvedField>.Success(field)
                : ResolutionResult<ResolvedField>.Failed(
                    ResolutionFailureKind.Unavailable,
                    "TEST_FIELD_UNAVAILABLE",
                    "Synthetic field descriptor was not configured.");
        }

        public ResolutionResult<ResolvedMethodCallTarget> ResolveMethod(
            MethodHandle contextMethod,
            int metadataToken)
        {
            Operations.Add($"call:{contextMethod.MetadataToken:X8}:{metadataToken:X8}");
            var key = (contextMethod, metadataToken);
            Increment(CallCounts, key);
            if (CallFactory is not null)
            {
                return CallFactory(contextMethod, metadataToken);
            }

            return Calls.TryGetValue(key, out var target)
                ? ResolutionResult<ResolvedMethodCallTarget>.Success(target)
                : ResolutionResult<ResolvedMethodCallTarget>.Failed(
                    ResolutionFailureKind.Unavailable,
                    "TEST_CALL_UNAVAILABLE",
                    "Synthetic direct-call target was not configured.");
        }

        internal int DefinitionCount(MethodHandle method) => DefinitionCounts.GetValueOrDefault(method);

        internal int FieldCount(MethodHandle method, int token) => FieldCounts.GetValueOrDefault((method, token));

        internal int CallCount(MethodHandle method, int token) => CallCounts.GetValueOrDefault((method, token));

        private static void Increment<TKey>(Dictionary<TKey, int> counts, TKey key)
            where TKey : notnull =>
            counts[key] = counts.GetValueOrDefault(key) + 1;
    }
}
