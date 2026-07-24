using System.Collections.Immutable;
using System.Globalization;
using System.Reflection;
using System.Reflection.Metadata;
using System.Text;
using PhoenixInspect.Core.Abstractions;

namespace PhoenixInspect.Host.Dump.ClrMD;

/// <summary>Declares physical acquisition limits for one selected method's complete lexical evidence.</summary>
/// <remarks>
/// These limits are draft adapter contracts rather than compatibility promises. The complete MethodDef and Param
/// catalogs are evidence used to prove source end and generated-name coverage; they are not display inventories.
/// </remarks>
public static class DumpSelectedMethodLexicalLimits
{
    /// <summary>Gets the maximum complete MethodDef-table row count.</summary>
    public const int MaximumMethodDefinitionRowCount = 65_536;

    /// <summary>Gets the maximum complete Param-table row count.</summary>
    public const int MaximumParameterRowCount = 262_144;

    /// <summary>Gets the maximum selected-owner GenericParam-row count.</summary>
    public const int MaximumGenericParameterRowCount = 4_096;

    /// <summary>Gets the maximum complete signature-blob length for one MethodDef row.</summary>
    public const int MaximumMethodSignatureByteCount = 65_536;

    /// <summary>Gets the maximum aggregate MethodDef signature bytes in one complete module catalog.</summary>
    public const int MaximumAggregateMethodSignatureByteCount = 64 * 1_024 * 1_024;

    /// <summary>Gets the maximum recursive type depth accepted in one MethodDef or local signature.</summary>
    public const int MaximumSignatureTypeDepth = 256;

    /// <summary>Gets the maximum aggregate type-node count accepted in one MethodDef or local signature.</summary>
    public const int MaximumSignatureTypeNodeCount = 131_072;

    /// <summary>Gets the maximum complete StandAloneSig blob length selected by the method body.</summary>
    public const int MaximumStandAloneSignatureByteCount = 262_144;

    /// <summary>Gets the maximum complete local-slot count decoded from the selected StandAloneSig.</summary>
    public const int MaximumLocalSlotCount = 65_536;

    /// <summary>Gets the maximum complete LocalVariable-row count for the selected method's scopes.</summary>
    public const int MaximumLocalVariableRowCount = 65_536;

    /// <summary>Gets the maximum complete LocalConstant-row count for the selected method's scopes.</summary>
    public const int MaximumLocalConstantRowCount = 65_536;

    /// <summary>Gets the maximum complete signature-blob length for one LocalConstant row.</summary>
    public const int MaximumLocalConstantSignatureByteCount = 65_536;

    /// <summary>Gets the maximum aggregate LocalConstant signature bytes retained for one selected method.</summary>
    public const int MaximumAggregateLocalConstantSignatureByteCount = 16 * 1_024 * 1_024;

    /// <summary>Gets the maximum raw-name-derived generated local-function row count.</summary>
    public const int MaximumGeneratedLocalFunctionRowCount = 4_096;

    private static readonly EvaluationDeterministicBound MethodDefinitionRowBound =
        new("dump.context.selected-method.method-definition-rows", MaximumMethodDefinitionRowCount);
    private static readonly EvaluationDeterministicBound ParameterRowBound =
        new("dump.context.selected-method.parameter-rows", MaximumParameterRowCount);
    private static readonly EvaluationDeterministicBound GenericParameterRowBound =
        new("dump.context.selected-method.generic-parameter-rows", MaximumGenericParameterRowCount);
    private static readonly EvaluationDeterministicBound MethodSignatureBlobByteBound =
        new("dump.context.selected-method.method-signature-blob-bytes", MaximumMethodSignatureByteCount);
    private static readonly EvaluationDeterministicBound AggregateMethodSignatureByteBound =
        new("dump.context.selected-method.method-signature-bytes", MaximumAggregateMethodSignatureByteCount);
    private static readonly EvaluationDeterministicBound SignatureTypeDepthBound =
        new("dump.context.selected-method.signature-type-depth", MaximumSignatureTypeDepth);
    private static readonly EvaluationDeterministicBound SignatureTypeNodeBound =
        new("dump.context.selected-method.signature-type-nodes", MaximumSignatureTypeNodeCount);
    private static readonly EvaluationDeterministicBound StandAloneSignatureByteBound =
        new("dump.context.selected-method.standalone-signature-bytes", MaximumStandAloneSignatureByteCount);
    private static readonly EvaluationDeterministicBound LocalSlotBound =
        new("dump.context.selected-method.local-slots", MaximumLocalSlotCount);
    private static readonly EvaluationDeterministicBound LocalVariableRowBound =
        new("dump.context.selected-method.local-variable-rows", MaximumLocalVariableRowCount);
    private static readonly EvaluationDeterministicBound LocalConstantRowBound =
        new("dump.context.selected-method.local-constant-rows", MaximumLocalConstantRowCount);
    private static readonly EvaluationDeterministicBound LocalConstantSignatureBlobByteBound =
        new(
            "dump.context.selected-method.local-constant-signature-blob-bytes",
            MaximumLocalConstantSignatureByteCount);
    private static readonly EvaluationDeterministicBound LocalConstantSignatureByteBound =
        new("dump.context.selected-method.local-constant-signature-bytes", MaximumAggregateLocalConstantSignatureByteCount);
    private static readonly EvaluationDeterministicBound GeneratedLocalFunctionRowBound =
        new("dump.context.selected-method.generated-local-function-rows", MaximumGeneratedLocalFunctionRowCount);

    private static readonly ImmutableArray<EvaluationDeterministicBound> DeclaredBounds =
    [
        GenericParameterRowBound,
        GeneratedLocalFunctionRowBound,
        LocalConstantRowBound,
        LocalConstantSignatureBlobByteBound,
        LocalConstantSignatureByteBound,
        ClrmdDumpSession.PortablePdbLocalScopeTraversalBound,
        LocalSlotBound,
        LocalVariableRowBound,
        MethodDefinitionRowBound,
        MethodSignatureBlobByteBound,
        AggregateMethodSignatureByteBound,
        ParameterRowBound,
        SignatureTypeDepthBound,
        SignatureTypeNodeBound,
        StandAloneSignatureByteBound,
    ];

    /// <summary>Gets the complete module MethodDef-table traversal bound.</summary>
    public static EvaluationDeterministicBound MethodDefinitionRowTraversalBound => MethodDefinitionRowBound;

    /// <summary>Gets the complete module Param-table traversal bound.</summary>
    public static EvaluationDeterministicBound ParameterRowTraversalBound => ParameterRowBound;

    /// <summary>Gets the selected owner GenericParam-row traversal bound.</summary>
    public static EvaluationDeterministicBound GenericParameterRowTraversalBound => GenericParameterRowBound;

    /// <summary>Gets the per-MethodDef complete signature-blob byte bound.</summary>
    public static EvaluationDeterministicBound MethodSignatureBlobByteTraversalBound => MethodSignatureBlobByteBound;

    /// <summary>Gets the aggregate complete MethodDef signature-byte bound.</summary>
    public static EvaluationDeterministicBound MethodSignatureByteTraversalBound => AggregateMethodSignatureByteBound;

    /// <summary>Gets the recursive MethodDef/local signature type-depth bound.</summary>
    public static EvaluationDeterministicBound SignatureTypeDepthTraversalBound => SignatureTypeDepthBound;

    /// <summary>Gets the aggregate MethodDef/local signature type-node bound.</summary>
    public static EvaluationDeterministicBound SignatureTypeNodeTraversalBound => SignatureTypeNodeBound;

    /// <summary>Gets the complete selected StandAloneSig byte bound.</summary>
    public static EvaluationDeterministicBound StandAloneSignatureByteTraversalBound => StandAloneSignatureByteBound;

    /// <summary>Gets the complete selected StandAloneSig local-slot count bound.</summary>
    public static EvaluationDeterministicBound LocalSlotTraversalBound => LocalSlotBound;

    /// <summary>Gets the unchanged W7 Portable-PDB LocalScope traversal bound.</summary>
    public static EvaluationDeterministicBound LocalScopeRowTraversalBound =>
        ClrmdDumpSession.PortablePdbLocalScopeTraversalBound;

    /// <summary>Gets the complete Portable-PDB LocalVariable-table traversal bound.</summary>
    public static EvaluationDeterministicBound LocalVariableRowTraversalBound => LocalVariableRowBound;

    /// <summary>Gets the complete Portable-PDB LocalConstant-table traversal bound.</summary>
    public static EvaluationDeterministicBound LocalConstantRowTraversalBound => LocalConstantRowBound;

    /// <summary>Gets the per-row complete LocalConstant signature-blob byte bound.</summary>
    public static EvaluationDeterministicBound LocalConstantSignatureBlobByteTraversalBound =>
        LocalConstantSignatureBlobByteBound;

    /// <summary>Gets the aggregate complete Portable-PDB LocalConstant signature-byte bound.</summary>
    public static EvaluationDeterministicBound LocalConstantSignatureByteTraversalBound =>
        LocalConstantSignatureByteBound;

    /// <summary>Gets the generated local-function row derivation bound.</summary>
    public static EvaluationDeterministicBound GeneratedLocalFunctionRowTraversalBound =>
        GeneratedLocalFunctionRowBound;

    /// <summary>Gets a defensive, canonical-name-ordered copy of every draft physical acquisition bound.</summary>
    public static ImmutableArray<EvaluationDeterministicBound> AllDeclaredBounds =>
        ImmutableArray.CreateRange(DeclaredBounds.OrderBy(static bound => bound.Name, StringComparer.Ordinal));
}

/// <summary>Records an ordered bounded source-end measurement trace for one selected-method lexical acquisition.</summary>
/// <remarks>
/// This draft evidence is produced only after bounded enumeration has either observed each source end or counted the
/// first item beyond a declared limit. Properties through that boundary are measurements, while later properties are
/// absent and canonically zero. Keeping the trace separate from row arrays lets an over-limit observation discard every
/// materialized prefix.
/// </remarks>
public sealed class DumpSelectedMethodLexicalSourceEnds : IEquatable<DumpSelectedMethodLexicalSourceEnds>
{
    private const int MethodDefinitionRowMeasurementPosition = 0;
    private const int MaximumMethodSignatureByteMeasurementPosition = 4;
    private const int AggregateMethodSignatureByteMeasurementPosition = 5;
    private const int StandaloneSignatureByteMeasurementPosition = 6;
    private const int LocalSlotMeasurementPosition = 7;
    private const int LocalConstantRowMeasurementPosition = 12;
    private const int MaximumLocalConstantSignatureByteMeasurementPosition = 13;
    private const int AggregateLocalConstantSignatureByteMeasurementPosition = 14;
    private const int GeneratedLocalFunctionRowMeasurementPosition = 15;

    private readonly ImmutableArray<byte> canonicalBytes;
    private readonly int canonicalHashCode;

    private DumpSelectedMethodLexicalSourceEnds(
        int methodDefinitionRowCount,
        int parameterRowCount,
        int declaringTypeGenericParameterRowCount,
        int methodGenericParameterRowCount,
        int localScopeRowCount,
        int localVariableRowCount,
        int localConstantRowCount,
        int maximumMethodSignatureByteCount,
        long aggregateMethodSignatureByteCount,
        int maximumSignatureTypeDepth,
        int maximumSignatureTypeNodeCount,
        int standaloneSignatureByteCount,
        int localSlotCount,
        int maximumLocalConstantSignatureByteCount,
        long aggregateLocalConstantSignatureByteCount,
        int generatedLocalFunctionRowCount)
    {
        MethodDefinitionRowCount = methodDefinitionRowCount;
        ParameterRowCount = parameterRowCount;
        DeclaringTypeGenericParameterRowCount = declaringTypeGenericParameterRowCount;
        MethodGenericParameterRowCount = methodGenericParameterRowCount;
        LocalScopeRowCount = localScopeRowCount;
        LocalVariableRowCount = localVariableRowCount;
        LocalConstantRowCount = localConstantRowCount;
        MaximumMethodSignatureByteCount = maximumMethodSignatureByteCount;
        AggregateMethodSignatureByteCount = aggregateMethodSignatureByteCount;
        MaximumSignatureTypeDepth = maximumSignatureTypeDepth;
        MaximumSignatureTypeNodeCount = maximumSignatureTypeNodeCount;
        StandaloneSignatureByteCount = standaloneSignatureByteCount;
        LocalSlotCount = localSlotCount;
        MaximumLocalConstantSignatureByteCount = maximumLocalConstantSignatureByteCount;
        AggregateLocalConstantSignatureByteCount = aggregateLocalConstantSignatureByteCount;
        GeneratedLocalFunctionRowCount = generatedLocalFunctionRowCount;

        var writer = new CanonicalReplayEncoding.Writer("dump-selected-method-lexical-source-ends", 1);
        writer.WriteInt32(methodDefinitionRowCount);
        writer.WriteInt32(parameterRowCount);
        writer.WriteInt32(declaringTypeGenericParameterRowCount);
        writer.WriteInt32(methodGenericParameterRowCount);
        writer.WriteInt32(localScopeRowCount);
        writer.WriteInt32(localVariableRowCount);
        writer.WriteInt32(localConstantRowCount);
        writer.WriteInt32(maximumMethodSignatureByteCount);
        writer.WriteInt64(aggregateMethodSignatureByteCount);
        writer.WriteInt32(maximumSignatureTypeDepth);
        writer.WriteInt32(maximumSignatureTypeNodeCount);
        writer.WriteInt32(standaloneSignatureByteCount);
        writer.WriteInt32(localSlotCount);
        writer.WriteInt32(maximumLocalConstantSignatureByteCount);
        writer.WriteInt64(aggregateLocalConstantSignatureByteCount);
        writer.WriteInt32(generatedLocalFunctionRowCount);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
        canonicalHashCode = CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
    }

    /// <summary>Gets the measured complete MethodDef-table row count.</summary>
    public int MethodDefinitionRowCount { get; }

    /// <summary>Gets the measured complete Param-table row count.</summary>
    public int ParameterRowCount { get; }

    /// <summary>Gets the measured selected declaring-TypeDef GenericParam row count.</summary>
    public int DeclaringTypeGenericParameterRowCount { get; }

    /// <summary>Gets the measured selected MethodDef GenericParam row count.</summary>
    public int MethodGenericParameterRowCount { get; }

    /// <summary>Gets the measured complete Portable-PDB LocalScope-table row count.</summary>
    public int LocalScopeRowCount { get; }

    /// <summary>Gets the measured complete Portable-PDB LocalVariable-table row count.</summary>
    public int LocalVariableRowCount { get; }

    /// <summary>Gets the measured complete Portable-PDB LocalConstant-table row count.</summary>
    public int LocalConstantRowCount { get; }

    /// <summary>Gets the largest complete MethodDef signature blob length.</summary>
    public int MaximumMethodSignatureByteCount { get; }

    /// <summary>Gets the measured aggregate MethodDef signature blob length.</summary>
    public long AggregateMethodSignatureByteCount { get; }

    /// <summary>Gets the largest recursive type depth observed in a MethodDef or selected local signature.</summary>
    public int MaximumSignatureTypeDepth { get; }

    /// <summary>Gets the largest aggregate type-node count observed in a MethodDef or selected local signature.</summary>
    public int MaximumSignatureTypeNodeCount { get; }

    /// <summary>Gets the measured selected StandAloneSig length, or zero when absent.</summary>
    public int StandaloneSignatureByteCount { get; }

    /// <summary>Gets the measured decoded local-slot count, or zero when absent.</summary>
    public int LocalSlotCount { get; }

    /// <summary>Gets the largest complete LocalConstant signature blob length.</summary>
    public int MaximumLocalConstantSignatureByteCount { get; }

    /// <summary>Gets the measured aggregate LocalConstant signature blob length.</summary>
    public long AggregateLocalConstantSignatureByteCount { get; }

    /// <summary>Gets the measured raw-name-derived local-function candidate row count.</summary>
    public int GeneratedLocalFunctionRowCount { get; }

    /// <summary>Gets a defensive copy of the canonical draft measurement bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => CanonicalReplayEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of <see cref="CanonicalBytes"/>.</summary>
    public string Sha256 { get; }

