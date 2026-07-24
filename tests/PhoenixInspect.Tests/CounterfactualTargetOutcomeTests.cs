using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using PhoenixInspect.Core.Abstractions;
using PhoenixInspect.Core.Execution;
using PhoenixInspect.Domain.Concrete;
using PhoenixInspect.Product.DumpDebugging;
using Xunit;
using IlBody = PhoenixInspect.Core.Abstractions.MethodBody;
using ModuleHandle = PhoenixInspect.Core.Abstractions.ModuleHandle;

namespace PhoenixInspect.Tests;

/// <summary>
/// Exercises W4.7's standalone projection of a complete, certified exact-null machine execution.
/// </summary>
public sealed class CounterfactualTargetOutcomeTests
{
    private const long InitialInstructionUnits = 10;
    private const long RemainingInstructionUnits = 8;
    private const string ExpectedCanonicalHex =
        "0000003350686F656E6978496E73706563742E436F756E7465726661637475616C5461726765" +
        "744F7574636F6D652E467261676D656E74000000010000000100000001000000010000000100" +
        "0000010000000100000001000000155441524745545F4E554C4C5F5245464552454E43450123" +
        "456789ABCDEFFEDCBA98765432100600000100000001000000010123456789ABCDEFFEDCBA98" +
        "7654321006000001000000000000000A00000000000000020000000000000008000000020000" +
        "00010123456789ABCDEFFEDCBA987654321006000001000000000000000C4C6F616441726775" +
        "6D656E74000000020123456789ABCDEFFEDCBA98765432100600000100000001000000094C6F" +
        "61644669656C64000000010000002057342E546172676574457863657074696F6E2E4E756C6C" +
        "5265666572656E6365000000515468652061646D6974746564206669656C64206C6F61642074" +
        "65726D696E61746564207769746820616E206578616374206E756C6C2D7265666572656E6365" +
        "2074617267657420657863657074696F6E2E";
    private const string ExpectedSha256 =
        "99cadd992d88ac481b570ec4bc1eb3c914f7d43565db414d9147225e01a9c754";

    private static readonly ModuleHandle Module = new(
        0x0123_4567_89AB_CDEFUL,
        0xFEDC_BA98_7654_3210UL);

    private static readonly MethodHandle Method = new(Module, 0x06000001);
    private static readonly MethodHandle OtherMethod = new(Module, 0x06000002);
    private static readonly TypeSig DeclaringType = TypeSig.CreateTypeDefinition(
        Module,
        0x02000001,
        "PhoenixInspect.Tests.CounterfactualTargetFixture");
    private static readonly ResolvedField Int32Field = new(
        new FieldHandle(Module, 0x04000001),
        DeclaringType,
        TypeSig.Int32,
        isStatic: false,
        isLiteral: false,
        hasRva: false);

    /// <summary>
    /// Runs the real legacy machine from a complete IL-zero activation and projects its two certified transitions
    /// into the exact standalone axes, trace, accounting, diagnostic, and fixed canonical identity.
    /// </summary>
    [Fact]
    public void CompleteCertifiedMachineExecutionProducesFixedCanonicalTargetFragment()
    {
        var fixture = CreateCertifiedFixture();

        var result = Project(fixture.Input);

        Assert.True(result.IsSuccess, result.Failure?.Code);
        Assert.Null(result.Failure);
        var fragment = Assert.IsType<CounterfactualTargetOutcomeFragment>(result.Fragment);
        Assert.Equal(CounterfactualTargetOutcomeFragment.CanonicalSchemaVersion, fragment.SchemaVersion);
        Assert.Equal(1, fragment.SchemaVersion);
        Assert.Equal(EvaluationSemanticMode.CounterfactualExecution, fragment.SemanticMode);
        Assert.Equal(EvaluationCompletionStatus.Completed, fragment.Completion);
        Assert.Equal(EvaluationCompleteness.Complete, fragment.Completeness);
        Assert.Equal(EvaluationEvidenceStatus.Exact, fragment.Evidence);
        Assert.Equal(EvaluationEffectStatus.None, fragment.Effects);
        Assert.Equal(MachineRunStatus.TargetException, fragment.TerminalStatus);
        Assert.Equal(TargetExceptionKind.NullReference, fragment.TargetException.Kind);
        Assert.Equal("TARGET_NULL_REFERENCE", fragment.TargetException.Code);
        Assert.Equal(Method, fragment.TargetException.Method);
        Assert.Equal(1, fragment.TargetException.IlOffset);
        Assert.True(fragment.CallTrace.AsSpan().SequenceEqual([Method]));
        Assert.Equal(InitialInstructionUnits, fragment.InitialInstructionUnits);
        Assert.Equal(2, fragment.UsedInstructionUnits);
        Assert.Equal(RemainingInstructionUnits, fragment.RemainingInstructionUnits);
        Assert.Collection(
            fragment.Events,
            item => AssertEvent(item, DebugEventKind.InstructionExecuted, Method, 0, "LoadArgument"),
            item => AssertEvent(item, DebugEventKind.TargetExceptionRaised, Method, 1, "LoadField"));
        Assert.Equal(
            "W4.TargetException.NullReference",
            Assert.Single(fragment.Diagnostics).Code);
        Assert.Equal(ExpectedCanonicalHex, Convert.ToHexString(fragment.CanonicalBytes.AsSpan()));
        Assert.Equal(ExpectedSha256, fragment.Sha256);
        Assert.True(fragment.CanonicalBytes.AsSpan().SequenceEqual(
            CounterfactualTargetOutcomeCanonicalCodec.SerializeCanonical(fragment).AsSpan()));
        Assert.Equal(fragment.Sha256, CounterfactualTargetOutcomeCanonicalCodec.ComputeSha256(fragment));
    }

