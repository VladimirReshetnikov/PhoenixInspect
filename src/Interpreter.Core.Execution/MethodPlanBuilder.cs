using System.Collections.Immutable;
using Interpreter.Core.Abstractions;

namespace Interpreter.Core.Execution;

internal static class MethodPlanBuilder
{
    private const int MaxAdmittedCodeBytes = 4096;
    private const int MaxAdmittedInstructions = 1024;

    internal static PlanPreparationResult Build(
        ResolvedMethodDefinition definition,
        IResolutionServices resolver)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(resolver);

        var method = definition.Method;
        var body = definition.Body;
        var signature = definition.Signature;
        if (body is null || signature is null)
        {
            return Reject(
                MachineRunStatus.InvalidProgram,
                ExecutionFailureKind.DependencyResolution,
                "EXEC_INVALID_METHOD_DEFINITION",
                "Resolved method definition is missing its body or signature shape.",
                method,
                0);
        }

        var signatureFailure = ValidateSignature(definition);
        if (signatureFailure is not null)
        {
            return signatureFailure.Value;
        }

        if (body.ExceptionRegionCount != 0)
        {
            return body.ExceptionRegionCount < 0
                ? Reject(
                    MachineRunStatus.InvalidProgram,
                    ExecutionFailureKind.InvalidInstruction,
                    "EXEC_INVALID_EH_COUNT",
                    "The method declares a negative exception-region count.",
                    method,
                    0)
                : Reject(
                    MachineRunStatus.Blocked,
                    ExecutionFailureKind.UnsupportedInstruction,
                    "EXEC_EH_UNSUPPORTED",
                    "Exception regions are outside the branchless W3 execution profile.",
                    method,
                    0);
        }

        if (body.CodeBytes.IsDefault)
        {
            return Reject(
                MachineRunStatus.InvalidProgram,
                ExecutionFailureKind.InvalidInstruction,
                "EXEC_INVALID_CODE_BUFFER",
                "The resolved method contains an uninitialized IL byte array.",
                method,
                0);
        }

        if (body.CodeBytes.Length > MaxAdmittedCodeBytes)
        {
            return Reject(
                MachineRunStatus.Blocked,
                ExecutionFailureKind.ResourceLimit,
                "EXEC_BODY_TOO_LARGE",
                $"Method body length exceeds the bounded limit of {MaxAdmittedCodeBytes} bytes.",
                method,
                0);
        }

        if (body.MaxStack < 0)
        {
            return Reject(
                MachineRunStatus.InvalidProgram,
                ExecutionFailureKind.InvalidStack,
                "EXEC_INVALID_MAXSTACK",
                "The resolved method declares a negative maximum stack depth.",
                method,
                0);
        }

        if (body.MaxStack > ExecutionLimits.MaximumFrameSlotCount)
        {
            return Reject(
                MachineRunStatus.Blocked,
                ExecutionFailureKind.ResourceLimit,
                "EXEC_MAXSTACK_LIMIT",
                $"Declared maximum stack depth is limited to {ExecutionLimits.MaximumFrameSlotCount} entries.",
                method,
                0);
        }

        if (body.HasLocalSignature && !IsValidStandaloneSignatureToken(body.LocalSignatureToken))
        {
            return Reject(
                MachineRunStatus.InvalidProgram,
                ExecutionFailureKind.InvalidInstruction,
                "EXEC_INVALID_LOCAL_SIGNATURE",
                "The method body carries an invalid StandAloneSig token.",
                method,
                0);
        }

        if (!body.HasLocalSignature && signature.LocalTypes.Length != 0)
        {
            return Reject(
                MachineRunStatus.InvalidProgram,
                ExecutionFailureKind.DependencyResolution,
                "EXEC_LOCAL_LAYOUT_CONFLICT",
                "Resolved local types disagree with the method body's absent local signature.",
                method,
                0);
        }

        if (signature.LocalTypes.Length > 0 && !body.LocalVariablesInitialized)
        {
            return Reject(
                MachineRunStatus.Blocked,
                ExecutionFailureKind.UnsupportedInstruction,
                "EXEC_UNINITIALIZED_LOCALS_UNSUPPORTED",
                "W3 admits local variables only when the method requests CLI default initialization.",
                method,
                0);
        }