    /// <summary>Creates an ordered bounded trace whose reached prefix is exact.</summary>
    /// <param name="methodDefinitionRowCount">The complete MethodDef-table row count.</param>
    /// <param name="parameterRowCount">The complete Param-table row count.</param>
    /// <param name="declaringTypeGenericParameterRowCount">Selected declaring-TypeDef GenericParam rows.</param>
    /// <param name="methodGenericParameterRowCount">Selected MethodDef GenericParam rows.</param>
    /// <param name="localScopeRowCount">Complete Portable-PDB LocalScope-table rows.</param>
    /// <param name="localVariableRowCount">Complete Portable-PDB LocalVariable-table rows.</param>
    /// <param name="localConstantRowCount">Complete Portable-PDB LocalConstant-table rows.</param>
    /// <param name="maximumMethodSignatureByteCount">Largest MethodDef signature length.</param>
    /// <param name="aggregateMethodSignatureByteCount">Aggregate MethodDef signature length.</param>
    /// <param name="maximumSignatureTypeDepth">Largest recursive type depth in a method or local signature.</param>
    /// <param name="maximumSignatureTypeNodeCount">Largest aggregate type-node count in a method or local signature.</param>
    /// <param name="standaloneSignatureByteCount">Selected StandAloneSig length, or zero.</param>
    /// <param name="localSlotCount">Decoded selected local-slot count, or zero.</param>
    /// <param name="maximumLocalConstantSignatureByteCount">Largest LocalConstant signature length.</param>
    /// <param name="aggregateLocalConstantSignatureByteCount">Aggregate LocalConstant signature length.</param>
    /// <param name="generatedLocalFunctionRowCount">Raw-name-derived candidate MethodDef count.</param>
    /// <returns>An immutable draft source-end measurement.</returns>
    /// <remarks>
    /// Measurements are exact through the first boundary. The first over-limit source is saturated at cap plus one,
    /// and later operations remain absent as zero. Acquisition checks this ordered trace before reading any row array.
    /// </remarks>
    public static DumpSelectedMethodLexicalSourceEnds Create(
        int methodDefinitionRowCount,
        int parameterRowCount,
        int declaringTypeGenericParameterRowCount,
        int methodGenericParameterRowCount,
        int localScopeRowCount,
        int localVariableRowCount,
        int localConstantRowCount,
        int maximumMethodSignatureByteCount,
        long aggregateMethodSignatureByteCount,
        int maximumSignatureTypeDepth,
        int maximumSignatureTypeNodeCount,
        int standaloneSignatureByteCount,
        int localSlotCount,
        int maximumLocalConstantSignatureByteCount,
        long aggregateLocalConstantSignatureByteCount,
        int generatedLocalFunctionRowCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(methodDefinitionRowCount);
        ArgumentOutOfRangeException.ThrowIfNegative(parameterRowCount);
        ArgumentOutOfRangeException.ThrowIfNegative(declaringTypeGenericParameterRowCount);
        ArgumentOutOfRangeException.ThrowIfNegative(methodGenericParameterRowCount);
        ArgumentOutOfRangeException.ThrowIfNegative(localScopeRowCount);
        ArgumentOutOfRangeException.ThrowIfNegative(localVariableRowCount);
        ArgumentOutOfRangeException.ThrowIfNegative(localConstantRowCount);
        ArgumentOutOfRangeException.ThrowIfNegative(maximumMethodSignatureByteCount);
        ArgumentOutOfRangeException.ThrowIfNegative(aggregateMethodSignatureByteCount);
        ArgumentOutOfRangeException.ThrowIfNegative(maximumSignatureTypeDepth);
        ArgumentOutOfRangeException.ThrowIfNegative(maximumSignatureTypeNodeCount);
        ArgumentOutOfRangeException.ThrowIfNegative(standaloneSignatureByteCount);
        ArgumentOutOfRangeException.ThrowIfNegative(localSlotCount);
        ArgumentOutOfRangeException.ThrowIfNegative(maximumLocalConstantSignatureByteCount);
        ArgumentOutOfRangeException.ThrowIfNegative(aggregateLocalConstantSignatureByteCount);
        ArgumentOutOfRangeException.ThrowIfNegative(generatedLocalFunctionRowCount);
        var firstBoundaryPosition = ValidateMeasurementSequence(
            (methodDefinitionRowCount, DumpSelectedMethodLexicalLimits.MethodDefinitionRowTraversalBound),
            (parameterRowCount, DumpSelectedMethodLexicalLimits.ParameterRowTraversalBound),
            (declaringTypeGenericParameterRowCount, DumpSelectedMethodLexicalLimits.GenericParameterRowTraversalBound),
            (methodGenericParameterRowCount, DumpSelectedMethodLexicalLimits.GenericParameterRowTraversalBound),
            (maximumMethodSignatureByteCount, DumpSelectedMethodLexicalLimits.MethodSignatureBlobByteTraversalBound),
            (aggregateMethodSignatureByteCount, DumpSelectedMethodLexicalLimits.MethodSignatureByteTraversalBound),
            (standaloneSignatureByteCount, DumpSelectedMethodLexicalLimits.StandAloneSignatureByteTraversalBound),
            (localSlotCount, DumpSelectedMethodLexicalLimits.LocalSlotTraversalBound),
            (maximumSignatureTypeDepth, DumpSelectedMethodLexicalLimits.SignatureTypeDepthTraversalBound),
            (maximumSignatureTypeNodeCount, DumpSelectedMethodLexicalLimits.SignatureTypeNodeTraversalBound),
            (localScopeRowCount, DumpSelectedMethodLexicalLimits.LocalScopeRowTraversalBound),
            (localVariableRowCount, DumpSelectedMethodLexicalLimits.LocalVariableRowTraversalBound),
            (localConstantRowCount, DumpSelectedMethodLexicalLimits.LocalConstantRowTraversalBound),
            (
                maximumLocalConstantSignatureByteCount,
                DumpSelectedMethodLexicalLimits.LocalConstantSignatureBlobByteTraversalBound),
            (
                aggregateLocalConstantSignatureByteCount,
                DumpSelectedMethodLexicalLimits.LocalConstantSignatureByteTraversalBound),
            (generatedLocalFunctionRowCount, DumpSelectedMethodLexicalLimits.GeneratedLocalFunctionRowTraversalBound));
        if (MeasurementIsObserved(
                MaximumMethodSignatureByteMeasurementPosition,
                firstBoundaryPosition) &&
            MeasurementIsObserved(
                AggregateMethodSignatureByteMeasurementPosition,
                firstBoundaryPosition) &&
            maximumMethodSignatureByteCount > aggregateMethodSignatureByteCount)
        {
            throw new ArgumentException(
                "MethodDef signature maximum and aggregate measurements must agree with the observed row count.",
                nameof(aggregateMethodSignatureByteCount));
        }

        if (MeasurementIsObserved(MethodDefinitionRowMeasurementPosition, firstBoundaryPosition) &&
            MeasurementIsObserved(AggregateMethodSignatureByteMeasurementPosition, firstBoundaryPosition) &&
            (methodDefinitionRowCount == 0) != (aggregateMethodSignatureByteCount == 0))
        {
            throw new ArgumentException(
                "MethodDef signature maximum and aggregate measurements must agree with the observed row count.",
                nameof(aggregateMethodSignatureByteCount));
        }

        if (MeasurementIsObserved(MethodDefinitionRowMeasurementPosition, firstBoundaryPosition) &&
            MeasurementIsObserved(MaximumMethodSignatureByteMeasurementPosition, firstBoundaryPosition) &&
            methodDefinitionRowCount > 0 &&
            maximumMethodSignatureByteCount == 0)
        {
            throw new ArgumentException(
                "MethodDef signature maximum and aggregate measurements must agree with the observed row count.",
                nameof(aggregateMethodSignatureByteCount));
        }

        if (MeasurementIsObserved(
                MaximumLocalConstantSignatureByteMeasurementPosition,
                firstBoundaryPosition) &&
            MeasurementIsObserved(
                AggregateLocalConstantSignatureByteMeasurementPosition,
                firstBoundaryPosition) &&
            maximumLocalConstantSignatureByteCount > aggregateLocalConstantSignatureByteCount)
        {
            throw new ArgumentException(
                "LocalConstant signature maximum and aggregate measurements must agree with the observed row count.",
                nameof(aggregateLocalConstantSignatureByteCount));
        }

        if (MeasurementIsObserved(LocalConstantRowMeasurementPosition, firstBoundaryPosition) &&
            MeasurementIsObserved(
                AggregateLocalConstantSignatureByteMeasurementPosition,
                firstBoundaryPosition) &&
            (localConstantRowCount == 0) != (aggregateLocalConstantSignatureByteCount == 0))
        {
            throw new ArgumentException(
                "LocalConstant signature maximum and aggregate measurements must agree with the observed row count.",
                nameof(aggregateLocalConstantSignatureByteCount));
        }

        if (MeasurementIsObserved(LocalConstantRowMeasurementPosition, firstBoundaryPosition) &&
            MeasurementIsObserved(
                MaximumLocalConstantSignatureByteMeasurementPosition,
                firstBoundaryPosition) &&
            localConstantRowCount > 0 &&
            maximumLocalConstantSignatureByteCount == 0)
        {
            throw new ArgumentException(
                "LocalConstant signature maximum and aggregate measurements must agree with the observed row count.",
                nameof(aggregateLocalConstantSignatureByteCount));
        }

        if (MeasurementIsObserved(StandaloneSignatureByteMeasurementPosition, firstBoundaryPosition) &&
            MeasurementIsObserved(LocalSlotMeasurementPosition, firstBoundaryPosition) &&
            standaloneSignatureByteCount == 0 &&
            localSlotCount != 0)
        {
            throw new ArgumentException(
                "A nonzero local-slot count requires an observed StandAloneSig.",
                nameof(localSlotCount));
        }

        if (MeasurementIsObserved(GeneratedLocalFunctionRowMeasurementPosition, firstBoundaryPosition) &&
            generatedLocalFunctionRowCount > methodDefinitionRowCount)
        {
            throw new ArgumentException(
                "Generated local-function candidates cannot outnumber observed MethodDef rows.",
                nameof(generatedLocalFunctionRowCount));
        }

        return new DumpSelectedMethodLexicalSourceEnds(
            methodDefinitionRowCount,
            parameterRowCount,
            declaringTypeGenericParameterRowCount,
            methodGenericParameterRowCount,
            localScopeRowCount,
            localVariableRowCount,
            localConstantRowCount,
            maximumMethodSignatureByteCount,
            aggregateMethodSignatureByteCount,
            maximumSignatureTypeDepth,
            maximumSignatureTypeNodeCount,
            standaloneSignatureByteCount,
            localSlotCount,
            maximumLocalConstantSignatureByteCount,
            aggregateLocalConstantSignatureByteCount,
            generatedLocalFunctionRowCount);
    }

    /// <summary>Determines content equality from all canonical source-end measurements.</summary>
    /// <param name="other">The measurement to compare.</param>
    /// <returns><see langword="true"/> when every count and byte total is equal.</returns>
    public bool Equals(DumpSelectedMethodLexicalSourceEnds? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as DumpSelectedMethodLexicalSourceEnds);

    /// <inheritdoc/>
    public override int GetHashCode() => canonicalHashCode;

    private static int? ValidateMeasurementSequence(
        params (long Value, EvaluationDeterministicBound Bound)[] measurements)
    {
        int? firstBoundaryPosition = null;
        for (var position = 0; position < measurements.Length; position++)
        {
            var measurement = measurements[position];
            if (firstBoundaryPosition is not null)
            {
                if (measurement.Value != 0)
                {
                    throw new ArgumentException(
                        "Measurements after the first over-limit witness must remain absent and use zero.",
                        nameof(measurements));
                }

                continue;
            }

            if (measurement.Value > measurement.Bound.Value + 1)
            {
                throw new ArgumentException(
                    "An over-limit measurement must saturate at exactly the declared bound plus one.",
                    nameof(measurements));
            }

            if (measurement.Value > measurement.Bound.Value)
            {
                firstBoundaryPosition = position;
            }
        }

        return firstBoundaryPosition;
    }

    private static bool MeasurementIsObserved(int measurementPosition, int? firstBoundaryPosition) =>
        firstBoundaryPosition is null || measurementPosition <= firstBoundaryPosition.Value;

    internal ImmutableArray<EvaluationDeterministicBound> GetReachedBounds(out bool exceeded)
    {
        var reached = ImmutableArray.CreateBuilder<EvaluationDeterministicBound>();
        var hasExceeded = false;
        Observe(MethodDefinitionRowCount, DumpSelectedMethodLexicalLimits.MethodDefinitionRowTraversalBound);
        Observe(ParameterRowCount, DumpSelectedMethodLexicalLimits.ParameterRowTraversalBound);
        Observe(DeclaringTypeGenericParameterRowCount, DumpSelectedMethodLexicalLimits.GenericParameterRowTraversalBound);
        Observe(MethodGenericParameterRowCount, DumpSelectedMethodLexicalLimits.GenericParameterRowTraversalBound);
        Observe(MaximumMethodSignatureByteCount, DumpSelectedMethodLexicalLimits.MethodSignatureBlobByteTraversalBound);
        Observe(AggregateMethodSignatureByteCount, DumpSelectedMethodLexicalLimits.MethodSignatureByteTraversalBound);
        Observe(StandaloneSignatureByteCount, DumpSelectedMethodLexicalLimits.StandAloneSignatureByteTraversalBound);
        Observe(LocalSlotCount, DumpSelectedMethodLexicalLimits.LocalSlotTraversalBound);
        Observe(MaximumSignatureTypeDepth, DumpSelectedMethodLexicalLimits.SignatureTypeDepthTraversalBound);
        Observe(MaximumSignatureTypeNodeCount, DumpSelectedMethodLexicalLimits.SignatureTypeNodeTraversalBound);
        Observe(LocalScopeRowCount, DumpSelectedMethodLexicalLimits.LocalScopeRowTraversalBound);
        Observe(LocalVariableRowCount, DumpSelectedMethodLexicalLimits.LocalVariableRowTraversalBound);
        Observe(LocalConstantRowCount, DumpSelectedMethodLexicalLimits.LocalConstantRowTraversalBound);
        Observe(
            MaximumLocalConstantSignatureByteCount,
            DumpSelectedMethodLexicalLimits.LocalConstantSignatureBlobByteTraversalBound);
        Observe(
            AggregateLocalConstantSignatureByteCount,
            DumpSelectedMethodLexicalLimits.LocalConstantSignatureByteTraversalBound);
        Observe(
            GeneratedLocalFunctionRowCount,
            DumpSelectedMethodLexicalLimits.GeneratedLocalFunctionRowTraversalBound);
        exceeded = hasExceeded;
        return CanonicalReplayEncoding.NormalizeBounds(reached.ToImmutable(), nameof(reached));

        void Observe(long value, EvaluationDeterministicBound bound)
        {
            if (hasExceeded)
            {
                return;
            }

            if (value >= bound.Value && !reached.Any(candidate =>
                    string.Equals(candidate.Name, bound.Name, StringComparison.Ordinal)))
            {
                reached.Add(bound);
            }

            hasExceeded = value > bound.Value;
        }
    }
}

/// <summary>Retains one exact physical Param row owned through a MethodDef ParamList interval.</summary>
/// <remarks>
/// This draft fact preserves an empty metadata name rather than treating it as row absence. Lexical completeness is a
/// later consumer decision; this type records the physical row without filling a missing name.
/// </remarks>
public sealed class DumpLexicalParameterRowFact : IEquatable<DumpLexicalParameterRowFact>
{
    private readonly ImmutableArray<byte> canonicalBytes;
    private readonly int canonicalHashCode;

    private DumpLexicalParameterRowFact(
        int parameterToken,
        int sequenceNumber,
        ParameterAttributes attributes,
        string name)
    {
        ParameterToken = parameterToken;
        SequenceNumber = sequenceNumber;
        Attributes = attributes;
        Name = name;
        var writer = new CanonicalReplayEncoding.Writer("dump-lexical-parameter-row", 1);
        WriteCanonical(writer);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
        canonicalHashCode = CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
    }

    /// <summary>Gets the non-nil Param token.</summary>
    public int ParameterToken { get; }

    /// <summary>Gets the exact unsigned Param sequence number; zero denotes the return row.</summary>
    public int SequenceNumber { get; }

    /// <summary>Gets the exact Param attribute bits.</summary>
    public ParameterAttributes Attributes { get; }

    /// <summary>Gets the exact metadata name, including an observed empty name.</summary>
    public string Name { get; }

    /// <summary>Gets a defensive copy of the draft canonical row bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => CanonicalReplayEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of <see cref="CanonicalBytes"/>.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one exact physical Param-row fact.</summary>
    /// <param name="parameterToken">A non-nil Param token.</param>
    /// <param name="sequenceNumber">The unsigned physical sequence number.</param>
    /// <param name="attributes">The exact Param attribute bits.</param>
    /// <param name="name">The exact metadata name; empty is retained.</param>
    /// <returns>An immutable draft Param-row fact.</returns>
    /// <remarks>No owner is accepted here because Param ownership is proven later by MethodDef ParamList intervals.</remarks>
    public static DumpLexicalParameterRowFact Create(
        int parameterToken,
        int sequenceNumber,
        ParameterAttributes attributes,
        string name)
    {
        CanonicalReplayEncoding.ValidateMetadataToken(parameterToken, 0x08, nameof(parameterToken));
        if (sequenceNumber is < 0 or > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(sequenceNumber));
        }

        DumpSelectedMethodLexicalEncoding.ValidateText(name, nameof(name), allowEmpty: true);
        return new DumpLexicalParameterRowFact(parameterToken, sequenceNumber, attributes, name);
    }

    /// <summary>Determines content equality from canonical physical row bytes.</summary>
    /// <param name="other">The row to compare.</param>
    /// <returns><see langword="true"/> when token, sequence, attributes, and name are equal.</returns>
    public bool Equals(DumpLexicalParameterRowFact? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as DumpLexicalParameterRowFact);

    /// <inheritdoc/>
    public override int GetHashCode() => canonicalHashCode;

    internal void WriteCanonical(CanonicalReplayEncoding.Writer writer)
    {
        writer.WriteInt32(ParameterToken);
        writer.WriteInt32(SequenceNumber);
        writer.WriteInt32((int)Attributes);
        writer.WriteString(Name);
    }
}

