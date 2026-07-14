using System.Collections.Immutable;

namespace Interpreter.Core.Abstractions;

/// <summary>
/// Represents immutable IL body bytes plus the minimum admission facts preserved by the current prototype.
/// </summary>
/// <param name="MaxStack">The declared maximum evaluation-stack depth.</param>
/// <param name="CodeBytes">An immutable copy of the method's IL instruction bytes.</param>
/// <param name="LocalVariablesInitialized">Whether the method header requests zero-initialized locals.</param>
/// <param name="LocalSignatureToken">The StandAloneSig token for locals, or zero when no local signature exists.</param>
/// <param name="ExceptionRegionCount">The number of exception-handling clauses carried by the method body.</param>
/// <remarks>
/// The slice does not yet decode local types or exception regions, but it must preserve their presence so admission
/// can reject unsupported semantics before any prefix executes. A pre-seeded-locals fixture may opt into a nonzero
/// local signature token explicitly; arbitrary production locals remain outside the admitted contract.
/// </remarks>
public sealed record MethodBody(
    int MaxStack,
    ImmutableArray<byte> CodeBytes,
    bool LocalVariablesInitialized,
    int LocalSignatureToken,
    int ExceptionRegionCount)
{
    /// <summary>Gets a value indicating whether the method declares a local-variable signature.</summary>
    public bool HasLocalSignature => LocalSignatureToken != 0;

    /// <summary>
    /// Creates a method body by copying a mutable byte span into immutable storage.
    /// </summary>
    /// <param name="maxStack">The declared maximum evaluation-stack depth.</param>
    /// <param name="codeBytes">The IL instruction bytes to copy.</param>
    /// <param name="localVariablesInitialized">Whether the method header requests zero-initialized locals.</param>
    /// <param name="localSignatureToken">The StandAloneSig token, or zero when absent.</param>
    /// <param name="exceptionRegionCount">The number of exception regions recorded in the body.</param>
    /// <returns>A method body whose bytes cannot be changed through the source buffer.</returns>
    public static MethodBody Create(
        int maxStack,
        ReadOnlySpan<byte> codeBytes,
        bool localVariablesInitialized = false,
        int localSignatureToken = 0,
        int exceptionRegionCount = 0) =>
        new(
            maxStack,
            ImmutableArray.Create(codeBytes.ToArray()),
            localVariablesInitialized,
            localSignatureToken,
            exceptionRegionCount);
}