        var argumentTypes = signature.HasImplicitThis
            ? ImmutableArray.Create(signature.DeclaringType).AddRange(signature.ParameterTypes)
            : signature.ParameterTypes;
        if (argumentTypes.Length > ExecutionLimits.MaximumFrameSlotCount ||
            signature.LocalTypes.Length > ExecutionLimits.MaximumFrameSlotCount)
        {
            return Reject(
                MachineRunStatus.Blocked,
                ExecutionFailureKind.ResourceLimit,
                "EXEC_FRAME_SLOT_LIMIT",
                $"Argument and local vectors are each limited to {ExecutionLimits.MaximumFrameSlotCount} entries.",
                method,
                0);
        }

        var offset = 0;
        var instructionCount = 0;
        var fieldInstructionCount = 0;
        var sawReturn = false;
        var stack = new List<TypeSig>();
        var boundaries = ImmutableArray.CreateBuilder<MethodInstructionBoundary>();
        var instructions = ImmutableArray.CreateBuilder<AdmittedInstruction>();

        while (offset < body.CodeBytes.Length)
        {
            if (instructionCount >= MaxAdmittedInstructions)
            {
                return Reject(
                    MachineRunStatus.Blocked,
                    ExecutionFailureKind.ResourceLimit,
                    "EXEC_TOO_MANY_INSTRUCTIONS",
                    $"Method body exceeds the bounded limit of {MaxAdmittedInstructions} instructions.",
                    method,
                    offset);
            }

            boundaries.Add(new MethodInstructionBoundary(offset, stack.ToImmutableArray()));
            var decode = Decode(body.CodeBytes, offset, method);
            if (!decode.IsSuccess)
            {
                return PlanPreparationResult.Failed(decode.Status, decode.Failure!);
            }

            var instruction = decode.Instruction;
            switch (instruction.Kind)
            {
                case AdmittedInstructionKind.Nop:
                    break;

                case AdmittedInstructionKind.LoadArgument:
                    if ((uint)instruction.Operand >= (uint)argumentTypes.Length)
                    {
                        return InvalidSlot(method, offset, "argument", instruction.Operand, argumentTypes.Length);
                    }

                    stack.Add(argumentTypes[instruction.Operand]);
                    break;

                case AdmittedInstructionKind.LoadLocal:
                    if ((uint)instruction.Operand >= (uint)signature.LocalTypes.Length)
                    {
                        return InvalidSlot(method, offset, "local", instruction.Operand, signature.LocalTypes.Length);
                    }

                    stack.Add(signature.LocalTypes[instruction.Operand]);
                    break;

                case AdmittedInstructionKind.StoreLocal:
                    if ((uint)instruction.Operand >= (uint)signature.LocalTypes.Length)
                    {
                        return InvalidSlot(method, offset, "local", instruction.Operand, signature.LocalTypes.Length);
                    }

                    if (!TryPopExpected(stack, signature.LocalTypes[instruction.Operand]))
                    {
                        return InvalidStack(
                            method,
                            offset,
                            "stloc requires one value whose structural type equals the metadata-projected local type.");
                    }

                    break;

                case AdmittedInstructionKind.LoadInt32:
                    stack.Add(TypeSig.Int32);
                    break;

                case AdmittedInstructionKind.Add or
                     AdmittedInstructionKind.Subtract or
                     AdmittedInstructionKind.Multiply:
                    if (!TryPopExpected(stack, TypeSig.Int32) || !TryPopExpected(stack, TypeSig.Int32))
                    {
                        return InvalidStack(method, offset, $"{instruction.Kind} requires two exact Int32 values.");
                    }

                    stack.Add(TypeSig.Int32);
                    break;

                case AdmittedInstructionKind.LoadField:
                    fieldInstructionCount++;
                    if (fieldInstructionCount > 1)
                    {
                        return Reject(
                            MachineRunStatus.Blocked,
                            ExecutionFailureKind.UnsupportedInstruction,
                            "EXEC_MULTIPLE_FIELD_LOADS_UNSUPPORTED",
                            "The closed W3 E2 profile admits exactly one field load in a getter body.",
                            method,
                            offset);
                    }

                    var fieldResult = resolver.ResolveField(method, instruction.Operand);
                    if (!fieldResult.IsSuccess)
                    {
                        var failure = fieldResult.Failure ?? new ResolutionFailure(
                            ResolutionFailureKind.Invalid,
                            "RESOLUTION_INVALID_RESULT",
                            "Field resolver returned an invalid default result.");
                        var sanitizedFailure = ResolutionFailureDiagnostics.Sanitize(failure);
                        return PlanPreparationResult.Failed(
                            failure.Kind == ResolutionFailureKind.Invalid
                                ? MachineRunStatus.InvalidProgram
                                : MachineRunStatus.Blocked,
                            new ExecutionFailure(
                                ExecutionFailureKind.DependencyResolution,
                                failure.Code,
                                "Field resolution did not produce an executable descriptor.",
                                method,
                                offset,
                                sanitizedFailure));
                    }

                    var field = fieldResult.Value;
                    var fieldFailure = ValidateField(method, offset, instruction.Operand, field);
                    if (fieldFailure is not null)
                    {
                        return fieldFailure.Value;
                    }

                    if (field.DeclaringType != signature.DeclaringType)
                    {
                        return Reject(
                            MachineRunStatus.Blocked,
                            ExecutionFailureKind.UnsupportedInstruction,
                            "EXEC_FIELD_OWNER_UNSUPPORTED",
                            "The W3 getter field must be declared directly by the method's exact receiver TypeDef.",
                            method,
                            offset);
                    }

                    if (!TryPopExpected(stack, field.DeclaringType))
                    {
                        return InvalidStack(
                            method,
                            offset,
                            "ldfld requires one exact receiver value of the field's declaring TypeDef.");
                    }

                    stack.Add(field.FieldType);
                    instruction = instruction with { Field = field };
                    break;

                case AdmittedInstructionKind.Return:
                    if (Equals(signature.ReturnType, TypeSig.Void))
                    {
                        if (stack.Count != 0)
                        {
                            return InvalidStack(method, offset, "A void return requires an empty evaluation stack.");
                        }
                    }
                    else if (stack.Count != 1 || !Equals(stack[0], signature.ReturnType))
                    {
                        return InvalidStack(
                            method,
                            offset,
                            "A value return requires exactly the metadata-projected return type.");
                    }

                    if (offset + instruction.Size != body.CodeBytes.Length)
                    {
                        return Reject(
                            MachineRunStatus.InvalidProgram,
                            ExecutionFailureKind.InvalidInstruction,
                            "EXEC_CODE_AFTER_RETURN",
                            "The straight-line W3 profile does not admit instructions after ret.",
                            method,
                            offset);
                    }

                    sawReturn = true;
                    break;

                default:
                    throw new InvalidOperationException($"Decoder produced unknown instruction kind {instruction.Kind}.");
            }

            if (stack.Count > body.MaxStack)
            {
                return Reject(
                    MachineRunStatus.InvalidProgram,
                    ExecutionFailureKind.InvalidStack,
                    "EXEC_MAXSTACK_EXCEEDED",
                    "Typed admission exceeds the method's declared maximum evaluation-stack depth.",
                    method,
                    offset);
            }

            instructions.Add(instruction);
            instructionCount++;
            offset = checked(offset + instruction.Size);
        }