/// <summary>Retains one exact GenericParam row for the selected declaring type or selected MethodDef.</summary>
/// <remarks>
/// The owner is an actual TypeDef or MethodDef token. This draft fact does not substitute a runtime generic argument
/// and does not infer a source declaration absent from the row.
/// </remarks>
public sealed class DumpLexicalGenericParameterRowFact : IEquatable<DumpLexicalGenericParameterRowFact>
{
    private readonly ImmutableArray<byte> canonicalBytes;
    private readonly int canonicalHashCode;

    private DumpLexicalGenericParameterRowFact(
        int genericParameterToken,
        int ownerToken,
        int number,
        GenericParameterAttributes attributes,
        string name)
    {
        GenericParameterToken = genericParameterToken;
        OwnerToken = ownerToken;
        Number = number;
        Attributes = attributes;
        Name = name;
        var writer = new CanonicalReplayEncoding.Writer("dump-lexical-generic-parameter-row", 1);
        WriteCanonical(writer);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
        canonicalHashCode = CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
    }

    /// <summary>Gets the non-nil GenericParam token.</summary>
    public int GenericParameterToken { get; }

    /// <summary>Gets the exact TypeDef or MethodDef owner token.</summary>
    public int OwnerToken { get; }

    /// <summary>Gets the zero-based flattened GenericParam number.</summary>
    public int Number { get; }

    /// <summary>Gets the exact GenericParam attribute bits.</summary>
    public GenericParameterAttributes Attributes { get; }

    /// <summary>Gets the exact metadata name, including an observed empty name.</summary>
    public string Name { get; }

    /// <summary>Gets a defensive copy of the draft canonical row bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => CanonicalReplayEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of <see cref="CanonicalBytes"/>.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one exact physical GenericParam-row fact.</summary>
    /// <param name="genericParameterToken">A non-nil GenericParam token.</param>
    /// <param name="ownerToken">The non-nil TypeDef or MethodDef owner token.</param>
    /// <param name="number">The zero-based physical parameter number.</param>
    /// <param name="attributes">The exact GenericParam attribute bits.</param>
    /// <param name="name">The exact metadata name; empty is retained.</param>
    /// <returns>An immutable draft GenericParam-row fact.</returns>
    public static DumpLexicalGenericParameterRowFact Create(
        int genericParameterToken,
        int ownerToken,
        int number,
        GenericParameterAttributes attributes,
        string name)
    {
        CanonicalReplayEncoding.ValidateMetadataToken(genericParameterToken, 0x2A, nameof(genericParameterToken));
        if (!CanonicalReplayEncoding.IsMetadataTokenForTable(ownerToken, 0x02) &&
            !CanonicalReplayEncoding.IsMetadataTokenForTable(ownerToken, 0x06))
        {
            throw new ArgumentOutOfRangeException(nameof(ownerToken), "A non-nil TypeDef or MethodDef owner is required.");
        }

        if (number is < 0 or > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(number));
        }

        DumpSelectedMethodLexicalEncoding.ValidateText(name, nameof(name), allowEmpty: true);
        return new DumpLexicalGenericParameterRowFact(
            genericParameterToken,
            ownerToken,
            number,
            attributes,
            name);
    }

    /// <summary>Determines content equality from canonical physical row bytes.</summary>
    /// <param name="other">The row to compare.</param>
    /// <returns><see langword="true"/> when every physical GenericParam field is equal.</returns>
    public bool Equals(DumpLexicalGenericParameterRowFact? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as DumpLexicalGenericParameterRowFact);

    /// <inheritdoc/>
    public override int GetHashCode() => canonicalHashCode;

    internal void WriteCanonical(CanonicalReplayEncoding.Writer writer)
    {
        writer.WriteInt32(GenericParameterToken);
        writer.WriteInt32(OwnerToken);
        writer.WriteInt32(Number);
        writer.WriteInt32((int)Attributes);
        writer.WriteString(Name);
    }
}

/// <summary>Retains one exact MethodDef row plus the Param rows in its declared ParamList interval.</summary>
/// <remarks>
/// This draft physical fact preserves raw signature bytes and declaration order. Declaring-type ownership is an
/// independently acquired relation; generated-name interpretation is performed only after the full catalog exists.
/// </remarks>
public sealed class DumpLexicalMethodDefinitionRowFact : IEquatable<DumpLexicalMethodDefinitionRowFact>
{
    private readonly ImmutableArray<byte> signature;
    private readonly ImmutableArray<DumpLexicalParameterRowFact> parameters;
    private readonly ImmutableArray<byte> canonicalBytes;
    private readonly int canonicalHashCode;

    private DumpLexicalMethodDefinitionRowFact(
        int methodDefinitionToken,
        int declaringTypeDefinitionToken,
        int relativeVirtualAddress,
        MethodAttributes attributes,
        MethodImplAttributes implementationAttributes,
        string name,
        ImmutableArray<byte> signature,
        int signatureGenericParameterCount,
        int signatureParameterCount,
        int signatureTypeNodeCount,
        int signatureTypeDepth,
        int parameterListStartRowId,
        ImmutableArray<DumpLexicalParameterRowFact> parameters)
    {
        MethodDefinitionToken = methodDefinitionToken;
        DeclaringTypeDefinitionToken = declaringTypeDefinitionToken;
        RelativeVirtualAddress = relativeVirtualAddress;
        Attributes = attributes;
        ImplementationAttributes = implementationAttributes;
        Name = name;
        this.signature = CanonicalReplayEncoding.Copy(signature);
        SignatureGenericParameterCount = signatureGenericParameterCount;
        SignatureParameterCount = signatureParameterCount;
        SignatureTypeNodeCount = signatureTypeNodeCount;
        SignatureTypeDepth = signatureTypeDepth;
        ParameterListStartRowId = parameterListStartRowId;
        this.parameters = CanonicalReplayEncoding.Copy(parameters);

        var writer = new CanonicalReplayEncoding.Writer("dump-lexical-method-definition-row", 1);
        WriteCanonical(writer);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
        canonicalHashCode = CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
    }

    /// <summary>Gets the non-nil MethodDef token.</summary>
    public int MethodDefinitionToken { get; }

    /// <summary>Gets the non-nil declaring TypeDef token acquired from physical MethodList ownership.</summary>
    public int DeclaringTypeDefinitionToken { get; }

    /// <summary>Gets the exact non-negative MethodDef relative virtual address, including zero.</summary>
    public int RelativeVirtualAddress { get; }

    /// <summary>Gets the exact MethodDef attribute bits.</summary>
    public MethodAttributes Attributes { get; }

    /// <summary>Gets the exact MethodDef implementation-attribute bits.</summary>
    public MethodImplAttributes ImplementationAttributes { get; }

    /// <summary>Gets the exact non-empty metadata method name.</summary>
    public string Name { get; }

    /// <summary>Gets a defensive copy of the complete MethodDef signature blob.</summary>
    public ImmutableArray<byte> Signature => CanonicalReplayEncoding.Copy(signature);

    /// <summary>Gets the exact parameter count decoded from the complete method signature.</summary>
    public int SignatureParameterCount { get; }

    /// <summary>Gets the exact generic-parameter count decoded from the complete method signature.</summary>
    public int SignatureGenericParameterCount { get; }

    /// <summary>Gets the aggregate structurally validated type-node count in the complete signature.</summary>
    public int SignatureTypeNodeCount { get; }

    /// <summary>Gets the maximum structurally validated recursive type depth in the complete signature.</summary>
    public int SignatureTypeDepth { get; }

    /// <summary>Gets the physical ParamList start RID, including the one-past-table sentinel.</summary>
    public int ParameterListStartRowId { get; }

    /// <summary>Gets a defensive copy of exact owned Param rows in physical RID order.</summary>
    public ImmutableArray<DumpLexicalParameterRowFact> Parameters => CanonicalReplayEncoding.Copy(parameters);

    /// <summary>Gets a defensive copy of the draft canonical row and owned-parameter bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => CanonicalReplayEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of <see cref="CanonicalBytes"/>.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one exact MethodDef row and its claimed physical Param rows.</summary>
    /// <param name="methodDefinitionToken">A non-nil MethodDef token.</param>
    /// <param name="declaringTypeDefinitionToken">The non-nil physically owning TypeDef token.</param>
    /// <param name="relativeVirtualAddress">The exact non-negative MethodDef RVA, including zero.</param>
    /// <param name="attributes">The exact MethodDef attribute bits.</param>
    /// <param name="implementationAttributes">The exact MethodDef implementation-attribute bits.</param>
    /// <param name="name">The exact non-empty metadata method name.</param>
    /// <param name="signature">The complete, initialized MethodDef signature bytes.</param>
    /// <param name="signatureParameterCount">The exact parameter count decoded from <paramref name="signature"/>.</param>
    /// <param name="parameterListStartRowId">The positive ParamList start RID, possibly one past the Param table.</param>
    /// <param name="parameters">An initialized physical set of Param rows claimed by this method.</param>
    /// <returns>An immutable draft MethodDef-row fact.</returns>
    /// <remarks>
    /// ParamList interval ownership is deliberately validated only by the complete catalog, where the next MethodDef
    /// and exact Param-table row count are available.
    /// </remarks>
    public static DumpLexicalMethodDefinitionRowFact Create(
        int methodDefinitionToken,
        int declaringTypeDefinitionToken,
        int relativeVirtualAddress,
        MethodAttributes attributes,
        MethodImplAttributes implementationAttributes,
        string name,
        ImmutableArray<byte> signature,
        int signatureParameterCount,
        int parameterListStartRowId,
        ImmutableArray<DumpLexicalParameterRowFact> parameters)
    {
        CanonicalReplayEncoding.ValidateMetadataToken(methodDefinitionToken, 0x06, nameof(methodDefinitionToken));
        CanonicalReplayEncoding.ValidateMetadataToken(
            declaringTypeDefinitionToken,
            0x02,
            nameof(declaringTypeDefinitionToken));
        if (relativeVirtualAddress < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(relativeVirtualAddress));
        }

        DumpSelectedMethodLexicalEncoding.ValidateText(name, nameof(name), allowEmpty: false);
        DumpSelectedMethodLexicalEncoding.ValidateBlob(
            signature,
            DumpSelectedMethodLexicalLimits.MaximumMethodSignatureByteCount,
            nameof(signature));
        if (signatureParameterCount is < 0 or > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(signatureParameterCount));
        }

        if (!BoundedEcmaSignatureProjection.TryDecodeMethodDefinition(
                signature.AsSpan(),
                DumpSelectedMethodLexicalLimits.MaximumMethodSignatureByteCount,
                DumpSelectedMethodLexicalLimits.MaximumSignatureTypeDepth,
                DumpSelectedMethodLexicalLimits.MaximumSignatureTypeNodeCount,
                out var decodedSignature) ||
            decodedSignature.ParameterCount != signatureParameterCount ||
            decodedSignature.HasThis == ((attributes & MethodAttributes.Static) != 0))
        {
            throw new ArgumentException(
                "The complete MethodDef signature must be fully consumed and agree with parameter count and staticness.",
                nameof(signature));
        }

        if (parameterListStartRowId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(parameterListStartRowId));
        }

        var normalizedParameters = DumpSelectedMethodLexicalEncoding.NormalizeRows(
            parameters,
            static row => row.ParameterToken,
            nameof(parameters));
        var sequences = new HashSet<int>();
        foreach (var parameter in normalizedParameters)
        {
            if (parameter.SequenceNumber > signatureParameterCount)
            {
                throw new ArgumentException(
                    "A Param sequence cannot exceed the complete method-signature parameter count.",
                    nameof(parameters));
            }

            if (!sequences.Add(parameter.SequenceNumber))
            {
                throw new ArgumentException("A MethodDef cannot own duplicate Param sequence numbers.", nameof(parameters));
            }
        }

        return new DumpLexicalMethodDefinitionRowFact(
            methodDefinitionToken,
            declaringTypeDefinitionToken,
            relativeVirtualAddress,
            attributes,
            implementationAttributes,
            name,
            signature,
            decodedSignature.GenericParameterCount,
            signatureParameterCount,
            decodedSignature.AggregateTypeCount,
            decodedSignature.MaximumObservedDepth,
            parameterListStartRowId,
            normalizedParameters);
    }

    /// <summary>Determines content equality from canonical row, signature, and owned-Param bytes.</summary>
    /// <param name="other">The method row to compare.</param>
    /// <returns><see langword="true"/> when every retained physical field is equal.</returns>
    public bool Equals(DumpLexicalMethodDefinitionRowFact? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as DumpLexicalMethodDefinitionRowFact);

    /// <inheritdoc/>
    public override int GetHashCode() => canonicalHashCode;

    internal void WriteCanonical(CanonicalReplayEncoding.Writer writer)
    {
        writer.WriteInt32(MethodDefinitionToken);
        writer.WriteInt32(DeclaringTypeDefinitionToken);
        writer.WriteInt32(RelativeVirtualAddress);
        writer.WriteInt32((int)Attributes);
        writer.WriteInt32((int)ImplementationAttributes);
        writer.WriteString(Name);
        writer.WriteLengthPrefixedBytes(signature.AsSpan());
        writer.WriteInt32(SignatureGenericParameterCount);
        writer.WriteInt32(SignatureParameterCount);
        writer.WriteInt32(SignatureTypeNodeCount);
        writer.WriteInt32(SignatureTypeDepth);
        writer.WriteInt32(ParameterListStartRowId);
        writer.WriteInt32(parameters.Length);
        foreach (var parameter in parameters)
        {
            parameter.WriteCanonical(writer);
        }
    }
}

/// <summary>Retains the complete StandAloneSig blob selected by one exact method body.</summary>
/// <remarks>
/// This draft fact structurally validates and fully consumes the complete local signature while retaining every byte.
/// It does not bind referenced type tokens or substitute generic arguments; those remain separate metadata operations.
/// </remarks>
public sealed class DumpLexicalStandaloneSignatureFact : IEquatable<DumpLexicalStandaloneSignatureFact>
{
    private readonly ImmutableArray<byte> signature;
    private readonly ImmutableArray<byte> canonicalBytes;
    private readonly int canonicalHashCode;

    private DumpLexicalStandaloneSignatureFact(
        int standaloneSignatureToken,
        int localSlotCount,
        int signatureTypeNodeCount,
        int signatureTypeDepth,
        ImmutableArray<byte> signature)
    {
        StandaloneSignatureToken = standaloneSignatureToken;
        LocalSlotCount = localSlotCount;
        SignatureTypeNodeCount = signatureTypeNodeCount;
        SignatureTypeDepth = signatureTypeDepth;
        this.signature = CanonicalReplayEncoding.Copy(signature);
        var writer = new CanonicalReplayEncoding.Writer("dump-lexical-standalone-signature", 1);
        writer.WriteInt32(standaloneSignatureToken);
        writer.WriteInt32(localSlotCount);
        writer.WriteInt32(signatureTypeNodeCount);
        writer.WriteInt32(signatureTypeDepth);
        writer.WriteLengthPrefixedBytes(signature.AsSpan());
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
        canonicalHashCode = CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
    }

    /// <summary>Gets the non-nil StandAloneSig token selected by the method body.</summary>
    public int StandaloneSignatureToken { get; }

    /// <summary>Gets the exact local-slot count decoded from the complete signature.</summary>
    public int LocalSlotCount { get; }

    /// <summary>Gets the aggregate structurally validated type-node count in the complete signature.</summary>
    public int SignatureTypeNodeCount { get; }

    /// <summary>Gets the maximum structurally validated recursive type depth in the complete signature.</summary>
    public int SignatureTypeDepth { get; }

    /// <summary>Gets a defensive copy of the complete StandAloneSig bytes.</summary>
    public ImmutableArray<byte> Signature => CanonicalReplayEncoding.Copy(signature);

    /// <summary>Gets a defensive copy of the draft canonical signature fact bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => CanonicalReplayEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of <see cref="CanonicalBytes"/>.</summary>
    public string Sha256 { get; }

