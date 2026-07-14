namespace Interpreter.Core.Abstractions;

/// <summary>
/// Provides the one metadata capability exercised by the current semantics slice: explainable method-body lookup.
/// </summary>
/// <remarks>
/// Token/signature/dispatch capabilities will be introduced only when an executable scenario requires them. This
/// contract remains intentionally narrow so an SRM backend does not advertise dozens of operations that merely
/// return "unsupported." A machine snapshots the first result observed for each <see cref="MethodHandle"/> so a
/// mutable resolver cannot change the body halfway through one deterministic execution session.
/// </remarks>
public interface IResolutionServices
{
    /// <summary>Retrieves a method body for interpretation.</summary>
    /// <param name="method">The deterministic method-definition handle.</param>
    /// <returns>
    /// The method body or a structured unavailable/unsupported/invalid result. Implementations should be stable
    /// for one analysis snapshot; <c>IlMachine</c> additionally caches the first observed result defensively.
    /// </returns>
    ResolutionResult<MethodBody> GetMethodBody(MethodHandle method);
}
