namespace Interpreter.Core.Abstractions;

/// <summary>
/// Defines the minimum value-domain contract required by the interpreter core.
/// </summary>
/// <typeparam name="TValue">Concrete value representation used by a domain implementation.</typeparam>
public interface IValueDomain<TValue>
{
    /// <summary>
    /// Creates the infeasible, least lattice value for a specific static type.
    /// </summary>
    /// <param name="type">The static CLI type whose value set is empty.</param>
    /// <returns>A typed bottom value used to represent contradictory refinements.</returns>
    TValue Bottom(TypeSig type);

    /// <summary>
    /// Determines whether a value is the infeasible lattice element.
    /// </summary>
    /// <param name="value">The domain value to inspect.</param>
    /// <returns><see langword="true"/> when <paramref name="value"/> denotes no possible runtime value.</returns>
    bool IsBottom(TValue value);

    /// <summary>Creates the greatest semantic lattice value for a specific static type.</summary>
    /// <param name="type">The static CLI type whose complete value set is represented.</param>
    /// <returns>A canonical typed top value; presentation provenance must live outside semantic equality.</returns>
    TValue Top(TypeSig type);

    /// <summary>Creates an exact signed 32-bit value in the CLI I4 stack category.</summary>
    /// <param name="value">The runtime integer payload.</param>
    /// <returns>A domain value representing only <paramref name="value"/>.</returns>
    TValue ConstInt32(int value);

    /// <summary>Computes the least upper bound of two same-typed semantic values.</summary>
    /// <param name="a">The first lattice value.</param>
    /// <param name="b">The second lattice value of the same static type.</param>
    /// <returns>The least value whose represented runtime set covers both inputs.</returns>
    TValue Join(TValue a, TValue b);

    /// <summary>
    /// Determines whether every concrete value represented by <paramref name="a"/> is also represented by
    /// <paramref name="b"/>.
    /// </summary>
    /// <param name="a">The candidate lower lattice value.</param>
    /// <param name="b">The candidate upper lattice value.</param>
    /// <returns><see langword="true"/> precisely when <c>a &lt;= b</c> in the domain's partial order.</returns>
    /// <remarks>
    /// Implementations must provide a reflexive and transitive relation. Together with <see cref="Join"/>, this
    /// operation makes lattice and widening obligations executable in tests rather than prose-only promises.
    /// </remarks>
    bool IsLessThanOrEqual(TValue a, TValue b);

    /// <summary>
    /// Computes the greatest lower bound of two values.
    /// </summary>
    /// <param name="a">The first value.</param>
    /// <param name="b">The second value.</param>
    /// <returns>The greatest value that is less than or equal to both inputs, possibly typed bottom.</returns>
    TValue Meet(TValue a, TValue b);

    /// <summary>Computes a deterministic widening that covers both an earlier and a later value.</summary>
    /// <param name="prev">The previous same-typed iteration value.</param>
    /// <param name="next">The next same-typed iteration value.</param>
    /// <returns>A stable covering value suitable for a future bounded fixpoint runner.</returns>
    TValue Widen(TValue prev, TValue next);

    /// <summary>Gets the static CLI type evidence carried by a semantic value.</summary>
    /// <param name="value">The value to classify.</param>
    /// <returns>The value's deterministic draft type signature.</returns>
    TypeSig GetStaticType(TValue value);

    /// <summary>Gets the CLI evaluation-stack category used by opcode-profile admission.</summary>
    /// <param name="value">The value to classify.</param>
    /// <returns>The value's stack category; E1 admits only <see cref="StackKind.I4"/>.</returns>
    StackKind GetStackKind(TValue value);

    /// <summary>Attempts to prove that a value is one exact signed 32-bit integer.</summary>
    /// <param name="value">The domain value to inspect.</param>
    /// <param name="c">The exact payload on success; otherwise zero.</param>
    /// <returns><see langword="true"/> only when no other runtime integer is represented.</returns>
    bool TryGetConstInt32(TValue value, out int c);

    /// <summary>Applies one arithmetic operation from the closed E1 binary-operation set.</summary>
    /// <param name="op">The admitted add, subtract, or multiply operation.</param>
    /// <param name="a">The left same-typed operand.</param>
    /// <param name="b">The right same-typed operand.</param>
    /// <returns>The operation result, preserving lattice bottom/top behavior and unchecked Int32 overflow.</returns>
    TValue ApplyBinary(BinaryOp op, TValue a, TValue b);

}