    /// <summary>Creates an exact complete local-variable StandAloneSig fact.</summary>
    /// <param name="standaloneSignatureToken">A non-nil StandAloneSig token.</param>
    /// <param name="localSlotCount">The exact non-negative slot count decoded by the metadata reader.</param>
    /// <param name="signature">The complete initialized StandAloneSig blob.</param>
    /// <returns>An immutable draft local-signature fact.</returns>
    /// <remarks>
    /// The encoded count must be canonical and equal <paramref name="localSlotCount"/>. Every type node is validated
    /// structurally and the blob must be fully consumed; metadata token binding remains a later operation.
    /// </remarks>
    public static DumpLexicalStandaloneSignatureFact Create(
        int standaloneSignatureToken,
        int localSlotCount,
        ImmutableArray<byte> signature)
    {
        CanonicalReplayEncoding.ValidateMetadataToken(
            standaloneSignatureToken,
            0x11,
            nameof(standaloneSignatureToken));
        DumpSelectedMethodLexicalEncoding.ValidateBlob(
            signature,
            DumpSelectedMethodLexicalLimits.MaximumStandAloneSignatureByteCount,
            nameof(signature));
        if (localSlotCount is < 0 or > DumpSelectedMethodLexicalLimits.MaximumLocalSlotCount)
        {
            throw new ArgumentOutOfRangeException(nameof(localSlotCount));
        }

        if (!BoundedEcmaSignatureProjection.TryDecodeLocal(
                signature.AsSpan(),
                DumpSelectedMethodLexicalLimits.MaximumStandAloneSignatureByteCount,
                DumpSelectedMethodLexicalLimits.MaximumSignatureTypeDepth,
                DumpSelectedMethodLexicalLimits.MaximumSignatureTypeNodeCount,
                out var decodedSignature) ||
            decodedSignature.LocalSlotCount != localSlotCount)
        {
            throw new ArgumentException(
                "The complete blob must be a fully consumed local signature matching the declared slot count.",
                nameof(signature));
        }

        return new DumpLexicalStandaloneSignatureFact(
            standaloneSignatureToken,
            localSlotCount,
            decodedSignature.AggregateTypeCount,
            decodedSignature.MaximumObservedDepth,
            signature);
    }

    /// <summary>Determines content equality from canonical token, count, and complete signature bytes.</summary>
    /// <param name="other">The signature fact to compare.</param>
    /// <returns><see langword="true"/> when all retained physical data is equal.</returns>
    public bool Equals(DumpLexicalStandaloneSignatureFact? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as DumpLexicalStandaloneSignatureFact);

    /// <inheritdoc/>
    public override int GetHashCode() => canonicalHashCode;
}

/// <summary>Retains one physical Portable-PDB LocalScope row and its half-open IL range.</summary>
/// <remarks>
/// LocalScope has no parent column. This draft fact therefore records no parent or nesting depth; active containment is
/// derived later from ranges and the selected IL offset while disjoint and inactive rows remain present.
/// </remarks>
public sealed class DumpLexicalLocalScopeRowFact : IEquatable<DumpLexicalLocalScopeRowFact>
{
    private readonly ImmutableArray<byte> canonicalBytes;
    private readonly int canonicalHashCode;

    private DumpLexicalLocalScopeRowFact(
        int localScopeToken,
        int methodDefinitionToken,
        int? importScopeToken,
        int startOffset,
        int length,
        int variableListStartRowId,
        int constantListStartRowId)
    {
        LocalScopeToken = localScopeToken;
        MethodDefinitionToken = methodDefinitionToken;
        ImportScopeToken = importScopeToken;
        StartOffset = startOffset;
        Length = length;
        VariableListStartRowId = variableListStartRowId;
        ConstantListStartRowId = constantListStartRowId;
        var writer = new CanonicalReplayEncoding.Writer("dump-lexical-local-scope-row", 1);
        WriteCanonical(writer);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
        canonicalHashCode = CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
    }

    /// <summary>Gets the non-nil LocalScope token.</summary>
    public int LocalScopeToken { get; }

    /// <summary>Gets the non-nil MethodDef token physically named by the LocalScope row.</summary>
    public int MethodDefinitionToken { get; }

    /// <summary>Gets the non-nil ImportScope token, or null for a nil handle.</summary>
    public int? ImportScopeToken { get; }

    /// <summary>Gets the inclusive non-negative IL start offset.</summary>
    public int StartOffset { get; }

    /// <summary>Gets the positive IL range length.</summary>
    public int Length { get; }

    /// <summary>Gets the exclusive, nonoverflowing IL end offset.</summary>
    public int EndOffset => StartOffset + Length;

    /// <summary>Gets the physical LocalVariable-list start RID from this LocalScope row.</summary>
    public int VariableListStartRowId { get; }

    /// <summary>Gets the physical LocalConstant-list start RID from this LocalScope row.</summary>
    public int ConstantListStartRowId { get; }

    /// <summary>Gets a defensive copy of the draft canonical physical row bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => CanonicalReplayEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of <see cref="CanonicalBytes"/>.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one exact physical LocalScope-row fact without a fabricated parent relation.</summary>
    /// <param name="localScopeToken">A non-nil LocalScope token.</param>
    /// <param name="methodDefinitionToken">The non-nil physically associated MethodDef token.</param>
    /// <param name="importScopeToken">The non-nil ImportScope token, or null for nil.</param>
    /// <param name="startOffset">The inclusive non-negative IL start offset.</param>
    /// <param name="length">The positive, nonoverflowing IL range length.</param>
    /// <param name="variableListStartRowId">
    /// The positive physical LocalVariable-list start RID. Its exclusive end is deliberately absent from this raw row
    /// and is derived only after the next complete physical LocalScope row or LocalVariable-table end is known.
    /// </param>
    /// <param name="constantListStartRowId">
    /// The positive physical LocalConstant-list start RID. Its exclusive end is deliberately absent from this raw row
    /// and is derived only after the next complete physical LocalScope row or LocalConstant-table end is known.
    /// </param>
    /// <returns>An immutable draft LocalScope-row fact.</returns>
    public static DumpLexicalLocalScopeRowFact Create(
        int localScopeToken,
        int methodDefinitionToken,
        int? importScopeToken,
        int startOffset,
        int length,
        int variableListStartRowId,
        int constantListStartRowId)
    {
        CanonicalReplayEncoding.ValidateMetadataToken(localScopeToken, 0x32, nameof(localScopeToken));
        CanonicalReplayEncoding.ValidateMetadataToken(methodDefinitionToken, 0x06, nameof(methodDefinitionToken));
        if (importScopeToken.HasValue)
        {
            CanonicalReplayEncoding.ValidateMetadataToken(importScopeToken.Value, 0x35, nameof(importScopeToken));
        }

        if (startOffset < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(startOffset));
        }

        if (length <= 0 || startOffset > int.MaxValue - length)
        {
            throw new ArgumentOutOfRangeException(nameof(length));
        }

        if (variableListStartRowId <= 0 || variableListStartRowId > 0x01000000)
        {
            throw new ArgumentOutOfRangeException(nameof(variableListStartRowId));
        }

        if (constantListStartRowId <= 0 || constantListStartRowId > 0x01000000)
        {
            throw new ArgumentOutOfRangeException(nameof(constantListStartRowId));
        }

        return new DumpLexicalLocalScopeRowFact(
            localScopeToken,
            methodDefinitionToken,
            importScopeToken,
            startOffset,
            length,
            variableListStartRowId,
            constantListStartRowId);
    }

    /// <summary>Determines whether the physical half-open range contains one IL offset.</summary>
    /// <param name="ilOffset">The IL offset to test.</param>
    /// <returns><see langword="true"/> when the offset lies in this row's range.</returns>
    /// <remarks>This draft helper derives containment only; it does not infer scope ancestry.</remarks>
    public bool Contains(int ilOffset) => ilOffset >= StartOffset && ilOffset < EndOffset;

    /// <summary>Determines content equality from canonical physical row bytes.</summary>
    /// <param name="other">The scope row to compare.</param>
    /// <returns><see langword="true"/> when token, method, import, range, and both physical list starts are equal.</returns>
    public bool Equals(DumpLexicalLocalScopeRowFact? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as DumpLexicalLocalScopeRowFact);

    /// <inheritdoc/>
    public override int GetHashCode() => canonicalHashCode;

    internal void WriteCanonical(CanonicalReplayEncoding.Writer writer)
    {
        writer.WriteInt32(LocalScopeToken);
        writer.WriteInt32(MethodDefinitionToken);
        writer.WriteBoolean(ImportScopeToken.HasValue);
        if (ImportScopeToken.HasValue)
        {
            writer.WriteInt32(ImportScopeToken.Value);
        }

        writer.WriteInt32(StartOffset);
        writer.WriteInt32(Length);
        writer.WriteInt32(VariableListStartRowId);
        writer.WriteInt32(ConstantListStartRowId);
    }
}

/// <summary>Retains one physical Portable-PDB LocalVariable row and its owning LocalScope row.</summary>
/// <remarks>
/// The LocalScope association comes from the exact row-list interval traversed by the producer. This draft fact keeps
/// unnamed rows and slot reuse; active versus inactive disposition is derived only after scope selection.
/// </remarks>
public sealed class DumpLexicalLocalVariableRowFact : IEquatable<DumpLexicalLocalVariableRowFact>
{
    private readonly ImmutableArray<byte> canonicalBytes;
    private readonly int canonicalHashCode;

    private DumpLexicalLocalVariableRowFact(
        int localVariableToken,
        int localScopeToken,
        int slotIndex,
        LocalVariableAttributes attributes,
        string name)
    {
        LocalVariableToken = localVariableToken;
        LocalScopeToken = localScopeToken;
        SlotIndex = slotIndex;
        Attributes = attributes;
        Name = name;
        var writer = new CanonicalReplayEncoding.Writer("dump-lexical-local-variable-row", 1);
        WriteCanonical(writer);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
        canonicalHashCode = CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
    }

    /// <summary>Gets the non-nil LocalVariable token.</summary>
    public int LocalVariableToken { get; }

    /// <summary>Gets the physical LocalScope token whose variable-list interval owns this row.</summary>
    public int LocalScopeToken { get; }

    /// <summary>Gets the exact non-negative StandAloneSig local slot index.</summary>
    public int SlotIndex { get; }

    /// <summary>Gets the exact LocalVariable attribute bits.</summary>
    public LocalVariableAttributes Attributes { get; }

    /// <summary>Gets the exact Portable-PDB name, including an observed empty name.</summary>
    public string Name { get; }

    /// <summary>Gets a defensive copy of the draft canonical physical row bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => CanonicalReplayEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of <see cref="CanonicalBytes"/>.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one exact LocalVariable-row fact with its physical scope ownership.</summary>
    /// <param name="localVariableToken">A non-nil LocalVariable token.</param>
    /// <param name="localScopeToken">The non-nil physically owning LocalScope token.</param>
    /// <param name="slotIndex">The non-negative StandAloneSig slot index.</param>
    /// <param name="attributes">The exact LocalVariable attribute bits.</param>
    /// <param name="name">The exact Portable-PDB name; empty is retained.</param>
    /// <returns>An immutable draft LocalVariable-row fact.</returns>
    public static DumpLexicalLocalVariableRowFact Create(
        int localVariableToken,
        int localScopeToken,
        int slotIndex,
        LocalVariableAttributes attributes,
        string name)
    {
        CanonicalReplayEncoding.ValidateMetadataToken(localVariableToken, 0x33, nameof(localVariableToken));
        CanonicalReplayEncoding.ValidateMetadataToken(localScopeToken, 0x32, nameof(localScopeToken));
        if (slotIndex is < 0 or > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(slotIndex));
        }

        DumpSelectedMethodLexicalEncoding.ValidateText(name, nameof(name), allowEmpty: true);
        return new DumpLexicalLocalVariableRowFact(
            localVariableToken,
            localScopeToken,
            slotIndex,
            attributes,
            name);
    }

    /// <summary>Determines content equality from canonical physical row bytes.</summary>
    /// <param name="other">The variable row to compare.</param>
    /// <returns><see langword="true"/> when token, scope, slot, attributes, and name are equal.</returns>
    public bool Equals(DumpLexicalLocalVariableRowFact? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as DumpLexicalLocalVariableRowFact);

    /// <inheritdoc/>
    public override int GetHashCode() => canonicalHashCode;

    internal void WriteCanonical(CanonicalReplayEncoding.Writer writer)
    {
        writer.WriteInt32(LocalVariableToken);
        writer.WriteInt32(LocalScopeToken);
        writer.WriteInt32(SlotIndex);
        writer.WriteInt32((int)Attributes);
        writer.WriteString(Name);
    }
}

/// <summary>Retains one physical Portable-PDB LocalConstant row and its complete signature blob.</summary>
/// <remarks>
/// The LocalScope association comes from the physical row-list interval. This draft fact does not decode or coerce the
/// constant value and preserves inactive rows alongside active rows.
/// </remarks>
public sealed class DumpLexicalLocalConstantRowFact : IEquatable<DumpLexicalLocalConstantRowFact>
{
    private readonly ImmutableArray<byte> signature;
    private readonly ImmutableArray<byte> canonicalBytes;
    private readonly int canonicalHashCode;

    private DumpLexicalLocalConstantRowFact(
        int localConstantToken,
        int localScopeToken,
        string name,
        ImmutableArray<byte> signature)
    {
        LocalConstantToken = localConstantToken;
        LocalScopeToken = localScopeToken;
        Name = name;
        this.signature = CanonicalReplayEncoding.Copy(signature);
        var writer = new CanonicalReplayEncoding.Writer("dump-lexical-local-constant-row", 1);
        WriteCanonical(writer);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
        canonicalHashCode = CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
    }

    /// <summary>Gets the non-nil LocalConstant token.</summary>
    public int LocalConstantToken { get; }

    /// <summary>Gets the physical LocalScope token whose constant-list interval owns this row.</summary>
    public int LocalScopeToken { get; }

    /// <summary>Gets the exact Portable-PDB name, including an observed empty name.</summary>
    public string Name { get; }

    /// <summary>Gets a defensive copy of the complete LocalConstant signature blob.</summary>
    public ImmutableArray<byte> Signature => CanonicalReplayEncoding.Copy(signature);

    /// <summary>Gets a defensive copy of the draft canonical physical row bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => CanonicalReplayEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of <see cref="CanonicalBytes"/>.</summary>
    public string Sha256 { get; }

    /// <summary>Creates one exact LocalConstant-row fact with its physical scope ownership.</summary>
    /// <param name="localConstantToken">A non-nil LocalConstant token.</param>
    /// <param name="localScopeToken">The non-nil physically owning LocalScope token.</param>
    /// <param name="name">The exact Portable-PDB name; empty is retained.</param>
    /// <param name="signature">The complete initialized non-empty constant signature.</param>
    /// <returns>An immutable draft LocalConstant-row fact.</returns>
    public static DumpLexicalLocalConstantRowFact Create(
        int localConstantToken,
        int localScopeToken,
        string name,
        ImmutableArray<byte> signature)
    {
        CanonicalReplayEncoding.ValidateMetadataToken(localConstantToken, 0x34, nameof(localConstantToken));
        CanonicalReplayEncoding.ValidateMetadataToken(localScopeToken, 0x32, nameof(localScopeToken));
        DumpSelectedMethodLexicalEncoding.ValidateText(name, nameof(name), allowEmpty: true);
        DumpSelectedMethodLexicalEncoding.ValidateBlob(
            signature,
            DumpSelectedMethodLexicalLimits.MaximumLocalConstantSignatureByteCount,
            nameof(signature));
        return new DumpLexicalLocalConstantRowFact(localConstantToken, localScopeToken, name, signature);
    }

    /// <summary>Determines content equality from canonical physical row and signature bytes.</summary>
    /// <param name="other">The constant row to compare.</param>
    /// <returns><see langword="true"/> when token, scope, name, and complete signature are equal.</returns>
    public bool Equals(DumpLexicalLocalConstantRowFact? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as DumpLexicalLocalConstantRowFact);

    /// <inheritdoc/>
    public override int GetHashCode() => canonicalHashCode;

    internal void WriteCanonical(CanonicalReplayEncoding.Writer writer)
    {
        writer.WriteInt32(LocalConstantToken);
        writer.WriteInt32(LocalScopeToken);
        writer.WriteString(Name);
        writer.WriteLengthPrefixedBytes(signature.AsSpan());
    }
}

/// <summary>Retains one exact MethodDef row whose raw generated name has current local-function form.</summary>
/// <remarks>
/// This draft fact derives only name components and canonical ordinals from the raw MethodDef name. It deliberately
/// exposes no parent MethodDef token, LocalScope token, or source-scope edge because metadata contains none of those
/// relations for a generated local-function method.
/// </remarks>
public sealed class DumpGeneratedLocalFunctionRowFact : IEquatable<DumpGeneratedLocalFunctionRowFact>
{
    private readonly ImmutableArray<byte> canonicalBytes;
    private readonly int canonicalHashCode;

    private DumpGeneratedLocalFunctionRowFact(
        DumpLexicalMethodDefinitionRowFact method,
        string containingMethodName,
        string localFunctionName,
        int? methodOrdinal,
        int? methodGeneration,
        int localFunctionOrdinal,
        int localFunctionGeneration)
    {
        Method = method;
        ContainingMethodName = containingMethodName;
        LocalFunctionName = localFunctionName;
        MethodOrdinal = methodOrdinal;
        MethodGeneration = methodGeneration;
        LocalFunctionOrdinal = localFunctionOrdinal;
        LocalFunctionGeneration = localFunctionGeneration;
        var writer = new CanonicalReplayEncoding.Writer("dump-generated-local-function-row", 1);
        writer.WriteLengthPrefixedBytes(method.CanonicalBytes.AsSpan());
        writer.WriteString(containingMethodName);
        writer.WriteString(localFunctionName);
        writer.WriteBoolean(methodOrdinal.HasValue);
        if (methodOrdinal.HasValue)
        {
            writer.WriteInt32(methodOrdinal.Value);
        }
        writer.WriteBoolean(methodGeneration.HasValue);
        if (methodGeneration.HasValue)
        {
            writer.WriteInt32(methodGeneration.Value);
        }
        writer.WriteInt32(localFunctionOrdinal);
        writer.WriteInt32(localFunctionGeneration);
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
        canonicalHashCode = CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
    }

