using Interpreter.Types;

namespace Interpreter.Core.Abstractions;

/// <summary>
/// Represents a resolved call-site envelope provided to call models.
/// </summary>
/// <param name="Kind">IL call instruction category.</param>
/// <param name="Module">Calling module handle for provenance.</param>
/// <param name="MethodToken">Original metadata token operand.</param>
/// <param name="Target">Resolved call target metadata.</param>
/// <param name="ConstrainedType">Optional constrained type for <c>constrained.</c> callvirt forms.</param>
public readonly record struct CallSite(
    CallKind Kind,
    ModuleHandle Module,
    int MethodToken,
    ResolvedMethod Target,
    TypeSig? ConstrainedType = null);