    /// <summary>
    /// Proves independently executed certified machine sequences reproduce fragment equality, operators, canonical
    /// bytes, SHA-256, and deterministic hash codes without relying on machine or fragment reference identity.
    /// </summary>
    [Fact]
    public void IndependentCertifiedExecutionsProduceContentEqualFragmentsAndHashes()
    {
        var first = AssertSuccess(Project(CreateCertifiedFixture().Input));
        var second = AssertSuccess(Project(CreateCertifiedFixture().Input));

        Assert.NotSame(first, second);
        Assert.Equal(first, second);
        Assert.True(first == second);
        Assert.False(first != second);
        Assert.True(first.Equals((object)second));
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
        Assert.True(first.CanonicalBytes.AsSpan().SequenceEqual(second.CanonicalBytes.AsSpan()));
        Assert.Equal(first.Sha256, second.Sha256);

        CounterfactualTargetOutcomeFragment? absent = null;
        Assert.False(first == absent);
        Assert.True(first != absent);
        Assert.True(absent == null);
    }

    /// <summary>
    /// Verifies an actual certified idempotent terminal re-step may follow the first latch without changing the
    /// canonical fragment, accounting, transcript, or outcome identity.
    /// </summary>
    [Fact]
    public void CertifiedTerminalRestepIsIdempotentAndCanonical()
    {
        var fixture = CreateCertifiedFixture();
        var first = AssertSuccess(Project(fixture.Input));
        var withRestep = fixture.Input with
        {
            Transitions = fixture.Input.Transitions.Add(fixture.Restep)
        };

        var replay = AssertSuccess(Project(withRestep));

        Assert.Equal(first, replay);
        Assert.True(first.CanonicalBytes.AsSpan().SequenceEqual(replay.CanonicalBytes.AsSpan()));
        Assert.Equal(first.Sha256, replay.Sha256);
        Assert.Equal(2, replay.UsedInstructionUnits);
        Assert.Equal(RemainingInstructionUnits, replay.RemainingInstructionUnits);
        Assert.Equal(2, replay.Events.Length);
    }

    /// <summary>
    /// Proves machine issuance is process-local reference evidence: only each original outcome with its exact input
    /// state and operational-state references is certified, while copies and equal fresh executions are not.
    /// </summary>
    [Fact]
    public void MachineTransitionCertificationRequiresExactOutcomeAndInputReferences()
    {
        var fixture = CreateCertifiedFixture();
        var other = CreateCertifiedFixture();
        var first = fixture.Input.Transitions[0];
        var terminal = fixture.Input.Transitions[1];

        Assert.True(first.IsMachineIssuedTransitionFrom(
            fixture.Input.Machine!,
            fixture.Input.InitialState!,
            fixture.Input.InitialOperationalState!));
        Assert.True(terminal.IsMachineIssuedTransitionFrom(
            fixture.Input.Machine!,
            first.State,
            first.OperationalState));
        Assert.True(fixture.Restep.IsMachineIssuedTransitionFrom(
            fixture.Input.Machine!,
            terminal.State,
            terminal.OperationalState));
        Assert.True(fixture.SecondRestep.IsMachineIssuedTransitionFrom(
            fixture.Input.Machine!,
            fixture.Restep.State,
            fixture.Restep.OperationalState));

        Assert.False(first.IsMachineIssuedTransitionFrom(
            fixture.Input.Machine!,
            fixture.Input.InitialState! with { },
            fixture.Input.InitialOperationalState!));
        Assert.False(first.IsMachineIssuedTransitionFrom(
            fixture.Input.Machine!,
            fixture.Input.InitialState!,
            fixture.Input.InitialOperationalState! with { }));
        Assert.False(first.IsMachineIssuedTransitionFrom(
            fixture.Input.Machine!,
            other.Input.InitialState!,
            other.Input.InitialOperationalState!));
        Assert.False(first.IsMachineIssuedTransitionFrom(
            other.Input.Machine!,
            fixture.Input.InitialState!,
            fixture.Input.InitialOperationalState!));
        Assert.False((first with { }).IsMachineIssuedTransitionFrom(
            fixture.Input.Machine!,
            fixture.Input.InitialState!,
            fixture.Input.InitialOperationalState!));
        Assert.False(CloneOutcome(first).IsMachineIssuedTransitionFrom(
            fixture.Input.Machine!,
            fixture.Input.InitialState!,
            fixture.Input.InitialOperationalState!));
        Assert.False(terminal.IsMachineIssuedTransitionFrom(
            fixture.Input.Machine!,
            fixture.Input.InitialState!,
            fixture.Input.InitialOperationalState!));
        Assert.Throws<ArgumentNullException>(() =>
            first.IsMachineIssuedTransitionFrom(
                null!,
                fixture.Input.InitialState!,
                fixture.Input.InitialOperationalState!));
        Assert.Throws<ArgumentNullException>(() =>
            first.IsMachineIssuedTransitionFrom(
                fixture.Input.Machine!,
                null!,
                fixture.Input.InitialOperationalState!));
        Assert.Throws<ArgumentNullException>(() =>
            first.IsMachineIssuedTransitionFrom(
                fixture.Input.Machine!,
                fixture.Input.InitialState!,
                null!));
    }

    /// <summary>
    /// Rejects a fully shape-valid caller-authored transcript, an otherwise identical outcome copy, and an otherwise
    /// identical record with-copy because public machine records cannot substitute for core-issued transitions.
    /// </summary>
    [Fact]
    public void StructurallyValidCallerAuthoredTransitionsAreNotExecutionEvidence()
    {
        var fixture = CreateCertifiedFixture();
        var other = CreateCertifiedFixture();
        var input = fixture.Input;
        var first = input.Transitions[0];
        var terminal = input.Transitions[1];

        AssertFailure(
            "W4.TargetException.OutcomeRequired",
            input with
            {
                Transitions = ImmutableArray.Create(CloneOutcome(first), CloneOutcome(terminal))
            });
        AssertFailure(
            "W4.TargetException.OutcomeRequired",
            ReplaceTransition(input, 0, first with { }));
        AssertFailure(
            "W4.TargetException.OutcomeRequired",
            ReplaceTransition(input, 1, terminal with { }));
        AssertFailure(
            "W4.TargetException.OutcomeRequired",
            ReplaceTransition(
                input,
                0,
                first with { Events = ImmutableArray.CreateRange(first.Events.AsSpan().ToArray()) }));
        AssertFailure(
            "W4.TargetException.OutcomeRequired",
            input with { Machine = other.Input.Machine });
    }

