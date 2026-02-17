using Microsoft.Extensions.DependencyInjection;

namespace Interpreter.Hosting;

/// <summary>
/// Provides a draft composition entry point for registering prototype interpreter services into a host container.
/// </summary>
/// <remarks>
/// This API is a design-time placeholder. The extension and option names are explicitly provisional and should not be
/// treated as production-ready compatibility commitments.
/// </remarks>
public static class InterpreterHostingServiceCollectionExtensions
{
    /// <summary>
    /// Registers the current prototype set of interpreter services and returns the service collection for fluent configuration.
    /// </summary>
    /// <param name="services">The dependency injection service collection that receives interpreter registrations.</param>
    /// <param name="configure">An optional callback used to override draft default policies for exploratory scenarios.</param>
    /// <returns>The original service collection to support chained registration calls.</returns>
    public static IServiceCollection AddPrototypeInterpreter(
        this IServiceCollection services,
        Action<PrototypeInterpreterOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new PrototypeInterpreterOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);
        return services;
    }
}

/// <summary>
/// Defines host-configurable options for the draft interpreter prototype registration.
/// </summary>
/// <remarks>
/// All properties in this options type are intentionally mutable during the conceptual design phase and are expected to be
/// revisited once policy objects and module boundaries are finalized.
/// </remarks>
public sealed class PrototypeInterpreterOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether verbose diagnostic events should be emitted by default.
    /// </summary>
    public bool EnableVerboseDiagnostics { get; set; } = true;

    /// <summary>
    /// Gets or sets the default instruction budget applied when a request does not specify explicit limits.
    /// </summary>
    public int DefaultInstructionBudget { get; set; } = 50_000;

    /// <summary>
    /// Gets or sets the default branch budget applied when hosts do not provide request-level overrides.
    /// </summary>
    public int DefaultBranchBudget { get; set; } = 2_000;

    /// <summary>
    /// Gets or sets a value indicating whether synthetic debug-map generation should be enabled when symbols are unavailable.
    /// </summary>
    /// <remarks>
    /// Keeping this switch host-configurable in the prototype helps compare strict-symbol and synthesized-stepping experiences
    /// without committing to a single long-term fallback policy.
    /// </remarks>
    public bool EnableSyntheticDebugMaps { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of debug-map entries generated for one method when synthetic fallback is active.
    /// </summary>
    /// <remarks>
    /// This budget is a coarse safeguard to keep fallback synthesis deterministic and bounded while debug-map normalization
    /// algorithms are still evolving.
    /// </remarks>
    public int SyntheticDebugMapEntryBudget { get; set; } = 4_096;
}