    /// <summary>Gets the complete physical MethodDef and owned Param-row fact.</summary>
    public DumpLexicalMethodDefinitionRowFact Method { get; }

    /// <summary>Gets the balanced-bracket containing method name decoded from the raw generated name.</summary>
    public string ContainingMethodName { get; }

    /// <summary>Gets the non-empty local-function source name decoded after literal <c>g__</c>.</summary>
    public string LocalFunctionName { get; }

    /// <summary>
    /// Gets the non-negative containing-method ordinal, or null for the generator-permitted form that omits it.
    /// </summary>
    public int? MethodOrdinal { get; }

    /// <summary>
    /// Gets the non-negative containing-method generation, or null when the generated form omits the method ordinal
    /// and therefore carries no method-generation component.
    /// </summary>
    public int? MethodGeneration { get; }

    /// <summary>Gets the non-negative local-function ordinal.</summary>
    public int LocalFunctionOrdinal { get; }

    /// <summary>Gets the non-negative local-function generation; zero means the canonical suffix is absent.</summary>
    public int LocalFunctionGeneration { get; }

    /// <summary>Gets a defensive copy of the draft canonical physical-and-derived fact bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => CanonicalReplayEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of <see cref="CanonicalBytes"/>.</summary>
    public string Sha256 { get; }

    /// <summary>Determines content equality from the canonical raw row and exact generated-name decomposition.</summary>
    /// <param name="other">The generated row fact to compare.</param>
    /// <returns><see langword="true"/> when the physical row and every parsed component are equal.</returns>
    public bool Equals(DumpGeneratedLocalFunctionRowFact? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as DumpGeneratedLocalFunctionRowFact);

    /// <inheritdoc/>
    public override int GetHashCode() => canonicalHashCode;

    internal static bool TryCreate(
        DumpLexicalMethodDefinitionRowFact method,
        out DumpGeneratedLocalFunctionRowFact? fact)
    {
        if (!DumpSelectedMethodLexicalEncoding.TryParseGeneratedLocalFunctionName(
                method.Name,
                out var containingMethodName,
                out var localFunctionName,
                out var methodOrdinal,
                out var methodGeneration,
                out var localFunctionOrdinal,
                out var localFunctionGeneration))
        {
            fact = null;
            return false;
        }

        fact = new DumpGeneratedLocalFunctionRowFact(
            method,
            containingMethodName,
            localFunctionName,
            methodOrdinal,
            methodGeneration,
            localFunctionOrdinal,
            localFunctionGeneration);
        return true;
    }
}

/// <summary>
/// Freezes one exact selected method, complete module method/parameter catalogs, generic rows, local signature, the
/// complete Portable-PDB lexical tables, and their selected-method projections.
/// </summary>
/// <remarks>
/// This is a draft physical-evidence contract. Active scopes are derived from exact half-open IL ranges and must
/// reproduce the existing W7 active-scope projection. Inactive and disjoint rows remain observable. Generated local
/// functions are raw-name-derived facts only and acquire no fabricated parent or source-scope relation.
/// </remarks>
public sealed class DumpSelectedMethodLexicalFacts : IEquatable<DumpSelectedMethodLexicalFacts>
{
    private readonly ImmutableArray<DumpLexicalMethodDefinitionRowFact> allMethods;
    private readonly ImmutableArray<DumpLexicalGenericParameterRowFact> declaringTypeGenericParameters;
    private readonly ImmutableArray<DumpLexicalGenericParameterRowFact> methodGenericParameters;
    private readonly ImmutableArray<DumpLexicalLocalScopeRowFact> localScopeTable;
    private readonly ImmutableArray<DumpLexicalLocalScopeRowFact> allLocalScopes;
    private readonly ImmutableArray<DumpLexicalLocalScopeRowFact> activeLocalScopes;
    private readonly ImmutableArray<DumpLexicalLocalScopeRowFact> inactiveLocalScopes;
    private readonly ImmutableArray<DumpLexicalLocalVariableRowFact> localVariableTable;
    private readonly ImmutableArray<DumpLexicalLocalVariableRowFact> allLocalVariables;
    private readonly ImmutableArray<DumpLexicalLocalVariableRowFact> activeLocalVariables;
    private readonly ImmutableArray<DumpLexicalLocalVariableRowFact> inactiveLocalVariables;
    private readonly ImmutableArray<DumpLexicalLocalConstantRowFact> localConstantTable;
    private readonly ImmutableArray<DumpLexicalLocalConstantRowFact> allLocalConstants;
    private readonly ImmutableArray<DumpLexicalLocalConstantRowFact> activeLocalConstants;
    private readonly ImmutableArray<DumpLexicalLocalConstantRowFact> inactiveLocalConstants;
    private readonly ImmutableArray<DumpGeneratedLocalFunctionRowFact> generatedLocalFunctions;
    private readonly ImmutableArray<int> inactiveOnlyLocalSlotIndices;
    private readonly ImmutableArray<int> unaccountedLocalSlotIndices;
    private readonly ImmutableArray<byte> canonicalBytes;
    private readonly int canonicalHashCode;

    private DumpSelectedMethodLexicalFacts(
        DumpPortablePdbContextFacts portablePdbContext,
        DumpSelectedMethodLexicalSourceEnds sourceEnds,
        ImmutableArray<DumpLexicalMethodDefinitionRowFact> allMethods,
        DumpLexicalMethodDefinitionRowFact selectedMethod,
        ImmutableArray<DumpLexicalGenericParameterRowFact> declaringTypeGenericParameters,
        ImmutableArray<DumpLexicalGenericParameterRowFact> methodGenericParameters,
        DumpLexicalStandaloneSignatureFact? localSignature,
        ImmutableArray<DumpLexicalLocalScopeRowFact> localScopeTable,
        ImmutableArray<DumpLexicalLocalScopeRowFact> allLocalScopes,
        ImmutableArray<DumpLexicalLocalScopeRowFact> activeLocalScopes,
        ImmutableArray<DumpLexicalLocalScopeRowFact> inactiveLocalScopes,
        ImmutableArray<DumpLexicalLocalVariableRowFact> localVariableTable,
        ImmutableArray<DumpLexicalLocalVariableRowFact> allLocalVariables,
        ImmutableArray<DumpLexicalLocalVariableRowFact> activeLocalVariables,
        ImmutableArray<DumpLexicalLocalVariableRowFact> inactiveLocalVariables,
        ImmutableArray<DumpLexicalLocalConstantRowFact> localConstantTable,
        ImmutableArray<DumpLexicalLocalConstantRowFact> allLocalConstants,
        ImmutableArray<DumpLexicalLocalConstantRowFact> activeLocalConstants,
        ImmutableArray<DumpLexicalLocalConstantRowFact> inactiveLocalConstants,
        ImmutableArray<DumpGeneratedLocalFunctionRowFact> generatedLocalFunctions,
        ImmutableArray<int> inactiveOnlyLocalSlotIndices,
        ImmutableArray<int> unaccountedLocalSlotIndices)
    {
        PortablePdbContext = portablePdbContext;
        SourceEnds = sourceEnds;
        this.allMethods = CanonicalReplayEncoding.Copy(allMethods);
        SelectedMethod = selectedMethod;
        this.declaringTypeGenericParameters = CanonicalReplayEncoding.Copy(declaringTypeGenericParameters);
        this.methodGenericParameters = CanonicalReplayEncoding.Copy(methodGenericParameters);
        LocalSignature = localSignature;
        this.localScopeTable = CanonicalReplayEncoding.Copy(localScopeTable);
        this.allLocalScopes = CanonicalReplayEncoding.Copy(allLocalScopes);
        this.activeLocalScopes = CanonicalReplayEncoding.Copy(activeLocalScopes);
        this.inactiveLocalScopes = CanonicalReplayEncoding.Copy(inactiveLocalScopes);
        this.localVariableTable = CanonicalReplayEncoding.Copy(localVariableTable);
        this.allLocalVariables = CanonicalReplayEncoding.Copy(allLocalVariables);
        this.activeLocalVariables = CanonicalReplayEncoding.Copy(activeLocalVariables);
        this.inactiveLocalVariables = CanonicalReplayEncoding.Copy(inactiveLocalVariables);
        this.localConstantTable = CanonicalReplayEncoding.Copy(localConstantTable);
        this.allLocalConstants = CanonicalReplayEncoding.Copy(allLocalConstants);
        this.activeLocalConstants = CanonicalReplayEncoding.Copy(activeLocalConstants);
        this.inactiveLocalConstants = CanonicalReplayEncoding.Copy(inactiveLocalConstants);
        this.generatedLocalFunctions = CanonicalReplayEncoding.Copy(generatedLocalFunctions);
        this.inactiveOnlyLocalSlotIndices = CanonicalReplayEncoding.Copy(inactiveOnlyLocalSlotIndices);
        this.unaccountedLocalSlotIndices = CanonicalReplayEncoding.Copy(unaccountedLocalSlotIndices);

        var writer = new CanonicalReplayEncoding.Writer("dump-selected-method-lexical-facts", 1);
        writer.WriteLengthPrefixedBytes(portablePdbContext.CanonicalBytes.AsSpan());
        writer.WriteLengthPrefixedBytes(sourceEnds.CanonicalBytes.AsSpan());
        writer.WriteInt32(allMethods.Length);
        foreach (var method in allMethods)
        {
            writer.WriteLengthPrefixedBytes(method.CanonicalBytes.AsSpan());
        }
        writer.WriteInt32(declaringTypeGenericParameters.Length);
        foreach (var parameter in declaringTypeGenericParameters)
        {
            parameter.WriteCanonical(writer);
        }
        writer.WriteInt32(methodGenericParameters.Length);
        foreach (var parameter in methodGenericParameters)
        {
            parameter.WriteCanonical(writer);
        }
        writer.WriteBoolean(localSignature is not null);
        if (localSignature is not null)
        {
            writer.WriteLengthPrefixedBytes(localSignature.CanonicalBytes.AsSpan());
        }
        writer.WriteInt32(localScopeTable.Length);
        foreach (var scope in localScopeTable)
        {
            scope.WriteCanonical(writer);
        }
        writer.WriteInt32(localVariableTable.Length);
        foreach (var variable in localVariableTable)
        {
            variable.WriteCanonical(writer);
        }
        writer.WriteInt32(localConstantTable.Length);
        foreach (var constant in localConstantTable)
        {
            constant.WriteCanonical(writer);
        }
        writer.WriteInt32(activeLocalScopes.Length);
        foreach (var scope in activeLocalScopes)
        {
            writer.WriteInt32(scope.LocalScopeToken);
        }
        writer.WriteInt32(generatedLocalFunctions.Length);
        foreach (var generated in generatedLocalFunctions)
        {
            writer.WriteLengthPrefixedBytes(generated.CanonicalBytes.AsSpan());
        }
        writer.WriteInt32(inactiveOnlyLocalSlotIndices.Length);
        foreach (var slot in inactiveOnlyLocalSlotIndices)
        {
            writer.WriteInt32(slot);
        }
        writer.WriteInt32(unaccountedLocalSlotIndices.Length);
        foreach (var slot in unaccountedLocalSlotIndices)
        {
            writer.WriteInt32(slot);
        }
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
        canonicalHashCode = CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
    }

    /// <summary>Gets the exact PDB artifact, selected frame, active scopes, and import context used by the projection.</summary>
    public DumpPortablePdbContextFacts PortablePdbContext { get; }

    /// <summary>Gets the exact bounded-enumeration source ends proved before these facts were materialized.</summary>
    public DumpSelectedMethodLexicalSourceEnds SourceEnds { get; }

    /// <summary>Gets the exact complete MethodDef-table row count.</summary>
    public int MethodDefinitionTableRowCount => SourceEnds.MethodDefinitionRowCount;

    /// <summary>Gets the exact complete Param-table row count.</summary>
    public int ParameterTableRowCount => SourceEnds.ParameterRowCount;

    /// <summary>Gets every MethodDef row in physical RID order as a defensive copy.</summary>
    public ImmutableArray<DumpLexicalMethodDefinitionRowFact> AllMethods => CanonicalReplayEncoding.Copy(allMethods);

    /// <summary>Gets the exact MethodDef row selected by the frame.</summary>
    public DumpLexicalMethodDefinitionRowFact SelectedMethod { get; }

    /// <summary>Gets selected declaring-TypeDef GenericParam rows in flattened-number order.</summary>
    public ImmutableArray<DumpLexicalGenericParameterRowFact> DeclaringTypeGenericParameters =>
        CanonicalReplayEncoding.Copy(declaringTypeGenericParameters);

    /// <summary>Gets selected MethodDef GenericParam rows in number order.</summary>
    public ImmutableArray<DumpLexicalGenericParameterRowFact> MethodGenericParameters =>
        CanonicalReplayEncoding.Copy(methodGenericParameters);

    /// <summary>Gets the complete selected StandAloneSig fact, or null when the body declares no local signature.</summary>
    public DumpLexicalStandaloneSignatureFact? LocalSignature { get; }

    /// <summary>Gets the complete physical Portable-PDB LocalScope table in RID order.</summary>
    /// <remarks>
    /// This draft catalog includes other methods so the selected method's final list intervals are proved from the next
    /// physical row rather than a selected-method-only boundary claim.
    /// </remarks>
    public ImmutableArray<DumpLexicalLocalScopeRowFact> LocalScopeTable =>
        CanonicalReplayEncoding.Copy(localScopeTable);

    /// <summary>Gets every physical selected-method LocalScope row in RID order.</summary>
    public ImmutableArray<DumpLexicalLocalScopeRowFact> AllLocalScopes => CanonicalReplayEncoding.Copy(allLocalScopes);

    /// <summary>Gets the IL-derived containing LocalScope chain in outer-to-inner range order.</summary>
    public ImmutableArray<DumpLexicalLocalScopeRowFact> ActiveLocalScopes =>
        CanonicalReplayEncoding.Copy(activeLocalScopes);

    /// <summary>Gets every selected-method LocalScope row not containing the selected IL offset.</summary>
    public ImmutableArray<DumpLexicalLocalScopeRowFact> InactiveLocalScopes =>
        CanonicalReplayEncoding.Copy(inactiveLocalScopes);

    /// <summary>Gets the complete physical Portable-PDB LocalVariable table in RID order.</summary>
    public ImmutableArray<DumpLexicalLocalVariableRowFact> LocalVariableTable =>
        CanonicalReplayEncoding.Copy(localVariableTable);

    /// <summary>Gets every physical selected-method LocalVariable row in RID order.</summary>
    public ImmutableArray<DumpLexicalLocalVariableRowFact> AllLocalVariables =>
        CanonicalReplayEncoding.Copy(allLocalVariables);

    /// <summary>Gets LocalVariable rows owned by an active LocalScope, retaining outer-to-inner shadow candidates.</summary>
    public ImmutableArray<DumpLexicalLocalVariableRowFact> ActiveLocalVariables =>
        CanonicalReplayEncoding.Copy(activeLocalVariables);

    /// <summary>Gets LocalVariable rows owned only by inactive LocalScopes.</summary>
    public ImmutableArray<DumpLexicalLocalVariableRowFact> InactiveLocalVariables =>
        CanonicalReplayEncoding.Copy(inactiveLocalVariables);

    /// <summary>Gets the complete physical Portable-PDB LocalConstant table in RID order.</summary>
    public ImmutableArray<DumpLexicalLocalConstantRowFact> LocalConstantTable =>
        CanonicalReplayEncoding.Copy(localConstantTable);

    /// <summary>Gets every physical selected-method LocalConstant row in RID order.</summary>
    public ImmutableArray<DumpLexicalLocalConstantRowFact> AllLocalConstants =>
        CanonicalReplayEncoding.Copy(allLocalConstants);

    /// <summary>Gets LocalConstant rows owned by an active LocalScope.</summary>
    public ImmutableArray<DumpLexicalLocalConstantRowFact> ActiveLocalConstants =>
        CanonicalReplayEncoding.Copy(activeLocalConstants);

    /// <summary>Gets LocalConstant rows owned only by inactive LocalScopes.</summary>
    public ImmutableArray<DumpLexicalLocalConstantRowFact> InactiveLocalConstants =>
        CanonicalReplayEncoding.Copy(inactiveLocalConstants);

    /// <summary>Derives one LocalScope row's exclusive LocalVariable-list end from the complete physical table.</summary>
    /// <param name="localScopeToken">A LocalScope token retained in <see cref="LocalScopeTable"/>.</param>
    /// <returns>
    /// The next physical LocalScope row's LocalVariable-list start, or the complete LocalVariable-table end for the
    /// final physical scope row.
    /// </returns>
    /// <remarks>
    /// This draft query never accepts an independently supplied list end. Acquisition has already checked the derived
    /// interval against every complete physical LocalVariable row and its range-derived owner.
    /// </remarks>
    public int GetLocalVariableListEndRowIdExclusive(int localScopeToken) =>
        GetDerivedListEndRowIdExclusive(
            localScopeToken,
            static scope => scope.VariableListStartRowId,
            localVariableTable.Length,
            nameof(localScopeToken));