    /// <summary>
    /// Rejects default, empty, null, omitted, reordered, duplicated, skipped, oversized, and forged transition
    /// sequences with the stable structural or missing-execution-evidence code reached by each malformed chronology.
    /// </summary>
    [Fact]
    public void IncompleteOrForgedTransitionChronologyIsRejected()
    {
        var fixture = CreateCertifiedFixture();
        var input = fixture.Input;
        var first = input.Transitions[0];
        var terminal = input.Transitions[1];

        AssertFailures(
            input,
            new FailureCase(
                "default sequence",
                "W4.TargetException.OutcomeRequired",
                item => item with { Transitions = default }),
            new FailureCase(
                "empty sequence",
                "W4.TargetException.OutcomeRequired",
                item => item with { Transitions = [] }),
            new FailureCase(
                "null sequence item",
                "W4.TargetException.OutcomeRequired",
                item => item with
                {
                    Transitions = ImmutableArray.CreateRange(
                        new StepOutcome<ConcreteValue, ConcreteMemory>[] { null! })
                }),
            new FailureCase(
                "omitted terminal",
                "W4.TargetException.KindUnsupported",
                item => item with { Transitions = ImmutableArray.Create(first) }),
            new FailureCase(
                "skipped ready prefix",
                "W4.TargetException.LocationInvalid",
                item => item with { Transitions = ImmutableArray.Create(terminal) }),
            new FailureCase(
                "reordered sequence",
                "W4.TargetException.LocationInvalid",
                item => item with { Transitions = ImmutableArray.Create(terminal, first) }),
            new FailureCase(
                "duplicated ready transition",
                "W4.TargetException.LocationInvalid",
                item => item with { Transitions = ImmutableArray.Create(first, first, terminal) }),
            new FailureCase(
                "duplicated terminal transition",
                "W4.TargetException.EventTraceInvalid",
                item => item with { Transitions = ImmutableArray.Create(first, terminal, terminal) }),
            new FailureCase(
                "forged prefix record",
                "W4.TargetException.OutcomeRequired",
                item => item with
                {
                    Transitions = ImmutableArray.Create(CloneOutcome(first), terminal)
                }),
            new FailureCase(
                "oversized sequence",
                "W4.TargetException.AccountingInvalid",
                item => item with
                {
                    Transitions = Enumerable.Repeat(first, 4_098).ToImmutableArray()
                }));
    }

    /// <summary>
    /// Verifies projection copies caller-owned transition arrays and each array-valued fragment getter returns an
    /// independent copy, so later mutations cannot rewrite retained replay material.
    /// </summary>
    [Fact]
    public void ProjectionAndFragmentDefensivelyCopyEveryArrayBoundary()
    {
        var fixture = CreateCertifiedFixture();
        var source = fixture.Input.Transitions.ToArray();
        var aliased = ImmutableCollectionsMarshal.AsImmutableArray(source);
        var fragment = AssertSuccess(Project(fixture.Input with { Transitions = aliased }));
        var expectedBytes = fragment.CanonicalBytes.ToArray();
        var expectedSha = fragment.Sha256;

        source[0] = source[0] with { Status = MachineRunStatus.Completed };
        Assert.Equal(Method, fragment.Events[0].Method);
        Assert.Equal(0, fragment.Events[0].IlOffset);

        var events = fragment.Events;
        ImmutableCollectionsMarshal.AsArray(events)![0] =
            CreateEvent(DebugEventKind.InstructionExecuted, OtherMethod, 77, "LoadArgument");
        Assert.Equal(Method, fragment.Events[0].Method);
        Assert.Equal(0, fragment.Events[0].IlOffset);

        var trace = fragment.CallTrace;
        ImmutableCollectionsMarshal.AsArray(trace)![0] = OtherMethod;
        Assert.Equal(Method, Assert.Single(fragment.CallTrace));

        var diagnostics = fragment.Diagnostics;
        ImmutableCollectionsMarshal.AsArray(diagnostics)![0] =
            new EvaluationDiagnostic("W4.TargetException.Mutated", "Mutated copy.");
        Assert.Equal("W4.TargetException.NullReference", Assert.Single(fragment.Diagnostics).Code);

        var bytes = fragment.CanonicalBytes;
        ImmutableCollectionsMarshal.AsArray(bytes)![0] ^= 0xFF;
        Assert.True(expectedBytes.AsSpan().SequenceEqual(fragment.CanonicalBytes.AsSpan()));
        Assert.Equal(expectedSha, fragment.Sha256);
    }

