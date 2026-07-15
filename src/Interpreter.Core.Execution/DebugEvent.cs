using Interpreter.Core.Abstractions;

namespace Interpreter.Core.Execution;

/// <summary>
/// Classifies deterministic events emitted after successful machine-state transfers.
/// </summary>
public enum DebugEventKind
{
    /// <summary>An IL instruction was fully decoded and its semantic transfer completed.</summary>
    InstructionExecuted,

    /// <summary>A <c>ret</c> transfer removed an activation frame.</summary>
    FramePopped,

    /// <summary>An admitted instruction terminated at a modeled target-exception boundary.</summary>
    TargetExceptionRaised,

    /// <summary>
    /// An admitted instruction completed by producing an explained unknown from canonical non-exact evidence.
    /// </summary>
    ValuePrecisionLost,
}

/// <summary>
/// Describes one deterministic event from a transfer that actually occurred.
/// </summary>
/// <remarks>
/// Human-readable failure messages are not represented as instruction events. Decode, evidence, and validation
/// failures belong in <see cref="ExecutionFailure"/> so traces never claim an instruction ran when it did not.
/// <see cref="DebugEventKind.TargetExceptionRaised"/> records an attempted admitted instruction that terminated;
/// it is emitted instead of <see cref="DebugEventKind.InstructionExecuted"/> because no ordinary transfer completed.
/// A successful approximate field load emits <see cref="DebugEventKind.InstructionExecuted"/> first and then
/// <see cref="DebugEventKind.ValuePrecisionLost"/>, both at the field-load instruction's original location.
/// </remarks>
public sealed record DebugEvent
{
    /// <summary>Creates one structurally valid deterministic execution event.</summary>
    /// <param name="kind">The stable semantic event category.</param>
    /// <param name="method">The method in which the transfer occurred.</param>
    /// <param name="ilOffset">The offset of the instruction responsible for the event.</param>
    /// <param name="instruction">A stable instruction-family name for diagnostics and smoke fingerprints.</param>
    /// <param name="fieldEvidence">
    /// Canonical partial or unavailable field evidence exactly for
    /// <see cref="DebugEventKind.ValuePrecisionLost"/>; otherwise <see langword="null"/>.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="kind"/> is undefined.</exception>
    /// <exception cref="ArgumentException">
    /// A precision-loss event has no field evidence, or another event kind carries field evidence.
    /// </exception>
    public DebugEvent(
        DebugEventKind kind,
        MethodHandle method,
        int ilOffset,
        string instruction,
        FieldLoadEvidence? fieldEvidence = null)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }

        if ((kind == DebugEventKind.ValuePrecisionLost) != (fieldEvidence is not null))
        {
            throw new ArgumentException(
                "Field evidence is required exactly for a value-precision-loss event.",
                nameof(fieldEvidence));
        }

        Kind = kind;
        Method = method;
        IlOffset = ilOffset;
        Instruction = instruction;
        FieldEvidence = fieldEvidence;
    }

    /// <summary>Gets the stable semantic event category.</summary>
    public DebugEventKind Kind { get; }

    /// <summary>Gets the method in which the transfer occurred.</summary>
    public MethodHandle Method { get; }

    /// <summary>Gets the offset of the instruction responsible for the event.</summary>
    public int IlOffset { get; }

    /// <summary>Gets the stable instruction-family name used by diagnostics and smoke fingerprints.</summary>
    public string Instruction { get; }

    /// <summary>
    /// Gets canonical partial or unavailable field evidence exactly for
    /// <see cref="DebugEventKind.ValuePrecisionLost"/>; otherwise <see langword="null"/>.
    /// </summary>
    public FieldLoadEvidence? FieldEvidence { get; }

    /// <summary>
    /// Deconstructs the original four event axes so source written before the optional evidence payload remains valid.
    /// </summary>
    /// <param name="kind">Receives the stable semantic event category.</param>
    /// <param name="method">Receives the method in which the event occurred.</param>
    /// <param name="ilOffset">Receives the responsible instruction offset.</param>
    /// <param name="instruction">Receives the stable instruction-family name.</param>
    public void Deconstruct(
        out DebugEventKind kind,
        out MethodHandle method,
        out int ilOffset,
        out string instruction)
    {
        kind = Kind;
        method = Method;
        ilOffset = IlOffset;
        instruction = Instruction;
    }

    /// <summary>Deconstructs every event axis, including optional field evidence.</summary>
    /// <param name="kind">Receives the stable semantic event category.</param>
    /// <param name="method">Receives the method in which the event occurred.</param>
    /// <param name="ilOffset">Receives the responsible instruction offset.</param>
    /// <param name="instruction">Receives the stable instruction-family name.</param>
    /// <param name="fieldEvidence">Receives precision-loss field evidence, or <see langword="null"/>.</param>
    public void Deconstruct(
        out DebugEventKind kind,
        out MethodHandle method,
        out int ilOffset,
        out string instruction,
        out FieldLoadEvidence? fieldEvidence)
    {
        kind = Kind;
        method = Method;
        ilOffset = IlOffset;
        instruction = Instruction;
        fieldEvidence = FieldEvidence;
    }
}