    /// <summary>Derives one LocalScope row's exclusive LocalConstant-list end from the complete physical table.</summary>
    /// <param name="localScopeToken">A LocalScope token retained in <see cref="LocalScopeTable"/>.</param>
    /// <returns>
    /// The next physical LocalScope row's LocalConstant-list start, or the complete LocalConstant-table end for the
    /// final physical scope row.
    /// </returns>
    /// <remarks>
    /// This draft query never accepts an independently supplied list end. Acquisition has already checked the derived
    /// interval against every complete physical LocalConstant row and its range-derived owner.
    /// </remarks>
    public int GetLocalConstantListEndRowIdExclusive(int localScopeToken) =>
        GetDerivedListEndRowIdExclusive(
            localScopeToken,
            static scope => scope.ConstantListStartRowId,
            localConstantTable.Length,
            nameof(localScopeToken));

    /// <summary>Gets raw-name-derived generated local-function rows in MethodDef RID order.</summary>
    public ImmutableArray<DumpGeneratedLocalFunctionRowFact> GeneratedLocalFunctions =>
        CanonicalReplayEncoding.Copy(generatedLocalFunctions);

    /// <summary>Gets slots named only by inactive scopes and therefore unavailable at the selected IL offset.</summary>
    public ImmutableArray<int> InactiveOnlyLocalSlotIndices =>
        CanonicalReplayEncoding.Copy(inactiveOnlyLocalSlotIndices);

    /// <summary>Gets StandAloneSig slots having no LocalVariable row in any retained scope.</summary>
    public ImmutableArray<int> UnaccountedLocalSlotIndices =>
        CanonicalReplayEncoding.Copy(unaccountedLocalSlotIndices);

    /// <summary>Gets a defensive copy of the complete draft canonical fact bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => CanonicalReplayEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of <see cref="CanonicalBytes"/>.</summary>
    public string Sha256 { get; }

    /// <summary>Determines content equality from complete physical inputs and deterministic derived projections.</summary>
    /// <param name="other">The exact lexical fact set to compare.</param>
    /// <returns><see langword="true"/> when every replay-significant fact is equal.</returns>
    public bool Equals(DumpSelectedMethodLexicalFacts? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as DumpSelectedMethodLexicalFacts);

    /// <inheritdoc/>
    public override int GetHashCode() => canonicalHashCode;

    private int GetDerivedListEndRowIdExclusive(
        int localScopeToken,
        Func<DumpLexicalLocalScopeRowFact, int> startSelector,
        int tableRowCount,
        string parameterName)
    {
        CanonicalReplayEncoding.ValidateMetadataToken(localScopeToken, 0x32, parameterName);
        for (var index = 0; index < localScopeTable.Length; index++)
        {
            if (localScopeTable[index].LocalScopeToken == localScopeToken)
            {
                return index + 1 < localScopeTable.Length
                    ? startSelector(localScopeTable[index + 1])
                    : checked(tableRowCount + 1);
            }
        }

        throw new ArgumentException("The LocalScope token is absent from the complete physical table.", parameterName);
    }

    internal static DumpSelectedMethodLexicalFacts CreateExact(
        DumpPortablePdbContextFacts portablePdbContext,
        DumpSelectedMethodLexicalSourceEnds sourceEnds,
        ImmutableArray<DumpLexicalMethodDefinitionRowFact> allMethods,
        ImmutableArray<DumpLexicalGenericParameterRowFact> declaringTypeGenericParameters,
        ImmutableArray<DumpLexicalGenericParameterRowFact> methodGenericParameters,
        DumpLexicalStandaloneSignatureFact? localSignature,
        ImmutableArray<DumpLexicalLocalScopeRowFact> allLocalScopes,
        ImmutableArray<DumpLexicalLocalVariableRowFact> allLocalVariables,
        ImmutableArray<DumpLexicalLocalConstantRowFact> allLocalConstants)
    {
        ArgumentNullException.ThrowIfNull(portablePdbContext);
        ArgumentNullException.ThrowIfNull(sourceEnds);
        var methodDefinitionTableRowCount = sourceEnds.MethodDefinitionRowCount;
        var parameterTableRowCount = sourceEnds.ParameterRowCount;
        if (methodDefinitionTableRowCount is <= 0 or > DumpSelectedMethodLexicalLimits.MaximumMethodDefinitionRowCount)
        {
            throw new ArgumentOutOfRangeException(nameof(methodDefinitionTableRowCount));
        }

        if (parameterTableRowCount is < 0 or > DumpSelectedMethodLexicalLimits.MaximumParameterRowCount)
        {
            throw new ArgumentOutOfRangeException(nameof(parameterTableRowCount));
        }

        var normalizedMethods = DumpSelectedMethodLexicalEncoding.NormalizeRows(
            allMethods,
            static row => row.MethodDefinitionToken,
            nameof(allMethods));
        if (normalizedMethods.Length != methodDefinitionTableRowCount)
        {
            throw new ArgumentException(
                "The MethodDef catalog must contain every row reported by the complete table count.",
                nameof(allMethods));
        }

        long aggregateMethodSignatureBytes = 0;
        var observedParameterCount = 0;
        for (var index = 0; index < normalizedMethods.Length; index++)
        {
            var method = normalizedMethods[index];
            if (CanonicalReplayEncoding.MetadataTokenRowId(method.MethodDefinitionToken) != index + 1)
            {
                throw new ArgumentException(
                    "The complete MethodDef catalog must exhaust every RID without a prefix or gap.",
                    nameof(allMethods));
            }

            aggregateMethodSignatureBytes += method.Signature.Length;
            var start = method.ParameterListStartRowId;
            var end = index + 1 < normalizedMethods.Length
                ? normalizedMethods[index + 1].ParameterListStartRowId
                : parameterTableRowCount + 1;
            if (start > parameterTableRowCount + 1 || end < start || end > parameterTableRowCount + 1)
            {
                throw new ArgumentException(
                    "MethodDef ParamList starts must form nondecreasing intervals within the complete Param table.",
                    nameof(allMethods));
            }

            var expectedParameterCount = end - start;
            if (method.Parameters.Length != expectedParameterCount)
            {
                throw new ArgumentException(
                    "Each MethodDef must retain exactly its complete ParamList ownership interval.",
                    nameof(allMethods));
            }

            for (var parameterIndex = 0; parameterIndex < method.Parameters.Length; parameterIndex++)
            {
                if (CanonicalReplayEncoding.MetadataTokenRowId(method.Parameters[parameterIndex].ParameterToken) !=
                    start + parameterIndex)
                {
                    throw new ArgumentException(
                        "Owned Param rows must retain complete physical RID order within each ParamList interval.",
                        nameof(allMethods));
                }
            }

            observedParameterCount += method.Parameters.Length;
        }

        if (observedParameterCount != parameterTableRowCount)
        {
            throw new ArgumentException(
                "The complete MethodDef catalog must account for every Param-table row exactly once.",
                nameof(allMethods));
        }

        if (aggregateMethodSignatureBytes > DumpSelectedMethodLexicalLimits.MaximumAggregateMethodSignatureByteCount)
        {
            throw new ArgumentException("The complete method-signature catalog exceeds its byte bound.", nameof(allMethods));
        }

        var maximumMethodSignatureBytes = normalizedMethods.Max(static method => method.Signature.Length);
        if (maximumMethodSignatureBytes != sourceEnds.MaximumMethodSignatureByteCount ||
            aggregateMethodSignatureBytes != sourceEnds.AggregateMethodSignatureByteCount)
        {
            throw new ArgumentException(
                "MethodDef signature lengths must exactly match their independently observed source ends.",
                nameof(sourceEnds));
        }

        var selectedFrame = portablePdbContext.SelectedFrame;
        var selectedMethod = normalizedMethods.SingleOrDefault(
            method => method.MethodDefinitionToken == selectedFrame.MethodDefinitionToken) ??
            throw new ArgumentException("The complete MethodDef catalog omits the selected MethodDef.", nameof(allMethods));
        if (selectedMethod.DeclaringTypeDefinitionToken != selectedFrame.DeclaringTypeDefinitionToken)
        {
            throw new ArgumentException(
                "The selected MethodDef row and selected frame disagree on declaring TypeDef ownership.",
                nameof(allMethods));
        }

        var normalizedTypeParameters = NormalizeGenericParameters(
            declaringTypeGenericParameters,
            selectedFrame.DeclaringTypeDefinitionToken,
            nameof(declaringTypeGenericParameters));
        var normalizedMethodParameters = NormalizeGenericParameters(
            methodGenericParameters,
            selectedFrame.MethodDefinitionToken,
            nameof(methodGenericParameters));
        if (normalizedTypeParameters.Length != sourceEnds.DeclaringTypeGenericParameterRowCount ||
            normalizedMethodParameters.Length != sourceEnds.MethodGenericParameterRowCount)
        {
            throw new ArgumentException(
                "GenericParam arrays must exactly match their independently observed source ends.",
                nameof(sourceEnds));
        }

        if (normalizedMethodParameters.Length != selectedMethod.SignatureGenericParameterCount)
        {
            throw new ArgumentException(
                "The selected MethodDef GenericParam cardinality must match its fully decoded signature arity.",
                nameof(methodGenericParameters));
        }
        if (normalizedTypeParameters
            .Select(static parameter => parameter.GenericParameterToken)
            .Intersect(normalizedMethodParameters.Select(static parameter => parameter.GenericParameterToken))
            .Any())
        {
            throw new ArgumentException(
                "A GenericParam token cannot occur in both selected-owner collections.",
                nameof(methodGenericParameters));
        }

        var localSignatureByteCount = localSignature?.Signature.Length ?? 0;
        var localSlotCount = localSignature?.LocalSlotCount ?? 0;
        if (localSignatureByteCount != sourceEnds.StandaloneSignatureByteCount ||
            localSlotCount != sourceEnds.LocalSlotCount)
        {
            throw new ArgumentException(
                "The selected local signature must exactly match its independently observed byte and slot source ends.",
                nameof(sourceEnds));
        }

        var maximumSignatureTypeDepth = Math.Max(
            normalizedMethods.Max(static method => method.SignatureTypeDepth),
            localSignature?.SignatureTypeDepth ?? 0);
        var maximumSignatureTypeNodeCount = Math.Max(
            normalizedMethods.Max(static method => method.SignatureTypeNodeCount),
            localSignature?.SignatureTypeNodeCount ?? 0);
        if (maximumSignatureTypeDepth != sourceEnds.MaximumSignatureTypeDepth ||
            maximumSignatureTypeNodeCount != sourceEnds.MaximumSignatureTypeNodeCount)
        {
            throw new ArgumentException(
                "MethodDef and local signature structure must exactly match independently observed depth and node ends.",
                nameof(sourceEnds));
        }

        var localScopeTable = DumpSelectedMethodLexicalEncoding.NormalizeRows(
            allLocalScopes,
            static row => row.LocalScopeToken,
            nameof(allLocalScopes));
        if (localScopeTable.Length != sourceEnds.LocalScopeRowCount)
        {
            throw new ArgumentException(
                "The complete LocalScope table must exactly match its independently observed source end.",
                nameof(allLocalScopes));
        }
        if (localScopeTable.Length > DumpSelectedMethodLexicalLimits.LocalScopeRowTraversalBound.Value)
        {
            throw new ArgumentException("The complete LocalScope table exceeds the unchanged PDB bound.", nameof(allLocalScopes));
        }

        ValidateCompletePhysicalTable(
            localScopeTable,
            static scope => scope.LocalScopeToken,
            nameof(allLocalScopes));
        ValidateLocalScopePhysicalOrder(localScopeTable, nameof(allLocalScopes));
        var methodTokens = normalizedMethods.Select(static method => method.MethodDefinitionToken).ToHashSet();
        if (localScopeTable.Any(scope => !methodTokens.Contains(scope.MethodDefinitionToken)))
        {
            throw new ArgumentException(
                "Every LocalScope row must name a MethodDef retained in the complete module table.",
                nameof(allLocalScopes));
        }

        var selectedScopes = localScopeTable
            .Where(scope => scope.MethodDefinitionToken == selectedFrame.MethodDefinitionToken)
            .ToImmutableArray();
        var activeScopes = selectedScopes
            .Where(scope => scope.Contains(selectedFrame.Instruction.IlOffset))
            .OrderBy(static scope => scope.StartOffset)
            .ThenByDescending(static scope => scope.EndOffset)
            .ThenBy(static scope => scope.LocalScopeToken)
            .ToImmutableArray();
        for (var index = 1; index < activeScopes.Length; index++)
        {
            var outer = activeScopes[index - 1];
            var inner = activeScopes[index];
            if (inner.StartOffset == outer.StartOffset && inner.EndOffset == outer.EndOffset ||
                inner.StartOffset < outer.StartOffset || inner.EndOffset > outer.EndOffset)
            {
                throw new DumpSelectedMethodLexicalScopeAmbiguityException();
            }
        }

        ValidateLocalScopeRangeTopology(localScopeTable, nameof(allLocalScopes));
        ValidateExistingActiveScopeProjection(portablePdbContext, activeScopes, nameof(allLocalScopes));
        var activeScopeTokens = activeScopes.Select(static scope => scope.LocalScopeToken).ToHashSet();
        var selectedScopeTokens = selectedScopes.Select(static scope => scope.LocalScopeToken).ToHashSet();
        var inactiveScopes = selectedScopes
            .Where(scope => !activeScopeTokens.Contains(scope.LocalScopeToken))
            .ToImmutableArray();
        var tableScopeTokens = localScopeTable.Select(static scope => scope.LocalScopeToken).ToHashSet();

        var localVariableTable = DumpSelectedMethodLexicalEncoding.NormalizeRows(
            allLocalVariables,
            static row => row.LocalVariableToken,
            nameof(allLocalVariables));
        if (localVariableTable.Length != sourceEnds.LocalVariableRowCount)
        {
            throw new ArgumentException(
                "The complete LocalVariable table must exactly match its independently observed source end.",
                nameof(allLocalVariables));
        }
        if (localVariableTable.Length > DumpSelectedMethodLexicalLimits.MaximumLocalVariableRowCount)
        {
            throw new ArgumentException("The complete LocalVariable table exceeds its row bound.", nameof(allLocalVariables));
        }

        ValidateCompletePhysicalTable(
            localVariableTable,
            static variable => variable.LocalVariableToken,
            nameof(allLocalVariables));
        if (localVariableTable.Any(variable => !tableScopeTokens.Contains(variable.LocalScopeToken)))
        {
            throw new ArgumentException(
                "Every LocalVariable row must identify one row in the complete physical LocalScope table.",
                nameof(allLocalVariables));
        }

        ValidatePhysicalListIntervals(
            localScopeTable,
            localVariableTable,
            static scope => scope.VariableListStartRowId,
            static row => row.LocalVariableToken,
            static row => row.LocalScopeToken,
            nameof(allLocalVariables));

        if (localVariableTable
            .GroupBy(static variable => (variable.LocalScopeToken, variable.SlotIndex))
            .Any(static group => group.Count() != 1))
        {
            throw new ArgumentException(
                "One physical LocalScope cannot assign the same local slot to multiple LocalVariable rows.",
                nameof(allLocalVariables));
        }

        var selectedVariables = localVariableTable
            .Where(variable => selectedScopeTokens.Contains(variable.LocalScopeToken))
            .ToImmutableArray();
        if (localSignature is null && !selectedVariables.IsEmpty ||
            localSignature is not null && selectedVariables.Any(variable => variable.SlotIndex >= localSignature.LocalSlotCount))
        {
            throw new ArgumentException(
                "Every selected-method LocalVariable slot must lie within the complete selected StandAloneSig.",
                nameof(allLocalVariables));
        }

        var activeScopeOrder = activeScopes
            .Select((scope, index) => (scope.LocalScopeToken, index))
            .ToDictionary(static pair => pair.LocalScopeToken, static pair => pair.index);
        var activeVariables = selectedVariables
            .Where(variable => activeScopeTokens.Contains(variable.LocalScopeToken))
            .OrderBy(variable => activeScopeOrder[variable.LocalScopeToken])
            .ThenBy(static variable => variable.LocalVariableToken)
            .ToImmutableArray();
        var inactiveVariables = selectedVariables
            .Where(variable => !activeScopeTokens.Contains(variable.LocalScopeToken))
            .ToImmutableArray();

        var localConstantTable = DumpSelectedMethodLexicalEncoding.NormalizeRows(
            allLocalConstants,
            static row => row.LocalConstantToken,
            nameof(allLocalConstants));
        if (localConstantTable.Length != sourceEnds.LocalConstantRowCount)
        {
            throw new ArgumentException(
                "The complete LocalConstant table must exactly match its independently observed source end.",
                nameof(allLocalConstants));
        }
        if (localConstantTable.Length > DumpSelectedMethodLexicalLimits.MaximumLocalConstantRowCount)
        {
            throw new ArgumentException("The complete LocalConstant table exceeds its row bound.", nameof(allLocalConstants));
        }

        ValidateCompletePhysicalTable(
            localConstantTable,
            static constant => constant.LocalConstantToken,
            nameof(allLocalConstants));
        if (localConstantTable.Any(constant => !tableScopeTokens.Contains(constant.LocalScopeToken)))
        {
            throw new ArgumentException(
                "Every LocalConstant row must identify one row in the complete physical LocalScope table.",
                nameof(allLocalConstants));
        }

        ValidatePhysicalListIntervals(
            localScopeTable,
            localConstantTable,
            static scope => scope.ConstantListStartRowId,
            static row => row.LocalConstantToken,
            static row => row.LocalScopeToken,
            nameof(allLocalConstants));

        var aggregateConstantSignatureBytes = localConstantTable.Sum(static constant => (long)constant.Signature.Length);
        if (aggregateConstantSignatureBytes >
            DumpSelectedMethodLexicalLimits.MaximumAggregateLocalConstantSignatureByteCount)
        {
            throw new ArgumentException(
                "The complete LocalConstant signature set exceeds its aggregate byte bound.",
                nameof(allLocalConstants));
        }

        var maximumConstantSignatureBytes = localConstantTable.IsEmpty
            ? 0
            : localConstantTable.Max(static constant => constant.Signature.Length);
        if (maximumConstantSignatureBytes != sourceEnds.MaximumLocalConstantSignatureByteCount ||
            aggregateConstantSignatureBytes != sourceEnds.AggregateLocalConstantSignatureByteCount)
        {
            throw new ArgumentException(
                "LocalConstant signature lengths must exactly match their independently observed source ends.",
                nameof(sourceEnds));
        }

        var selectedConstants = localConstantTable
            .Where(constant => selectedScopeTokens.Contains(constant.LocalScopeToken))
            .ToImmutableArray();
        var activeConstants = selectedConstants
            .Where(constant => activeScopeTokens.Contains(constant.LocalScopeToken))
            .OrderBy(constant => activeScopeOrder[constant.LocalScopeToken])
            .ThenBy(static constant => constant.LocalConstantToken)
            .ToImmutableArray();
        var inactiveConstants = selectedConstants
            .Where(constant => !activeScopeTokens.Contains(constant.LocalScopeToken))
            .ToImmutableArray();

        var generated = normalizedMethods
            .Select(method => DumpGeneratedLocalFunctionRowFact.TryCreate(method, out var fact) ? fact : null)
            .Where(fact => fact is not null &&
                string.Equals(fact.ContainingMethodName, selectedMethod.Name, StringComparison.Ordinal))
            .Select(static fact => fact!)
            .ToImmutableArray();
        if (generated.Length > DumpSelectedMethodLexicalLimits.MaximumGeneratedLocalFunctionRowCount)
        {
            throw new ArgumentException(
                "The selected raw method name matches more generated local-function rows than the bound permits.",
                nameof(allMethods));
        }

        if (generated.Length != sourceEnds.GeneratedLocalFunctionRowCount)
        {
            throw new ArgumentException(
                "Generated local-function candidates must exactly match their independently observed source end.",
                nameof(sourceEnds));
        }

        var activeNamedSlots = activeVariables.Select(static variable => variable.SlotIndex).ToHashSet();
        var inactiveNamedSlots = inactiveVariables.Select(static variable => variable.SlotIndex).ToHashSet();
        var namedSlots = selectedVariables.Select(static variable => variable.SlotIndex).ToHashSet();
        var inactiveOnlySlots = localSignature is null
            ? ImmutableArray<int>.Empty
            : Enumerable.Range(0, localSignature.LocalSlotCount)
                .Where(slot => inactiveNamedSlots.Contains(slot) && !activeNamedSlots.Contains(slot))
                .ToImmutableArray();
        var unaccountedSlots = localSignature is null
            ? ImmutableArray<int>.Empty
            : Enumerable.Range(0, localSignature.LocalSlotCount)
                .Where(slot => !namedSlots.Contains(slot))
                .ToImmutableArray();

        return new DumpSelectedMethodLexicalFacts(
            portablePdbContext,
            sourceEnds,
            normalizedMethods,
            selectedMethod,
            normalizedTypeParameters,
            normalizedMethodParameters,
            localSignature,
            localScopeTable,
            selectedScopes,
            activeScopes,
            inactiveScopes,
            localVariableTable,
            selectedVariables,
            activeVariables,
            inactiveVariables,
            localConstantTable,
            selectedConstants,
            activeConstants,
            inactiveConstants,
            generated,
            inactiveOnlySlots,
            unaccountedSlots);
    }

