namespace Interpreter.Product.DumpDebugging;

/// <summary>Projects independently certified result fragments into the common counterfactual product envelope.</summary>
/// <remarks>
/// This draft W4 bridge accepts only issuer-certified canonical fragments and deliberately cannot synthesize rooted
/// request, plan, graph, or evidence identities.
/// </remarks>
public static class CounterfactualExecutionProjector
{
    /// <summary>
    /// Nests one unchanged canonical W4.7 target-outcome fragment without inventing a rooted request, plan, graph,
    /// traversal, depth, lineage, field-observation, or model-attempt identity.
    /// </summary>
    /// <param name="fragment">The complete exact/no-effect standalone target-outcome fragment to verify and nest.</param>
    /// <returns>
    /// A separately canonicalized common result whose origin is
    /// <see cref="CounterfactualExecutionOriginKind.StandaloneTargetOutcome"/> and whose instruction accounting,
    /// call trace, events, and diagnostics exactly project the fragment.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="fragment"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// The fragment's fixed axes, canonical bytes, or SHA-256 identity fail verification.
    /// </exception>
    public static CounterfactualExecutionResult FromTargetOutcome(CounterfactualTargetOutcomeFragment fragment)
    {
        ArgumentNullException.ThrowIfNull(fragment);
        if (!CounterfactualExecutionValue.IsCanonicalTargetOutcome(fragment))
        {
            throw new ArgumentException("A canonical certified W4.7 target-outcome fragment is required.", nameof(fragment));
        }

        return CounterfactualExecutionResult.CreateStandaloneTargetOutcome(fragment);
    }
}
