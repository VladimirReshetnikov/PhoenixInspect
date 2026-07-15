namespace Interpreter.Core.Execution;

/// <summary>
/// Selects whether a machine activation may transport provenance-bearing unknown <see cref="int"/> values.
/// </summary>
public enum UnknownExecutionPolicy
{
    /// <summary>Require exact values at every executable boundary.</summary>
    ExactOnly,

    /// <summary>
    /// Permit structurally typed <see cref="int"/> values whose domain validates an explanatory lineage root.
    /// </summary>
    ExplainedInt32,
}