    /// <summary>
    /// Guards the intentionally narrow standalone fragment surface against synthetic snapshot, root, product
    /// request, plan, or preparation-traversal identities.
    /// </summary>
    [Fact]
    public void FragmentSurfaceContainsNoSyntheticProductReachabilityIdentity()
    {
        var publicNames = typeof(CounterfactualTargetOutcomeFragment)
            .GetMembers(BindingFlags.Instance | BindingFlags.Public)
            .Select(static member => member.Name)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        foreach (var forbidden in new[] { "Snapshot", "Root", "Request", "Plan", "Traversal" })
        {
            Assert.DoesNotContain(
                publicNames,
                name => name.Contains(forbidden, StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>Rejects each null top-level input and each null transition axis with a stable diagnostic code.</summary>
    [Fact]
    public void NullInputsFailWithStableCodes()
    {
        var input = CreateCertifiedFixture().Input;
        var first = input.Transitions[0];

        AssertFailure("W4.TargetException.OutcomeRequired", input with { Machine = null });
        AssertFailure("W4.TargetException.OutcomeRequired", input with { InitialState = null });
        AssertFailure("W4.TargetException.OutcomeRequired", input with { InitialOperationalState = null });
        AssertFailure(
            "W4.TargetException.OutcomeRequired",
            ReplaceTransition(input, 0, first with { State = null! }));
        AssertFailure(
            "W4.TargetException.OutcomeRequired",
            ReplaceTransition(input, 0, first with { OperationalState = null! }));
    }

    /// <summary>
    /// Rejects terminal outcomes whose status, failure union, latch, exception kind, code, or throwing location does
    /// not describe the one admitted exact null-reference boundary.
    /// </summary>
    [Fact]
    public void InvalidTerminalOutcomeUnionFailsWithStableCodes()
    {
        var input = CreateCertifiedFixture().Input;
        var terminal = input.Transitions[^1];
        var exception = terminal.TargetException!;

        AssertFailures(
            input,
            new FailureCase(
                "wrong status",
                "W4.TargetException.KindUnsupported",
                item => ReplaceLast(item, terminal with { Status = MachineRunStatus.Completed })),
            new FailureCase(
                "unexpected failure",
                "W4.TargetException.KindUnsupported",
                item => ReplaceLast(item, terminal with
                {
                    Failure = new ExecutionFailure(
                        ExecutionFailureKind.MemoryFailure,
                        "MEMORY_FAILURE",
                        "Host prose must not escape.")
                })),
            new FailureCase(
                "missing outcome exception",
                "W4.TargetException.OutcomeRequired",
                item => ReplaceLast(item, terminal with { TargetException = null })),
            new FailureCase(
                "missing terminal latch",
                "W4.TargetException.OutcomeRequired",
                item => ReplaceLast(
                    item,
                    terminal with { State = terminal.State with { TerminalTargetException = null } })),
            new FailureCase(
                "different latch",
                "W4.TargetException.LatchInvalid",
                item => ReplaceLast(
                    item,
                    terminal with
                    {
                        State = terminal.State with
                        {
                            TerminalTargetException = new TargetExceptionInfo(
                                TargetExceptionKind.NullReference,
                                "TARGET_DIFFERENT",
                                Method,
                                1)
                        }
                    })),
            new FailureCase(
                "undefined kind",
                "W4.TargetException.KindUnsupported",
                item => WithTerminalException(
                    item,
                    CreateMalformedException(
                        (TargetExceptionKind)int.MaxValue,
                        "TARGET_NULL_REFERENCE",
                        Method,
                        1))),
            new FailureCase(
                "wrong stable code",
                "W4.TargetException.KindUnsupported",
                item => WithTerminalException(
                    item,
                    new TargetExceptionInfo(
                        TargetExceptionKind.NullReference,
                        "TARGET_OTHER",
                        Method,
                        1))),
            new FailureCase(
                "unlocated exception",
                "W4.TargetException.LocationInvalid",
                item => WithTerminalException(
                    item,
                    new TargetExceptionInfo(TargetExceptionKind.NullReference, "TARGET_NULL_REFERENCE"))),
            new FailureCase(
                "wrong method",
                "W4.TargetException.LocationInvalid",
                item => WithTerminalException(item, AtLocation(exception, OtherMethod, 1))),
            new FailureCase(
                "wrong offset",
                "W4.TargetException.LocationInvalid",
                item => WithTerminalException(item, AtLocation(exception, Method, 2))));
    }

    /// <summary>
    /// Rejects malformed activation and terminal state shapes rather than manufacturing a call trace from default,
    /// null, rootless, multi-frame, returned, return-site-bearing, or memory-changing state.
    /// </summary>
    [Fact]
    public void InvalidStateSequenceFailsWithStableCodes()
    {
        var input = CreateCertifiedFixture().Input;
        var initial = input.InitialState!;
        var initialFrame = Assert.Single(initial.CallStack);
        var terminal = input.Transitions[^1];
        var changedMemory = new ConcreteMemoryModel(new ConcreteDomain())
            .NewObject(ConcreteMemory.Empty, DeclaringType).mem;

        AssertFailures(
            input,
            new FailureCase(
                "default activation frames",
                "W4.TargetException.LatchInvalid",
                item => item with { InitialState = initial with { CallStack = default } }),
            new FailureCase(
                "rootless activation",
                "W4.TargetException.LatchInvalid",
                item => item with { InitialState = initial with { CallStack = [] } }),
            new FailureCase(
                "null activation frame",
                "W4.TargetException.LatchInvalid",
                item => item with
                {
                    InitialState = initial with
                    {
                        CallStack = ImmutableArray.CreateRange(
                            new FrameState<ConcreteValue>[] { null! })
                    }
                }),
            new FailureCase(
                "multiple activation frames",
                "W4.TargetException.LatchInvalid",
                item => item with
                {
                    InitialState = initial with
                    {
                        CallStack = ImmutableArray.Create(initialFrame, initialFrame)
                    }
                }),
            new FailureCase(
                "activation return value",
                "W4.TargetException.LatchInvalid",
                item => item with
                {
                    InitialState = initial with
                    {
                        ReturnValue = OptionalValue<ConcreteValue>.Some(initialFrame.Arguments[0])
                    }
                }),
            new FailureCase(
                "activation return site",
                "W4.TargetException.LatchInvalid",
                item => item with
                {
                    InitialState = initial with
                    {
                        CallStack = ImmutableArray.Create(initialFrame with
                        {
                            ReturnSite = new FrameReturnSite(
                                new DirectCallSiteIdentity(Method, 0, OtherMethod),
                                5)
                        })
                    }
                }),
            new FailureCase(
                "activation already latched",
                "W4.TargetException.LatchInvalid",
                item => item with
                {
                    InitialState = initial with
                    {
                        TerminalTargetException = terminal.TargetException
                    }
                }),
            new FailureCase(
                "default activation arguments",
                "W4.TargetException.LatchInvalid",
                item => item with
                {
                    InitialState = initial with
                    {
                        CallStack = ImmutableArray.Create(initialFrame with { Arguments = default })
                    }
                }),
            new FailureCase(
                "wrong activation method",
                "W4.TargetException.LocationInvalid",
                item => item with
                {
                    InitialState = initial with
                    {
                        CallStack = ImmutableArray.Create(initialFrame with { Method = OtherMethod })
                    }
                }),
            new FailureCase(
                "nonzero activation offset",
                "W4.TargetException.LocationInvalid",
                item => item with
                {
                    InitialState = initial with
                    {
                        CallStack = ImmutableArray.Create(initialFrame with { IlOffset = 1 })
                    }
                }),
            new FailureCase(
                "default terminal frames",
                "W4.TargetException.LatchInvalid",
                item => ReplaceLast(
                    item,
                    terminal with { State = terminal.State with { CallStack = default } })),
            new FailureCase(
                "terminal nonempty frames",
                "W4.TargetException.LatchInvalid",
                item => ReplaceLast(
                    item,
                    terminal with
                    {
                        State = terminal.State with
                        {
                            CallStack = ImmutableArray.Create(initialFrame)
                        }
                    })),
            new FailureCase(
                "terminal return value",
                "W4.TargetException.LatchInvalid",
                item => ReplaceLast(
                    item,
                    terminal with
                    {
                        State = terminal.State with
                        {
                            ReturnValue = OptionalValue<ConcreteValue>.Some(initialFrame.Arguments[0])
                        }
                    })),
            new FailureCase(
                "changed terminal memory",
                "W4.TargetException.LatchInvalid",
                item => ReplaceLast(
                    item,
                    terminal with { State = terminal.State with { Memory = changedMemory } })));
    }

    /// <summary>
    /// Rejects legacy-mode depth, model-attempt, model-counter, high-water, and null-budget contradictions in the
    /// activation or either returned operational envelope.
    /// </summary>
    [Fact]
    public void InvalidOperationalSequenceFailsWithStableCodes()
    {
        var input = CreateCertifiedFixture().Input;
        var initial = input.InitialOperationalState!;
        var first = input.Transitions[0];
        var terminal = input.Transitions[1];

        AssertFailures(
            input,
            new FailureCase(
                "null activation budget",
                "W4.TargetException.ExecutionModeUnsupported",
                item => item with
                {
                    InitialOperationalState = initial with { Budget = null! }
                }),
            new FailureCase(
                "default attempts",
                "W4.TargetException.ExecutionModeUnsupported",
                item => item with
                {
                    InitialOperationalState = initial with { ModelAttempts = default }
                }),
            new FailureCase(
                "null model attempt",
                "W4.TargetException.ExecutionModeUnsupported",
                item => item with
                {
                    InitialOperationalState = initial with
                    {
                        ModelAttempts = ImmutableArray.CreateRange(
                            new PureModelAttempt[] { null! })
                    }
                }),
            new FailureCase(
                "invocation count",
                "W4.TargetException.ExecutionModeUnsupported",
                item => item with
                {
                    InitialOperationalState = initial with { ModelInvocationCount = 1 }
                }),
            new FailureCase(
                "completed model count",
                "W4.TargetException.ExecutionModeUnsupported",
                item => item with
                {
                    InitialOperationalState = initial with { CompletedModeledCallCount = 1 }
                }),
            new FailureCase(
                "configured prepared depth",
                "W4.TargetException.ExecutionModeUnsupported",
                item => item with
                {
                    InitialOperationalState = initial with
                    {
                        ConfiguredMaximumLogicalCallDepth = 1
                    }
                }),
            new FailureCase(
                "required prepared depth",
                "W4.TargetException.ExecutionModeUnsupported",
                item => item with
                {
                    InitialOperationalState = initial with { RequiredLogicalCallDepth = 1 }
                }),
            new FailureCase(
                "logical high water",
                "W4.TargetException.ExecutionModeUnsupported",
                item => item with
                {
                    InitialOperationalState = initial with { ObservedLogicalDepthHighWater = 2 }
                }),
            new FailureCase(
                "active-frame high water",
                "W4.TargetException.ExecutionModeUnsupported",
                item => item with
                {
                    InitialOperationalState = initial with { ActiveFrameDepthHighWater = 2 }
                }),
            new FailureCase(
                "ready envelope mutation",
                "W4.TargetException.ExecutionModeUnsupported",
                item => ReplaceTransition(
                    item,
                    0,
                    first with
                    {
                        OperationalState = first.OperationalState with
                        {
                            ObservedLogicalDepthHighWater = 2
                        }
                    })),
            new FailureCase(
                "terminal envelope mutation",
                "W4.TargetException.ExecutionModeUnsupported",
                item => ReplaceLast(
                    item,
                    terminal with
                    {
                        OperationalState = terminal.OperationalState with
                        {
                            ModelInvocationCount = 1
                        }
                    })));
    }

    /// <summary>
    /// Rejects negative, gained, zero-delta, multi-delta, and contradictory instruction accounting across the
    /// activation, ordinary prefix, and throwing transition.
    /// </summary>
    [Fact]
    public void InvalidInstructionAccountingFailsWithStableCodes()
    {
        var input = CreateCertifiedFixture().Input;
        var initial = input.InitialOperationalState!;
        var first = input.Transitions[0];
        var terminal = input.Transitions[1];

        AssertFailures(
            input,
            new FailureCase(
                "negative initial budget",
                "W4.TargetException.AccountingInvalid",
                item => item with
                {
                    InitialOperationalState = initial with { Budget = new BudgetState(-1) }
                }),
            new FailureCase(
                "zero initial budget",
                "W4.TargetException.AccountingInvalid",
                item => item with
                {
                    InitialOperationalState = initial with { Budget = new BudgetState(0) }
                }),
            new FailureCase(
                "zero first delta",
                "W4.TargetException.AccountingInvalid",
                item => item with
                {
                    InitialOperationalState = initial with { Budget = new BudgetState(9) }
                }),
            new FailureCase(
                "two-unit first delta",
                "W4.TargetException.AccountingInvalid",
                item => item with
                {
                    InitialOperationalState = initial with { Budget = new BudgetState(11) }
                }),
            new FailureCase(
                "ready gained budget",
                "W4.TargetException.AccountingInvalid",
                item => ReplaceTransition(
                    item,
                    0,
                    first with
                    {
                        OperationalState = first.OperationalState with
                        {
                            Budget = new BudgetState(InitialInstructionUnits)
                        }
                    })),
            new FailureCase(
                "zero terminal delta",
                "W4.TargetException.AccountingInvalid",
                item => ReplaceLast(
                    item,
                    terminal with
                    {
                        OperationalState = terminal.OperationalState with
                        {
                            Budget = new BudgetState(9)
                        }
                    })),
            new FailureCase(
                "two-unit terminal delta",
                "W4.TargetException.AccountingInvalid",
                item => ReplaceLast(
                    item,
                    terminal with
                    {
                        OperationalState = terminal.OperationalState with
                        {
                            Budget = new BudgetState(7)
                        }
                    })),
            new FailureCase(
                "negative returned budget",
                "W4.TargetException.AccountingInvalid",
                item => ReplaceTransition(
                    ReplaceLast(
                        item,
                        terminal with
                        {
                            OperationalState = terminal.OperationalState with
                            {
                                Budget = new BudgetState(-1)
                            }
                        }),
                    0,
                    first with
                    {
                        OperationalState = first.OperationalState with
                        {
                            Budget = new BudgetState(0)
                        }
                    })));
    }

    /// <summary>
    /// Rejects default, empty, multiple, cross-method, nonzero-start, wrong-location, wrong-kind, wrong-instruction,
    /// impossible call/return, payload-bearing, and malformed terminal event records.
    /// </summary>
    [Fact]
    public void InvalidCertifiedEventTranscriptFailsWithStableCodes()
    {
        var input = CreateCertifiedFixture().Input;
        var first = input.Transitions[0];
        var terminal = input.Transitions[1];
        var ordinary = first.Events[0];
        var target = terminal.Events[0];

        AssertFailures(
            input,
            new FailureCase(
                "default ready events",
                "W4.TargetException.EventTraceInvalid",
                item => ReplaceTransition(item, 0, first with { Events = default })),
            new FailureCase(
                "empty ready events",
                "W4.TargetException.EventTraceInvalid",
                item => ReplaceTransition(item, 0, first with { Events = [] })),
            new FailureCase(
                "multiple ready events",
                "W4.TargetException.EventTraceInvalid",
                item => ReplaceTransition(
                    item,
                    0,
                    first with { Events = ImmutableArray.Create(ordinary, ordinary) })),
            new FailureCase(
                "cross-method ready event",
                "W4.TargetException.EventTraceInvalid",
                item => WithFirstEvent(
                    item,
                    CreateEvent(DebugEventKind.InstructionExecuted, OtherMethod, 0, "LoadArgument"))),
            new FailureCase(
                "nonzero-start ready event",
                "W4.TargetException.EventTraceInvalid",
                item => WithFirstEvent(
                    item,
                    CreateEvent(DebugEventKind.InstructionExecuted, Method, 1, "LoadArgument"))),
            new FailureCase(
                "wrong ready kind",
                "W4.TargetException.EventTraceInvalid",
                item => WithFirstEvent(
                    item,
                    CreateEvent(DebugEventKind.TargetExceptionRaised, Method, 0, "LoadArgument"))),
            new FailureCase(
                "wrong ready instruction",
                "W4.TargetException.EventTraceInvalid",
                item => WithFirstEvent(
                    item,
                    CreateEvent(DebugEventKind.InstructionExecuted, Method, 0, "Unknown"))),
            new FailureCase(
                "impossible call prefix",
                "W4.TargetException.EventTraceInvalid",
                item => WithFirstEvent(
                    item,
                    CreateEvent(DebugEventKind.InstructionExecuted, Method, 0, "Call"))),
            new FailureCase(
                "impossible return prefix",
                "W4.TargetException.EventTraceInvalid",
                item => WithFirstEvent(
                    item,
                    CreateEvent(DebugEventKind.InstructionExecuted, Method, 0, "Return"))),
            new FailureCase(
                "frame prefix event",
                "W4.TargetException.EventTraceInvalid",
                item => WithFirstEvent(
                    item,
                    CreateEvent(DebugEventKind.FramePushed, Method, 0, "Entry"))),
            new FailureCase(
                "payload prefix event",
                "W4.TargetException.EventTraceInvalid",
                item => WithFirstEvent(item, CreatePrecisionEvent())),
            new FailureCase(
                "default terminal events",
                "W4.TargetException.EventTraceInvalid",
                item => ReplaceLast(item, terminal with { Events = default })),
            new FailureCase(
                "empty terminal events",
                "W4.TargetException.EventTraceInvalid",
                item => ReplaceLast(item, terminal with { Events = [] })),
            new FailureCase(
                "multiple terminal events",
                "W4.TargetException.EventTraceInvalid",
                item => ReplaceLast(
                    item,
                    terminal with { Events = ImmutableArray.Create(target, target) })),
            new FailureCase(
                "wrong terminal location",
                "W4.TargetException.EventTraceInvalid",
                item => WithTerminalEvent(
                    item,
                    CreateEvent(DebugEventKind.TargetExceptionRaised, Method, 2, "LoadField"))),
            new FailureCase(
                "wrong terminal instruction",
                "W4.TargetException.EventTraceInvalid",
                item => WithTerminalEvent(
                    item,
                    CreateEvent(DebugEventKind.TargetExceptionRaised, Method, 1, "Add"))),
            new FailureCase(
                "wrong terminal kind",
                "W4.TargetException.EventTraceInvalid",
                item => WithTerminalEvent(
                    item,
                    CreateEvent(DebugEventKind.InstructionExecuted, Method, 1, "LoadField"))));
    }

    /// <summary>
    /// Rejects every non-idempotent terminal re-step mutation and more than one otherwise certified re-step while
    /// preserving the stable seven-code taxonomy.
    /// </summary>
    [Fact]
    public void TerminalRestepMutationsAreRejected()
    {
        var fixture = CreateCertifiedFixture();
        var input = fixture.Input with
        {
            Transitions = fixture.Input.Transitions.Add(fixture.Restep)
        };
        var restep = fixture.Restep;
        var terminal = fixture.Input.Transitions[^1];

        AssertFailures(
            input,
            new FailureCase(
                "nonidentical returned state",
                "W4.TargetException.LatchInvalid",
                item => ReplaceLast(item, restep with { State = restep.State with { } })),
            new FailureCase(
                "nonidentical returned operations",
                "W4.TargetException.LatchInvalid",
                item => ReplaceLast(
                    item,
                    restep with { OperationalState = restep.OperationalState with { } })),
            new FailureCase(
                "re-step consumed budget",
                "W4.TargetException.LatchInvalid",
                item => ReplaceLast(
                    item,
                    restep with
                    {
                        OperationalState = restep.OperationalState with
                        {
                            Budget = new BudgetState(RemainingInstructionUnits - 1)
                        }
                    })),
            new FailureCase(
                "re-step returned failure",
                "W4.TargetException.KindUnsupported",
                item => ReplaceLast(
                    item,
                    restep with
                    {
                        Failure = new ExecutionFailure(
                            ExecutionFailureKind.InvalidInstruction,
                            "EXEC_REPLAY_FAILURE",
                            "Ignored host prose.")
                    })),
            new FailureCase(
                "re-step emitted event",
                "W4.TargetException.EventTraceInvalid",
                item => ReplaceLast(
                    item,
                    restep with { Events = ImmutableArray.Create(terminal.Events[0]) })),
            new FailureCase(
                "re-step changed status",
                "W4.TargetException.KindUnsupported",
                item => ReplaceLast(item, restep with { Status = MachineRunStatus.Completed })),
            new FailureCase(
                "re-step changed exception code",
                "W4.TargetException.KindUnsupported",
                item => ReplaceLast(
                    item,
                    restep with
                    {
                        TargetException = new TargetExceptionInfo(
                            TargetExceptionKind.NullReference,
                            "TARGET_OTHER",
                            Method,
                            1)
                    })),
            new FailureCase(
                "uncertified equal re-step copy",
                "W4.TargetException.OutcomeRequired",
                item => ReplaceLast(item, restep with { })),
            new FailureCase(
                "equal but unissued exception instance",
                "W4.TargetException.OutcomeRequired",
                item => ReplaceLast(
                    item,
                    restep with
                    {
                        TargetException = new TargetExceptionInfo(
                            TargetExceptionKind.NullReference,
                            "TARGET_NULL_REFERENCE",
                            Method,
                            1)
                    })),
            new FailureCase(
                "second certified re-step",
                "W4.TargetException.LatchInvalid",
                item => item with
                {
                    Transitions = item.Transitions.Add(fixture.SecondRestep)
                }));
    }

    private static CertifiedFixture CreateCertifiedFixture()
    {
        var definition = new ResolvedMethodDefinition(
            Method,
            IlBody.Create(
                1,
                [
                    0x02,                       // ldarg.0
                    0x7B, 0x01, 0x00, 0x00, 0x04, // ldfld 0x04000001
                    0x2A,                       // ret
                ]),
            new MethodSignatureShape(
                DeclaringType,
                MethodCallingConventionKind.Default,
                hasImplicitThis: true,
                hasExplicitThis: false,
                genericParameterCount: 0,
                ImmutableArray<TypeSig>.Empty,
                TypeSig.Int32,
                ImmutableArray<TypeSig>.Empty));
        var domain = new ConcreteDomain();
        var machine = new IlMachine<ConcreteValue, ConcreteMemory>(
            domain,
            new FixedResolver(definition, Int32Field),
            new ConcreteMemoryModel(domain),
            new InstructionBudgetPolicy());
        var activation = machine.ActivateRoot(
            Method,
            ImmutableArray.Create(domain.ConstNull(DeclaringType)),
            ConcreteMemory.Empty);
        Assert.True(activation.IsSuccess, activation.Failure?.Code);
        var initialState = activation.State!;
        var initialOperations = new MachineOperationalState(new BudgetState(InitialInstructionUnits));

        var first = machine.StepOne(initialState, initialOperations);
        Assert.Equal(MachineRunStatus.Ready, first.Status);
        Assert.True(first.IsMachineIssuedTransitionFrom(machine, initialState, initialOperations));
        var terminal = machine.StepOne(first.State, first.OperationalState);
        Assert.Equal(MachineRunStatus.TargetException, terminal.Status);
        Assert.True(terminal.IsMachineIssuedTransitionFrom(machine, first.State, first.OperationalState));
        var restep = machine.StepOne(terminal.State, terminal.OperationalState);
        Assert.Equal(MachineRunStatus.TargetException, restep.Status);
        Assert.True(restep.IsMachineIssuedTransitionFrom(
            machine,
            terminal.State,
            terminal.OperationalState));
        var secondRestep = machine.StepOne(restep.State, restep.OperationalState);
        Assert.True(secondRestep.IsMachineIssuedTransitionFrom(
            machine,
            restep.State,
            restep.OperationalState));

        return new CertifiedFixture(
            new ProjectionInput(
                machine,
                initialState,
                initialOperations,
                ImmutableArray.Create(first, terminal)),
            restep,
            secondRestep);
    }

    private static CounterfactualTargetOutcomeProjectionResult Project(ProjectionInput input) =>
        CounterfactualTargetOutcomeProjector.Project<ConcreteValue, ConcreteMemory>(
            input.Machine!,
            input.InitialState!,
            input.InitialOperationalState!,
            input.Transitions);

    private static CounterfactualTargetOutcomeFragment AssertSuccess(
        CounterfactualTargetOutcomeProjectionResult result)
    {
        Assert.True(result.IsSuccess, result.Failure?.Code);
        Assert.Null(result.Failure);
        return Assert.IsType<CounterfactualTargetOutcomeFragment>(result.Fragment);
    }

    private static void AssertFailure(string expectedCode, ProjectionInput input)
    {
        var result = Project(input);

        Assert.False(result.IsSuccess, expectedCode);
        Assert.Null(result.Fragment);
        Assert.Equal(expectedCode, Assert.IsType<EvaluationDiagnostic>(result.Failure).Code);
    }

    private static void AssertFailures(ProjectionInput baseline, params FailureCase[] cases)
    {
        foreach (var item in cases)
        {
            var result = Project(item.Mutate(baseline));
            Assert.False(result.IsSuccess, item.Label);
            Assert.Null(result.Fragment);
            Assert.Equal(item.ExpectedCode, Assert.IsType<EvaluationDiagnostic>(result.Failure).Code);
        }
    }

    private static ProjectionInput ReplaceTransition(
        ProjectionInput input,
        int index,
        StepOutcome<ConcreteValue, ConcreteMemory> replacement)
    {
        var transitions = input.Transitions.ToBuilder();
        transitions[index] = replacement;
        return input with { Transitions = transitions.MoveToImmutable() };
    }

    private static ProjectionInput ReplaceLast(
        ProjectionInput input,
        StepOutcome<ConcreteValue, ConcreteMemory> replacement) =>
        ReplaceTransition(input, input.Transitions.Length - 1, replacement);

    private static ProjectionInput WithTerminalException(
        ProjectionInput input,
        TargetExceptionInfo exception)
    {
        var terminal = input.Transitions[^1];
        return ReplaceLast(
            input,
            terminal with
            {
                State = terminal.State with { TerminalTargetException = exception },
                TargetException = exception
            });
    }

    private static ProjectionInput WithFirstEvent(ProjectionInput input, DebugEvent item) =>
        ReplaceTransition(
            input,
            0,
            input.Transitions[0] with { Events = ImmutableArray.Create(item) });

    private static ProjectionInput WithTerminalEvent(ProjectionInput input, DebugEvent item) =>
        ReplaceLast(
            input,
            input.Transitions[^1] with { Events = ImmutableArray.Create(item) });

    private static StepOutcome<ConcreteValue, ConcreteMemory> CloneOutcome(
        StepOutcome<ConcreteValue, ConcreteMemory> item) =>
        new(
            item.State,
            item.OperationalState,
            item.Status,
            item.Events,
            item.Failure,
            item.TargetException);

    private static TargetExceptionInfo AtLocation(
        TargetExceptionInfo _,
        MethodHandle method,
        int ilOffset) =>
        new(TargetExceptionKind.NullReference, "TARGET_NULL_REFERENCE", method, ilOffset);

    private static TargetExceptionInfo CreateMalformedException(
        TargetExceptionKind kind,
        string code,
        MethodHandle method,
        int ilOffset)
    {
        var value = (TargetExceptionInfo)RuntimeHelpers.GetUninitializedObject(typeof(TargetExceptionInfo));
        SetAutoProperty(value, nameof(TargetExceptionInfo.Kind), kind);
        SetAutoProperty(value, nameof(TargetExceptionInfo.Code), code);
        SetAutoProperty(value, nameof(TargetExceptionInfo.Method), (MethodHandle?)method);
        SetAutoProperty(value, nameof(TargetExceptionInfo.IlOffset), (int?)ilOffset);
        return value;
    }

    private static void SetAutoProperty<T>(object instance, string propertyName, T value)
    {
        var field = instance.GetType().GetField(
            $"<{propertyName}>k__BackingField",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field.SetValue(instance, value);
    }

    private static DebugEvent CreateEvent(
        DebugEventKind kind,
        MethodHandle method,
        int ilOffset,
        string instruction) =>
        new(kind, method, ilOffset, instruction);

    private static DebugEvent CreatePrecisionEvent()
    {
        var evidence = new FieldLoadEvidence(
            0,
            Int32Field,
            EvaluationEvidenceStatus.Partial,
            "W4.Field.Partial",
            new string('1', 64),
            new string('2', 64),
            0x1000,
            sizeof(int),
            [0x2A]);
        return new DebugEvent(
            DebugEventKind.ValuePrecisionLost,
            Method,
            0,
            "LoadField",
            evidence);
    }

    private static void AssertEvent(
        DebugEvent item,
        DebugEventKind kind,
        MethodHandle method,
        int ilOffset,
        string instruction)
    {
        Assert.Equal(kind, item.Kind);
        Assert.Equal(method, item.Method);
        Assert.Equal(ilOffset, item.IlOffset);
        Assert.Equal(instruction, item.Instruction);
        Assert.Null(item.FieldEvidence);
    }

    private sealed record ProjectionInput(
        IlMachine<ConcreteValue, ConcreteMemory>? Machine,
        MachineState<ConcreteValue, ConcreteMemory>? InitialState,
        MachineOperationalState? InitialOperationalState,
        ImmutableArray<StepOutcome<ConcreteValue, ConcreteMemory>> Transitions);

    private sealed record CertifiedFixture(
        ProjectionInput Input,
        StepOutcome<ConcreteValue, ConcreteMemory> Restep,
        StepOutcome<ConcreteValue, ConcreteMemory> SecondRestep);

    private sealed record FailureCase(
        string Label,
        string ExpectedCode,
        Func<ProjectionInput, ProjectionInput> Mutate);

    private sealed class FixedResolver : IResolutionServices
    {
        private readonly ResolvedMethodDefinition definition;
        private readonly ResolvedField field;

        internal FixedResolver(ResolvedMethodDefinition definition, ResolvedField field)
        {
            this.definition = definition;
            this.field = field;
        }

        public ResolutionResult<ResolvedMethodDefinition> GetMethodDefinition(MethodHandle method) =>
            method == definition.Method
                ? ResolutionResult<ResolvedMethodDefinition>.Success(definition)
                : ResolutionResult<ResolvedMethodDefinition>.Failed(
                    ResolutionFailureKind.Invalid,
                    "TEST_METHOD_MISMATCH",
                    "The synthetic resolver received another method.");

        public ResolutionResult<ResolvedMethodCallTarget> ResolveMethod(
            MethodHandle contextMethod,
            int metadataToken) =>
            ResolutionResult<ResolvedMethodCallTarget>.Failed(
                ResolutionFailureKind.Unsupported,
                "TEST_CALL_UNSUPPORTED",
                "The certified fixture has no direct call.");

        public ResolutionResult<ResolvedField> ResolveField(
            MethodHandle contextMethod,
            int metadataToken) =>
            contextMethod == definition.Method && metadataToken == field.Handle.MetadataToken
                ? ResolutionResult<ResolvedField>.Success(field)
                : ResolutionResult<ResolvedField>.Failed(
                    ResolutionFailureKind.Invalid,
                    "TEST_FIELD_MISMATCH",
                    "The synthetic resolver received another field.");
    }
}
