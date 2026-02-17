using Interpreter.IL;
using Interpreter.Types;

namespace Interpreter.Core.Abstractions;

/// <summary>
/// Attempts to model a method call as a semantic intrinsic or lifted behavior.
/// </summary>
/// <typeparam name="TValue">Value-domain representation type.</typeparam>
/// <typeparam name="TMem">Memory-state representation type.</typeparam>
public interface ICallModel<TValue, TMem>
{
    /// <summary>
    /// Attempts to model a call-site invocation.
    /// </summary>
    /// <param name="ctx">Call-model evaluation context.</param>
    /// <returns>A modeled outcome or <see cref="CallOutcomeKind.NotHandled"/> when the model does not apply.</returns>
    CallOutcome<TValue, TMem> TryCall(CallModelContext<TValue, TMem> ctx);
}