    private static void ValidatePhysicalListIntervals<TRow>(
        ImmutableArray<DumpLexicalLocalScopeRowFact> scopes,
        ImmutableArray<TRow> rows,
        Func<DumpLexicalLocalScopeRowFact, int> startSelector,
        Func<TRow, int> tokenSelector,
        Func<TRow, int> ownerSelector,
        string parameterName)
    {
        if (scopes.IsEmpty)
        {
            if (!rows.IsEmpty)
            {
                throw new ArgumentException(
                    "A non-empty physical list table requires at least one LocalScope row.",
                    parameterName);
            }

            return;
        }

        if (startSelector(scopes[0]) != 1)
        {
            throw new ArgumentException(
                "The first physical LocalScope list must begin at the first row of the complete table.",
                parameterName);
        }

        var tableEnd = checked(rows.Length + 1);
        for (var scopeIndex = 0; scopeIndex < scopes.Length; scopeIndex++)
        {
            var scope = scopes[scopeIndex];
            var start = startSelector(scope);
            var end = scopeIndex + 1 < scopes.Length
                ? startSelector(scopes[scopeIndex + 1])
                : tableEnd;
            if (start > tableEnd || end < start || end > tableEnd)
            {
                throw new ArgumentException(
                    "LocalScope list starts must monotonically partition the complete physical table.",
                    parameterName);
            }

            for (var rowId = start; rowId < end; rowId++)
            {
                var row = rows[rowId - 1];
                if (CanonicalReplayEncoding.MetadataTokenRowId(tokenSelector(row)) != rowId ||
                    ownerSelector(row) != scope.LocalScopeToken)
                {
                    throw new ArgumentException(
                        "Each derived LocalScope interval must own exactly its contiguous physical rows.",
                        parameterName);
                }
            }
        }
    }

    private static void ValidateCompletePhysicalTable<TRow>(
        ImmutableArray<TRow> rows,
        Func<TRow, int> tokenSelector,
        string parameterName)
    {
        for (var index = 0; index < rows.Length; index++)
        {
            if (CanonicalReplayEncoding.MetadataTokenRowId(tokenSelector(rows[index])) != index + 1)
            {
                throw new ArgumentException(
                    "A complete physical table must contain every RID exactly once from one through source end.",
                    parameterName);
            }
        }
    }

    private static void ValidateLocalScopePhysicalOrder(
        ImmutableArray<DumpLexicalLocalScopeRowFact> scopes,
        string parameterName)
    {
        for (var index = 1; index < scopes.Length; index++)
        {
            var previous = scopes[index - 1];
            var current = scopes[index];
            if (previous.MethodDefinitionToken > current.MethodDefinitionToken ||
                previous.MethodDefinitionToken == current.MethodDefinitionToken &&
                (previous.StartOffset > current.StartOffset ||
                previous.StartOffset == current.StartOffset && previous.Length < current.Length))
            {
                throw new ArgumentException(
                    "Physical LocalScope rows must be ordered by MethodDef, start offset ascending, then length descending.",
                    parameterName);
            }
        }
    }

    private static void ValidateLocalScopeRangeTopology(
        ImmutableArray<DumpLexicalLocalScopeRowFact> scopes,
        string parameterName)
    {
        var containing = new Stack<DumpLexicalLocalScopeRowFact>();
        var currentMethod = 0;
        foreach (var scope in scopes)
        {
            if (scope.MethodDefinitionToken != currentMethod)
            {
                containing.Clear();
                currentMethod = scope.MethodDefinitionToken;
            }

            while (containing.Count != 0 && scope.StartOffset >= containing.Peek().EndOffset)
            {
                containing.Pop();
            }

            if (containing.Count != 0 && scope.EndOffset > containing.Peek().EndOffset)
            {
                throw new ArgumentException(
                    "Same-method LocalScope ranges must be pairwise nested or disjoint.",
                    parameterName);
            }

            containing.Push(scope);
        }
    }

    private static ImmutableArray<DumpLexicalGenericParameterRowFact> NormalizeGenericParameters(
        ImmutableArray<DumpLexicalGenericParameterRowFact> parameters,
        int expectedOwnerToken,
        string parameterName)
    {
        if (parameters.IsDefault)
        {
            throw new ArgumentException("An explicitly initialized GenericParam-row set is required.", parameterName);
        }

        if (parameters.Length > DumpSelectedMethodLexicalLimits.MaximumGenericParameterRowCount)
        {
            throw new ArgumentException("The GenericParam-row set exceeds its bound.", parameterName);
        }

        var normalized = parameters
            .Select(parameter => parameter ?? throw new ArgumentException("GenericParam rows cannot be null.", parameterName))
            .OrderBy(static parameter => parameter.Number)
            .ThenBy(static parameter => parameter.GenericParameterToken)
            .ToImmutableArray();
        if (normalized.Select(static parameter => parameter.GenericParameterToken).Distinct().Count() != normalized.Length)
        {
            throw new ArgumentException("A GenericParam token occurs more than once.", parameterName);
        }

        for (var index = 0; index < normalized.Length; index++)
        {
            if (normalized[index].OwnerToken != expectedOwnerToken || normalized[index].Number != index)
            {
                throw new ArgumentException(
                    "GenericParam rows must exhaust zero-based number order for the exact selected owner.",
                    parameterName);
            }
        }

        return normalized;
    }

    private static void ValidateExistingActiveScopeProjection(
        DumpPortablePdbContextFacts portablePdbContext,
        ImmutableArray<DumpLexicalLocalScopeRowFact> activeScopes,
        string parameterName)
    {
        var existing = portablePdbContext.LocalScopes;
        if (existing.Length != activeScopes.Length)
        {
            throw new ArgumentException(
                "The all-scope projection must reproduce every existing active PDB scope.",
                parameterName);
        }

        for (var index = 0; index < activeScopes.Length; index++)
        {
            var raw = activeScopes[index];
            var projected = existing[index];
            if (projected.NestingDepth != index ||
                projected.LocalScopeToken != raw.LocalScopeToken ||
                projected.MethodDefinitionToken != raw.MethodDefinitionToken ||
                projected.ImportScopeToken != raw.ImportScopeToken ||
                projected.StartOffset != raw.StartOffset ||
                projected.Length != raw.Length)
            {
                throw new ArgumentException(
                    "The range-derived active chain disagrees with the existing PDB scope projection.",
                    parameterName);
            }
        }
    }
}

/// <summary>Represents the complete disposition of bounded selected-method lexical acquisition.</summary>
/// <remarks>
/// This draft observation reuses the established context status vocabulary. Exact is the sole status carrying
/// <see cref="Facts"/>; every other status retains the selected frame, typed issue, and reached bounds with no row
/// prefix exposed through the API.
/// </remarks>
public sealed class DumpSelectedMethodLexicalObservation : IEquatable<DumpSelectedMethodLexicalObservation>
{
    private readonly ImmutableArray<EvaluationDeterministicBound> reachedBounds;
    private readonly ImmutableArray<byte> canonicalBytes;
    private readonly int canonicalHashCode;

    private DumpSelectedMethodLexicalObservation(
        DumpContextEvidenceStatus status,
        DumpContextEvidenceIssue issue,
        DumpSelectedFrameIdentity selectedFrame,
        DumpSelectedMethodLexicalFacts? facts,
        ImmutableArray<EvaluationDeterministicBound> reachedBounds)
    {
        DumpContextContractEncoding.ValidateStatusIssue(status, issue);
        ValidateLexicalStatusIssue(status, issue);
        if ((status == DumpContextEvidenceStatus.Exact) != (facts is not null))
        {
            throw new ArgumentException("Exactly the Exact status must carry selected-method lexical facts.", nameof(facts));
        }

        if (facts is not null && !facts.PortablePdbContext.SelectedFrame.Equals(selectedFrame))
        {
            throw new ArgumentException("Exact lexical facts must identify the observation's selected frame.", nameof(facts));
        }

        Status = status;
        Issue = issue;
        SelectedFrame = selectedFrame;
        Facts = facts;
        this.reachedBounds = CanonicalReplayEncoding.Copy(reachedBounds);
        var writer = new CanonicalReplayEncoding.Writer("dump-selected-method-lexical-observation", 1);
        writer.WriteInt32((int)status);
        writer.WriteInt32((int)issue);
        writer.WriteLengthPrefixedBytes(selectedFrame.CanonicalBytes.AsSpan());
        DumpContextContractEncoding.WriteBounds(writer, reachedBounds);
        writer.WriteBoolean(facts is not null);
        if (facts is not null)
        {
            writer.WriteLengthPrefixedBytes(facts.CanonicalBytes.AsSpan());
        }
        canonicalBytes = writer.ToImmutableArray();
        Sha256 = CanonicalReplayEncoding.ComputeSha256(canonicalBytes.AsSpan());
        canonicalHashCode = CanonicalReplayEncoding.CanonicalHashCode(canonicalBytes);
    }

    /// <summary>Gets the exact, partial, unavailable, ambiguous, conflict, invalid, or unsupported status.</summary>
    public DumpContextEvidenceStatus Status { get; }

    /// <summary>Gets the stable first-boundary issue; exact evidence always uses <c>None</c>.</summary>
    public DumpContextEvidenceIssue Issue { get; }

    /// <summary>Gets the exact selected frame whose method and IL offset define this acquisition.</summary>
    public DumpSelectedFrameIdentity SelectedFrame { get; }

    /// <summary>Gets complete lexical facts only for exact status; otherwise gets null.</summary>
    public DumpSelectedMethodLexicalFacts? Facts { get; }

    /// <summary>Gets whether this observation carries one complete exact lexical fact set.</summary>
    public bool HasExactFacts => Facts is not null;

    /// <summary>Gets a defensive canonical-name-ordered copy of bounds reached by this acquisition path.</summary>
    public ImmutableArray<EvaluationDeterministicBound> ReachedBounds => CanonicalReplayEncoding.Copy(reachedBounds);

    /// <summary>Gets a defensive copy of the draft canonical observation bytes.</summary>
    public ImmutableArray<byte> CanonicalBytes => CanonicalReplayEncoding.Copy(canonicalBytes);

    /// <summary>Gets the lowercase SHA-256 digest of <see cref="CanonicalBytes"/>.</summary>
    public string Sha256 { get; }

    /// <summary>
    /// Acquires an exact fact set from complete physical catalogs or returns a factless bound observation before
    /// validating a forbidden prefix.
    /// </summary>
    /// <param name="portablePdbContext">The exact selected-frame PDB context whose active scopes must be reproduced.</param>
    /// <param name="sourceEnds">Exact counts and byte totals observed by bounded source enumeration.</param>
    /// <param name="allMethods">
    /// Every MethodDef row in the module, including exact ParamList intervals and Param rows. Caller order is ignored;
    /// physical RID order is restored and must exhaust the table.
    /// </param>
    /// <param name="declaringTypeGenericParameters">The complete selected declaring-TypeDef GenericParam rows.</param>
    /// <param name="methodGenericParameters">The complete selected MethodDef GenericParam rows.</param>
    /// <param name="localSignature">The complete selected StandAloneSig, or null when the method body declares none.</param>
    /// <param name="allLocalScopes">
    /// The complete physical Portable-PDB LocalScope table. Other-method and final rows are required because each
    /// list end is derived from the next physical row or complete child-table end.
    /// </param>
    /// <param name="allLocalVariables">The complete physical Portable-PDB LocalVariable table.</param>
    /// <param name="allLocalConstants">The complete physical Portable-PDB LocalConstant table.</param>
    /// <returns>
    /// Exact facts when every source ends within its bound; otherwise a factless Partial/BoundReached observation when
    /// a complete MethodDef/Param table or another supplied physical set exceeds its declared cap.
    /// </returns>
    /// <remarks>
    /// This draft factory checks every independently observed source end before touching supplied row arrays. A
    /// cap-plus-one source may therefore pass no prefix at all and cannot accidentally expose one. Within-cap complete
    /// catalogs must have contiguous RIDs, physical LocalScope tuple order, pairwise nested-or-disjoint ranges, and list
    /// starts whose next-row/table-end intervals account for every child row. Malformed caller facts are contract
    /// violations and throw; competing active range projections produce the typed ambiguous observation.
    /// </remarks>
    public static DumpSelectedMethodLexicalObservation Acquire(
        DumpPortablePdbContextFacts portablePdbContext,
        DumpSelectedMethodLexicalSourceEnds sourceEnds,
        ImmutableArray<DumpLexicalMethodDefinitionRowFact> allMethods,
        ImmutableArray<DumpLexicalGenericParameterRowFact> declaringTypeGenericParameters,
        ImmutableArray<DumpLexicalGenericParameterRowFact> methodGenericParameters,
        DumpLexicalStandaloneSignatureFact? localSignature,
        ImmutableArray<DumpLexicalLocalScopeRowFact> allLocalScopes,
        ImmutableArray<DumpLexicalLocalVariableRowFact> allLocalVariables,
        ImmutableArray<DumpLexicalLocalConstantRowFact> allLocalConstants)
    {
        ArgumentNullException.ThrowIfNull(portablePdbContext);
        ArgumentNullException.ThrowIfNull(sourceEnds);
        var reachedBounds = sourceEnds.GetReachedBounds(out var exceeded);
        if (exceeded)
        {
            return Partial(
                portablePdbContext.SelectedFrame,
                DumpContextEvidenceIssue.BoundReached,
                reachedBounds);
        }

        try
        {
            var facts = DumpSelectedMethodLexicalFacts.CreateExact(
                portablePdbContext,
                sourceEnds,
                allMethods,
                declaringTypeGenericParameters,
                methodGenericParameters,
                localSignature,
                allLocalScopes,
                allLocalVariables,
                allLocalConstants);
            return Exact(facts);
        }
        catch (DumpSelectedMethodLexicalScopeAmbiguityException)
        {
            return Ambiguous(
                portablePdbContext.SelectedFrame,
                DumpContextEvidenceIssue.ScopeAmbiguous,
                reachedBounds);
        }
    }