        if (!sawReturn)
        {
            return Reject(
                MachineRunStatus.InvalidProgram,
                ExecutionFailureKind.InvalidInstruction,
                "EXEC_MISSING_RETURN",
                "The straight-line method body does not terminate in ret.",
                method,
                body.CodeBytes.Length);
        }

        var profileFailure = ValidateClosedProfile(definition, instructions, fieldInstructionCount);
        if (profileFailure is not null)
        {
            return profileFailure.Value;
        }

        var admission = new MethodAdmissionResult(
            true,
            instructionCount,
            boundaries.ToImmutable(),
            MachineRunStatus.Ready,
            null);
        return PlanPreparationResult.Success(
            new AdmittedMethodPlan(
                definition,
                argumentTypes,
                instructions.ToImmutable(),
                admission));
    }

    private static PlanPreparationResult? ValidateClosedProfile(
        ResolvedMethodDefinition definition,
        ImmutableArray<AdmittedInstruction>.Builder instructions,
        int fieldInstructionCount)
    {
        var method = definition.Method;
        var signature = definition.Signature;
        if (fieldInstructionCount == 0)
        {
            return signature.HasImplicitThis
                ? Reject(
                    MachineRunStatus.Blocked,
                    ExecutionFailureKind.UnsupportedInstruction,
                    "EXEC_INSTANCE_PROFILE_UNSUPPORTED",
                    "W3 instance methods are limited to the closed E2 field-getter profile.",
                    method,
                    0)
                : null;
        }

        if (!signature.HasImplicitThis ||
            signature.ParameterTypes.Length != 0 ||
            signature.LocalTypes.Length != 0 ||
            signature.ReturnType != TypeSig.Int32)
        {
            return Reject(
                MachineRunStatus.Blocked,
                ExecutionFailureKind.UnsupportedInstruction,
                "EXEC_FIELD_GETTER_SIGNATURE_UNSUPPORTED",
                "The W3 E2 getter requires one implicit receiver, no parameters or locals, and an Int32 return.",
                method,
                0);
        }

        var direct = instructions.Count == 3 &&
            IsInstruction(instructions[0], AdmittedInstructionKind.LoadArgument, operand: 0) &&
            instructions[1].Kind == AdmittedInstructionKind.LoadField &&
            instructions[2].Kind == AdmittedInstructionKind.Return;
        var adjusted = instructions.Count == 5 &&
            IsInstruction(instructions[0], AdmittedInstructionKind.LoadArgument, operand: 0) &&
            instructions[1].Kind == AdmittedInstructionKind.LoadField &&
            instructions[2].Kind == AdmittedInstructionKind.LoadInt32 &&
            (instructions[3].Kind is
                AdmittedInstructionKind.Add or
                AdmittedInstructionKind.Subtract or
                AdmittedInstructionKind.Multiply) &&
            instructions[4].Kind == AdmittedInstructionKind.Return;
        return direct || adjusted
            ? null
            : Reject(
                MachineRunStatus.Blocked,
                ExecutionFailureKind.UnsupportedInstruction,
                "EXEC_FIELD_GETTER_SHAPE_UNSUPPORTED",
                "The W3 E2 body must be the direct getter or one constant-adjusted arithmetic getter shape.",
                method,
                0);
    }

    private static bool IsInstruction(
        AdmittedInstruction instruction,
        AdmittedInstructionKind kind,
        int operand) =>
        instruction.Kind == kind && instruction.Operand == operand;

    private static PlanPreparationResult? ValidateSignature(ResolvedMethodDefinition definition)
    {
        var method = definition.Method;
        var signature = definition.Signature;
        if (signature.ParameterTypes.IsDefault || signature.LocalTypes.IsDefault)
        {
            return Reject(
                MachineRunStatus.InvalidProgram,
                ExecutionFailureKind.DependencyResolution,
                "EXEC_INVALID_SIGNATURE_SHAPE",
                "Resolved method signature contains an uninitialized type vector.",
                method,
                0);
        }

        if (signature.CallingConvention != MethodCallingConventionKind.Default ||
            signature.HasExplicitThis ||
            signature.GenericParameterCount != 0)
        {
            return Reject(
                MachineRunStatus.Blocked,
                ExecutionFailureKind.UnsupportedInstruction,
                "EXEC_SIGNATURE_UNSUPPORTED",
                "The resolved calling convention or generic shape is outside the W3 profile.",
                method,
                0);
        }

        if (signature.HasImplicitThis && !signature.DeclaringType.IsMetadataTypeDefinition)
        {
            return Reject(
                MachineRunStatus.Blocked,
                ExecutionFailureKind.UnsupportedInstruction,
                "EXEC_RECEIVER_TYPE_UNSUPPORTED",
                "An admitted instance method requires an exact metadata TypeDef receiver.",
                method,
                0);
        }

        foreach (var parameter in signature.ParameterTypes)
        {
            if (!Equals(parameter, TypeSig.Int32))
            {
                return UnsupportedType(method, "parameter");
            }
        }

        foreach (var local in signature.LocalTypes)
        {
            if (!Equals(local, TypeSig.Int32))
            {
                return UnsupportedType(method, "local");
            }
        }

        if (!Equals(signature.ReturnType, TypeSig.Void) && !Equals(signature.ReturnType, TypeSig.Int32))
        {
            return UnsupportedType(method, "return");
        }

        return null;
    }

    private static PlanPreparationResult? ValidateField(
        MethodHandle method,
        int offset,
        int rawToken,
        ResolvedField field)
    {
        if (field is null ||
            field.Handle.Module != method.Module ||
            field.Handle.MetadataToken != rawToken)
        {
            return Reject(
                MachineRunStatus.InvalidProgram,
                ExecutionFailureKind.DependencyResolution,
                "EXEC_FIELD_IDENTITY_CONFLICT",
                "Resolved field identity does not match the same-module InlineField operand.",
                method,
                offset);
        }

        if (field.IsStatic || field.IsLiteral || field.HasRva)
        {
            return Reject(
                MachineRunStatus.Blocked,
                ExecutionFailureKind.UnsupportedInstruction,
                "EXEC_FIELD_STORAGE_UNSUPPORTED",
                "W3 admits only ordinary instance fields without literal or RVA storage.",
                method,
                offset);
        }

        if (!field.DeclaringType.IsMetadataTypeDefinition || !Equals(field.FieldType, TypeSig.Int32))
        {
            return Reject(
                MachineRunStatus.Blocked,
                ExecutionFailureKind.UnsupportedInstruction,
                "EXEC_FIELD_TYPE_UNSUPPORTED",
                "W3 admits only exact Int32 fields declared by an exact TypeDef.",
                method,
                offset);
        }

        return null;
    }

    private static bool TryPopExpected(List<TypeSig> stack, TypeSig expected)
    {
        if (stack.Count == 0 || !Equals(stack[^1], expected))
        {
            return false;
        }

        stack.RemoveAt(stack.Count - 1);
        return true;
    }

    private static PlanPreparationResult InvalidSlot(
        MethodHandle method,
        int offset,
        string slotKind,
        int slot,
        int count) =>
        Reject(
            MachineRunStatus.InvalidProgram,
            ExecutionFailureKind.InvalidSlot,
            "EXEC_INVALID_SLOT",
            $"Instruction references {slotKind} slot {slot}, but metadata projects {count} {slotKind} slot(s).",
            method,
            offset);

    private static PlanPreparationResult InvalidStack(MethodHandle method, int offset, string message) =>
        Reject(
            MachineRunStatus.InvalidProgram,
            ExecutionFailureKind.InvalidStack,
            "EXEC_INVALID_STACK",
            message,
            method,
            offset);

    private static PlanPreparationResult UnsupportedType(MethodHandle method, string slotKind) =>
        Reject(
            MachineRunStatus.Blocked,
            ExecutionFailureKind.UnsupportedInstruction,
            "EXEC_SIGNATURE_TYPE_UNSUPPORTED",
            $"The metadata-projected {slotKind} type is outside the exact W3 type closure.",
            method,
            0);

    private static PlanPreparationResult Reject(
        MachineRunStatus status,
        ExecutionFailureKind kind,
        string code,
        string message,
        MethodHandle method,
        int offset) =>
        PlanPreparationResult.Failed(
            status,
            new ExecutionFailure(kind, code, message, method, offset));

    private static bool IsValidStandaloneSignatureToken(int token) =>
        (token & unchecked((int)0xFF000000)) == 0x11000000 &&
        (token & 0x00FFFFFF) != 0;

    private static DecodeResult Decode(ImmutableArray<byte> code, int offset, MethodHandle method)
    {
        if ((uint)offset >= (uint)code.Length)
        {
            return InvalidDecode(
                method,
                offset,
                "EXEC_INVALID_IL_OFFSET",
                "The current IL offset is outside the resolved method body.");
        }

        var opcode = code[offset];
        return opcode switch
        {
            0x00 => Success(AdmittedInstructionKind.Nop),
            >= 0x02 and <= 0x05 => Success(AdmittedInstructionKind.LoadArgument, opcode - 0x02),
            >= 0x06 and <= 0x09 => Success(AdmittedInstructionKind.LoadLocal, opcode - 0x06),
            >= 0x0A and <= 0x0D => Success(AdmittedInstructionKind.StoreLocal, opcode - 0x0A),
            0x0E => OneByteOperand(AdmittedInstructionKind.LoadArgument),
            0x11 => OneByteOperand(AdmittedInstructionKind.LoadLocal),
            0x13 => OneByteOperand(AdmittedInstructionKind.StoreLocal),
            0x15 => Success(AdmittedInstructionKind.LoadInt32, -1),
            >= 0x16 and <= 0x1E => Success(AdmittedInstructionKind.LoadInt32, opcode - 0x16),
            0x1F => OneByteOperand(AdmittedInstructionKind.LoadInt32, signed: true),
            0x20 => FourByteOperand(AdmittedInstructionKind.LoadInt32),
            0x2A => Success(AdmittedInstructionKind.Return),
            0x58 => Success(AdmittedInstructionKind.Add),
            0x59 => Success(AdmittedInstructionKind.Subtract),
            0x5A => Success(AdmittedInstructionKind.Multiply),
            0x7B => FourByteOperand(AdmittedInstructionKind.LoadField),
            0xFE => TwoByteInstruction(),
            _ => new DecodeResult(
                false,
                default,
                MachineRunStatus.Blocked,
                new ExecutionFailure(
                    ExecutionFailureKind.UnsupportedInstruction,
                    "EXEC_UNSUPPORTED_OPCODE",
                    $"Opcode 0x{opcode:X2} is outside the admitted W3 profile.",
                    method,
                    offset)),
        };

        DecodeResult Success(AdmittedInstructionKind kind, int operand = 0) =>
            new(true, new AdmittedInstruction(offset, kind, operand, 1), MachineRunStatus.Ready, null);

        DecodeResult OneByteOperand(AdmittedInstructionKind kind, bool signed = false)
        {
            if (offset + 1 >= code.Length)
            {
                return InvalidDecode(
                    method,
                    offset,
                    "EXEC_TRUNCATED_INSTRUCTION",
                    $"Opcode 0x{opcode:X2} is missing its one-byte operand.");
            }

            var operand = signed ? unchecked((int)(sbyte)code[offset + 1]) : code[offset + 1];
            return new DecodeResult(
                true,
                new AdmittedInstruction(offset, kind, operand, 2),
                MachineRunStatus.Ready,
                null);
        }

        DecodeResult FourByteOperand(AdmittedInstructionKind kind)
        {
            if (offset > code.Length - 5)
            {
                return InvalidDecode(
                    method,
                    offset,
                    "EXEC_TRUNCATED_INSTRUCTION",
                    $"Opcode 0x{opcode:X2} is missing its four-byte operand.");
            }

            var operand = code[offset + 1]
                | (code[offset + 2] << 8)
                | (code[offset + 3] << 16)
                | (code[offset + 4] << 24);
            return new DecodeResult(
                true,
                new AdmittedInstruction(offset, kind, operand, 5),
                MachineRunStatus.Ready,
                null);
        }

        DecodeResult TwoByteInstruction()
        {
            if (offset + 1 >= code.Length)
            {
                return InvalidDecode(
                    method,
                    offset,
                    "EXEC_TRUNCATED_INSTRUCTION",
                    "The two-byte opcode prefix is missing its second opcode byte.");
            }

            var secondOpcode = code[offset + 1];
            var kind = secondOpcode switch
            {
                0x09 => AdmittedInstructionKind.LoadArgument,
                0x0C => AdmittedInstructionKind.LoadLocal,
                0x0E => AdmittedInstructionKind.StoreLocal,
                _ => (AdmittedInstructionKind?)null,
            };
            if (kind is null)
            {
                return new DecodeResult(
                    false,
                    default,
                    MachineRunStatus.Blocked,
                    new ExecutionFailure(
                        ExecutionFailureKind.UnsupportedInstruction,
                        "EXEC_UNSUPPORTED_OPCODE",
                        $"Opcode 0xFE{secondOpcode:X2} is outside the admitted W3 profile.",
                        method,
                        offset));
            }

            if (offset > code.Length - 4)
            {
                return InvalidDecode(
                    method,
                    offset,
                    "EXEC_TRUNCATED_INSTRUCTION",
                    $"Opcode 0xFE{secondOpcode:X2} is missing its two-byte slot operand.");
            }

            var operand = code[offset + 2] | (code[offset + 3] << 8);
            return new DecodeResult(
                true,
                new AdmittedInstruction(offset, kind.Value, operand, 4),
                MachineRunStatus.Ready,
                null);
        }
    }

    private static DecodeResult InvalidDecode(
        MethodHandle method,
        int offset,
        string code,
        string message) =>
        new(
            false,
            default,
            MachineRunStatus.InvalidProgram,
            new ExecutionFailure(
                ExecutionFailureKind.InvalidInstruction,
                code,
                message,
                method,
                offset));

    private readonly record struct DecodeResult(
        bool IsSuccess,
        AdmittedInstruction Instruction,
        MachineRunStatus Status,
        ExecutionFailure? Failure);

}

internal static class ExecutionLimits
{
    internal const int MaximumFrameSlotCount = 1024;
}
