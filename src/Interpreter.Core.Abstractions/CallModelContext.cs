using Interpreter.IL;
using Interpreter.Types;

namespace Interpreter.Core.Abstractions;

/// <summary>
/// Provides call-model handlers with all services required to evaluate a call without depending on VM internals.
/// </summary>
/// <typeparam name="TValue">Value-domain representation type.</typeparam>
/// <typeparam name="TMem">Memory-state representation type.</typeparam>
/// <param name="Domain">Value-domain operations.</param>
/// <param name="MemoryModel">Memory-model operations.</param>
/// <param name="Resolver">Metadata/token resolution service.</param>
/// <param name="Diagnostics">Diagnostic sink for explainability output.</param>
/// <param name="Budget">Current budget state.</param>
/// <param name="Memory">Current memory snapshot.</param>
/// <param name="Site">Call-site metadata envelope.</param>
/// <param name="Arguments">Call argument values in invocation order.</param>
/// <param name="Services">Optional host/service locator for advanced call models.</param>
public sealed record CallModelContext<TValue, TMem>(
    IValueDomain<TValue> Domain,
    IMemoryModel<TValue, TMem> MemoryModel,
    IResolutionServices Resolver,
    IDiagnosticSink Diagnostics,
    BudgetState Budget,
    TMem Memory,
    CallSite Site,
    IReadOnlyList<TValue> Arguments,
    IServiceProvider? Services = null);