    /// <summary>Creates an exact observation carrying one complete selected-method lexical fact set.</summary>
    /// <param name="facts">The complete exact facts.</param>
    /// <returns>An exact draft observation whose issue is <c>None</c>.</returns>
    public static DumpSelectedMethodLexicalObservation Exact(DumpSelectedMethodLexicalFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        var reachedBounds = facts.SourceEnds.GetReachedBounds(out var exceeded);
        if (exceeded)
        {
            throw new ArgumentException("Exact facts cannot contain an exceeded source measurement.", nameof(facts));
        }

        return Create(
            DumpContextEvidenceStatus.Exact,
            DumpContextEvidenceIssue.None,
            facts.PortablePdbContext.SelectedFrame,
            facts,
            reachedBounds);
    }

    /// <summary>Creates a partial lexical observation carrying no row prefix.</summary>
    /// <param name="selectedFrame">The exact selected frame reached before incompleteness.</param>
    /// <param name="issue">A partial issue: <c>BoundReached</c> or <c>SourceIncomplete</c>.</param>
    /// <param name="reachedBounds">An initialized set containing only physically reached bounds.</param>
    /// <returns>A factless partial draft observation.</returns>
    public static DumpSelectedMethodLexicalObservation Partial(
        DumpSelectedFrameIdentity selectedFrame,
        DumpContextEvidenceIssue issue,
        ImmutableArray<EvaluationDeterministicBound> reachedBounds) =>
        Create(DumpContextEvidenceStatus.Partial, issue, selectedFrame, null, reachedBounds);

    /// <summary>Creates an unavailable lexical observation carrying no facts.</summary>
    /// <param name="selectedFrame">The exact selected frame whose prerequisite was unavailable.</param>
    /// <param name="issue">An unavailable issue: <c>PrerequisiteUnavailable</c> or <c>ScopeUnavailable</c>.</param>
    /// <param name="reachedBounds">An initialized set containing only physically reached bounds.</param>
    /// <returns>A factless unavailable draft observation.</returns>
    public static DumpSelectedMethodLexicalObservation Unavailable(
        DumpSelectedFrameIdentity selectedFrame,
        DumpContextEvidenceIssue issue,
        ImmutableArray<EvaluationDeterministicBound> reachedBounds) =>
        Create(DumpContextEvidenceStatus.Unavailable, issue, selectedFrame, null, reachedBounds);

    /// <summary>Creates an ambiguous lexical observation without selecting one competing scope projection.</summary>
    /// <param name="selectedFrame">The exact selected frame associated with the ambiguity.</param>
    /// <param name="issue">The required <c>ScopeAmbiguous</c> issue.</param>
    /// <param name="reachedBounds">An initialized set containing only physically reached bounds.</param>
    /// <returns>A factless ambiguous draft observation.</returns>
    public static DumpSelectedMethodLexicalObservation Ambiguous(
        DumpSelectedFrameIdentity selectedFrame,
        DumpContextEvidenceIssue issue,
        ImmutableArray<EvaluationDeterministicBound> reachedBounds) =>
        Create(DumpContextEvidenceStatus.Ambiguous, issue, selectedFrame, null, reachedBounds);

    /// <summary>Creates a conflicting lexical observation without exposing either source as exact facts.</summary>
    /// <param name="selectedFrame">The exact selected frame associated with the conflict.</param>
    /// <param name="issue">A snapshot or module identity conflict issue.</param>
    /// <param name="reachedBounds">An initialized set containing only physically reached bounds.</param>
    /// <returns>A factless conflict draft observation.</returns>
    public static DumpSelectedMethodLexicalObservation Conflict(
        DumpSelectedFrameIdentity selectedFrame,
        DumpContextEvidenceIssue issue,
        ImmutableArray<EvaluationDeterministicBound> reachedBounds) =>
        Create(DumpContextEvidenceStatus.Conflict, issue, selectedFrame, null, reachedBounds);

    /// <summary>Creates an invalid lexical observation without exposing malformed rows.</summary>
    /// <param name="selectedFrame">The exact selected frame associated with the invalid source.</param>
    /// <param name="issue">An <c>InvalidFrame</c> or <c>InvalidScope</c> issue.</param>
    /// <param name="reachedBounds">An initialized set containing only physically reached bounds.</param>
    /// <returns>A factless invalid draft observation.</returns>
    public static DumpSelectedMethodLexicalObservation Invalid(
        DumpSelectedFrameIdentity selectedFrame,
        DumpContextEvidenceIssue issue,
        ImmutableArray<EvaluationDeterministicBound> reachedBounds) =>
        Create(DumpContextEvidenceStatus.Invalid, issue, selectedFrame, null, reachedBounds);

    /// <summary>Creates an unsupported lexical observation without manufacturing generated-name or scope facts.</summary>
    /// <param name="selectedFrame">The exact selected frame associated with the unsupported representation.</param>
    /// <param name="issue">The required <c>UnsupportedScope</c> issue.</param>
    /// <param name="reachedBounds">An initialized set containing only physically reached bounds.</param>
    /// <returns>A factless unsupported draft observation.</returns>
    public static DumpSelectedMethodLexicalObservation Unsupported(
        DumpSelectedFrameIdentity selectedFrame,
        DumpContextEvidenceIssue issue,
        ImmutableArray<EvaluationDeterministicBound> reachedBounds) =>
        Create(DumpContextEvidenceStatus.Unsupported, issue, selectedFrame, null, reachedBounds);

    /// <summary>Determines content equality from canonical status, source, bounds, and optional exact facts.</summary>
    /// <param name="other">The observation to compare.</param>
    /// <returns><see langword="true"/> when every replay-significant field is equal.</returns>
    public bool Equals(DumpSelectedMethodLexicalObservation? other) =>
        other is not null && CanonicalReplayEncoding.CanonicalEquals(canonicalBytes, other.canonicalBytes);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as DumpSelectedMethodLexicalObservation);

    /// <inheritdoc/>
    public override int GetHashCode() => canonicalHashCode;

    private static DumpSelectedMethodLexicalObservation Create(
        DumpContextEvidenceStatus status,
        DumpContextEvidenceIssue issue,
        DumpSelectedFrameIdentity selectedFrame,
        DumpSelectedMethodLexicalFacts? facts,
        ImmutableArray<EvaluationDeterministicBound> reachedBounds)
    {
        ArgumentNullException.ThrowIfNull(selectedFrame);
        var normalizedBounds = CanonicalReplayEncoding.NormalizeBounds(reachedBounds, nameof(reachedBounds));
        var declared = DumpSelectedMethodLexicalLimits.AllDeclaredBounds;
        if (normalizedBounds.Any(bound => !declared.Contains(bound)))
        {
            throw new ArgumentException(
                "Reached bounds must be exact members of the selected-method lexical limit catalog.",
                nameof(reachedBounds));
        }

        if (issue == DumpContextEvidenceIssue.BoundReached && normalizedBounds.IsEmpty)
        {
            throw new ArgumentException("A BoundReached observation must identify its reached boundary.", nameof(reachedBounds));
        }

        return new DumpSelectedMethodLexicalObservation(status, issue, selectedFrame, facts, normalizedBounds);
    }

    private static void ValidateLexicalStatusIssue(
        DumpContextEvidenceStatus status,
        DumpContextEvidenceIssue issue)
    {
        var valid = status switch
        {
            DumpContextEvidenceStatus.Exact => issue == DumpContextEvidenceIssue.None,
            DumpContextEvidenceStatus.Partial => issue is
                DumpContextEvidenceIssue.BoundReached or DumpContextEvidenceIssue.SourceIncomplete,
            DumpContextEvidenceStatus.Unavailable => issue is
                DumpContextEvidenceIssue.PrerequisiteUnavailable or DumpContextEvidenceIssue.ScopeUnavailable,
            DumpContextEvidenceStatus.Ambiguous => issue == DumpContextEvidenceIssue.ScopeAmbiguous,
            DumpContextEvidenceStatus.Conflict => issue is
                DumpContextEvidenceIssue.SnapshotMismatch or DumpContextEvidenceIssue.ModuleMismatch,
            DumpContextEvidenceStatus.Invalid => issue is
                DumpContextEvidenceIssue.InvalidFrame or DumpContextEvidenceIssue.InvalidScope,
            DumpContextEvidenceStatus.Unsupported => issue == DumpContextEvidenceIssue.UnsupportedScope,
            _ => false,
        };
        if (!valid)
        {
            throw new ArgumentException(
                $"Issue {issue} is not meaningful for a selected-method lexical {status} observation.",
                nameof(issue));
        }
    }
}

internal sealed class DumpSelectedMethodLexicalScopeAmbiguityException : Exception
{
}

internal static class DumpSelectedMethodLexicalEncoding
{
    private const int MaximumTextLength = 4_096;

    internal static void ValidateText(string value, string parameterName, bool allowEmpty)
    {
        ArgumentNullException.ThrowIfNull(value, parameterName);
        if ((!allowEmpty && value.Length == 0) ||
            value.Length > MaximumTextLength ||
            value.Any(static character => char.IsControl(character)))
        {
            throw new ArgumentException(
                allowEmpty
                    ? $"Text must contain at most {MaximumTextLength} non-control UTF-16 code units."
                    : $"Non-empty text must contain at most {MaximumTextLength} non-control UTF-16 code units.",
                parameterName);
        }
    }

    internal static void ValidateBlob(ImmutableArray<byte> value, int maximumLength, string parameterName)
    {
        if (value.IsDefault || value.IsEmpty || value.Length > maximumLength)
        {
            throw new ArgumentException(
                $"An initialized non-empty blob of at most {maximumLength} bytes is required.",
                parameterName);
        }
    }

    internal static ImmutableArray<T> NormalizeRows<T>(
        ImmutableArray<T> rows,
        Func<T, int> tokenSelector,
        string parameterName)
        where T : class
    {
        if (rows.IsDefault)
        {
            throw new ArgumentException("An explicitly initialized physical row set is required.", parameterName);
        }

        var normalized = rows
            .Select(row => row ?? throw new ArgumentException("Physical row sets cannot contain null.", parameterName))
            .OrderBy(tokenSelector)
            .ToImmutableArray();
        for (var index = 1; index < normalized.Length; index++)
        {
            if (tokenSelector(normalized[index - 1]) == tokenSelector(normalized[index]))
            {
                throw new ArgumentException("A physical metadata token occurs more than once.", parameterName);
            }
        }

        return normalized;
    }

    internal static bool TryParseGeneratedLocalFunctionName(
        string rawName,
        out string containingMethodName,
        out string localFunctionName,
        out int? methodOrdinal,
        out int? methodGeneration,
        out int localFunctionOrdinal,
        out int localFunctionGeneration)
    {
        containingMethodName = string.Empty;
        localFunctionName = string.Empty;
        methodOrdinal = null;
        methodGeneration = null;
        localFunctionOrdinal = 0;
        localFunctionGeneration = 0;
        if (string.IsNullOrEmpty(rawName) || rawName[0] != '<')
        {
            return false;
        }

        var depth = 0;
        var closingBracket = -1;
        for (var index = 0; index < rawName.Length; index++)
        {
            if (rawName[index] == '<')
            {
                depth++;
            }
            else if (rawName[index] == '>')
            {
                depth--;
                if (depth == 0)
                {
                    closingBracket = index;
                    break;
                }

                if (depth < 0)
                {
                    return false;
                }
            }
        }

        if (closingBracket <= 1 || depth != 0 ||
            !rawName.AsSpan(closingBracket + 1).StartsWith("g__", StringComparison.Ordinal))
        {
            return false;
        }

        containingMethodName = rawName[1..closingBracket];
        var localNameStart = closingBracket + 4;
        var separator = rawName.IndexOf('|', localNameStart);
        if (separator <= localNameStart || separator == rawName.Length - 1)
        {
            return false;
        }

        localFunctionName = rawName[localNameStart..separator];
        if (!IsCSharpIdentifierValue(localFunctionName))
        {
            return false;
        }

        var suffix = rawName.AsSpan(separator + 1);
        var ordinalSeparator = suffix.IndexOf('_');
        if (ordinalSeparator < 0)
        {
            return TryParseOrdinalComponent(suffix, out localFunctionOrdinal, out localFunctionGeneration);
        }

        var parsedMethodGeneration = 0;
        if (ordinalSeparator == 0 || ordinalSeparator == suffix.Length - 1 ||
            suffix[(ordinalSeparator + 1)..].IndexOf('_') >= 0 ||
            !TryParseOrdinalComponent(
                suffix[..ordinalSeparator],
                out var parsedMethodOrdinal,
                out parsedMethodGeneration) ||
            !TryParseOrdinalComponent(
                suffix[(ordinalSeparator + 1)..],
                out localFunctionOrdinal,
                out localFunctionGeneration) ||
            localFunctionGeneration < parsedMethodGeneration)
        {
            return false;
        }

        methodOrdinal = parsedMethodOrdinal;
        methodGeneration = parsedMethodGeneration;
        return true;
    }

    private static bool IsCSharpIdentifierValue(string value)
    {
        if (value.Length == 0)
        {
            return false;
        }

        var position = 0;
        if (!Rune.TryGetRuneAt(value, position, out var first) || !IsIdentifierStart(first))
        {
            return false;
        }

        position += first.Utf16SequenceLength;
        while (position < value.Length)
        {
            if (!Rune.TryGetRuneAt(value, position, out var current) || !IsIdentifierPart(current))
            {
                return false;
            }

            position += current.Utf16SequenceLength;
        }

        return true;
    }

    private static bool IsIdentifierStart(Rune value) =>
        value.Value == '_' || Rune.GetUnicodeCategory(value) is
            UnicodeCategory.UppercaseLetter or
            UnicodeCategory.LowercaseLetter or
            UnicodeCategory.TitlecaseLetter or
            UnicodeCategory.ModifierLetter or
            UnicodeCategory.OtherLetter or
            UnicodeCategory.LetterNumber;

    private static bool IsIdentifierPart(Rune value) =>
        IsIdentifierStart(value) || Rune.GetUnicodeCategory(value) is
            UnicodeCategory.DecimalDigitNumber or
            UnicodeCategory.ConnectorPunctuation or
            UnicodeCategory.NonSpacingMark or
            UnicodeCategory.SpacingCombiningMark or
            UnicodeCategory.Format;

    internal static bool TryMaterializeComplete<T>(
        IEnumerable<T> source,
        int maximumCount,
        out ImmutableArray<T> complete)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (maximumCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumCount));
        }

        var builder = ImmutableArray.CreateBuilder<T>(maximumCount);
        using var enumerator = source.GetEnumerator();
        while (builder.Count < maximumCount && enumerator.MoveNext())
        {
            builder.Add(enumerator.Current);
        }

        if (builder.Count == maximumCount && enumerator.MoveNext())
        {
            complete = ImmutableArray<T>.Empty;
            return false;
        }

        complete = builder.ToImmutable();
        return true;
    }

    private static bool TryParseOrdinalComponent(
        ReadOnlySpan<char> component,
        out int ordinal,
        out int generation)
    {
        ordinal = 0;
        generation = 0;
        var generationSeparator = component.IndexOf('#');
        if (generationSeparator < 0)
        {
            return TryParseCanonicalNonNegativeDecimal(component, out ordinal);
        }

        if (generationSeparator == 0 || generationSeparator == component.Length - 1 ||
            component[(generationSeparator + 1)..].IndexOf('#') >= 0 ||
            !TryParseCanonicalNonNegativeDecimal(component[..generationSeparator], out ordinal) ||
            !TryParseCanonicalNonNegativeDecimal(component[(generationSeparator + 1)..], out generation) ||
            generation == 0)
        {
            return false;
        }

        return true;
    }

    private static bool TryParseCanonicalNonNegativeDecimal(ReadOnlySpan<char> value, out int result)
    {
        result = 0;
        if (value.IsEmpty || value.Length > 1 && value[0] == '0')
        {
            return false;
        }

        foreach (var character in value)
        {
            if (character is < '0' or > '9')
            {
                return false;
            }
        }

        return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out result);
    }

}
