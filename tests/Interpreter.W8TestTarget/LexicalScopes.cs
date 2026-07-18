extern alias requestlib;

using ConstructedAlias = Interpreter.W8TestTarget.GenericSlot<Interpreter.W8TestTarget.RequestContext>;
using ExternalInterfaceAlias = requestlib::Interpreter.W8AliasTarget.IExternalInterfaceSlot<requestlib::Interpreter.W8AliasTarget.ExternalRequestContext>;
using MatrixAlias = Interpreter.W8TestTarget.RequestContext[,];
using NestedOwnerAlias = Interpreter.W8TestTarget.Outer<Interpreter.W8TestTarget.RequestContext>.Middle.Inner<Interpreter.W8TestTarget.BatchContext>;
using TypeDefinitionAlias = Interpreter.W8TestTarget.RequestContext;
using TypeReferenceAlias = requestlib::Interpreter.W8AliasTarget.ExternalRequestContext;
using VectorAlias = Interpreter.W8TestTarget.RequestContext[];
using System.Runtime.CompilerServices;
using static Interpreter.W8TestTarget.NonGenericImports;
using static Interpreter.W8TestTarget.StaticImports<Interpreter.W8TestTarget.BatchContext>;

namespace Interpreter.W8TestTarget.LexicalEvidence;

/// <summary>Supplies a blocker-free bare imported static-field use.</summary>
/// <remarks>This is a draft compiler/PDB fixture and not a product lookup contract.</remarks>
public static class BlockerFreeBareNameProbe
{
    /// <summary>Reads fields imported from generic and non-generic static owners.</summary>
    /// <returns>A deterministic checksum of the directly imported fields.</returns>
    public static int ReadImportedFields() => ImportedSentinel ^ NonGenericImportedSentinel;
}

/// <summary>Supplies higher-precedence members that block imported static fields.</summary>
/// <remarks>This is a draft compiler/PDB fixture and not a product lookup contract.</remarks>
public static class CurrentMemberBlockerProbe
{
    /// <summary>Gets a property whose name blocks the constructed import's stored field.</summary>
    public static int ImportedSentinel => 0x1C017A01;

    /// <summary>Names a method group that blocks the non-generic import's stored field.</summary>
    /// <returns>A deterministic blocker value.</returns>
    public static int NonGenericImportedSentinel() => 0x1C017A02;

    /// <summary>Reads the two current-type blockers.</summary>
    /// <returns>A deterministic checksum over the blocker results.</returns>
    public static int ReadBlockers() => ImportedSentinel ^ NonGenericImportedSentinel();
}

/// <summary>Supplies an imported-field spelling shadowed by a parameter.</summary>
/// <remarks>This is a draft compiler/PDB fixture and not a product lookup contract.</remarks>
public static class ParameterBlockerProbe
{
    /// <summary>Returns the parameter that shadows the imported field.</summary>
    /// <param name="ImportedSentinel">The higher-precedence parameter value.</param>
    /// <returns>The exact supplied value.</returns>
    public static int ReadParameter(int ImportedSentinel) => ImportedSentinel;
}

/// <summary>Supplies the complete optimized lexical-name and local-scope evidence frame.</summary>
/// <remarks>
/// This is a draft frame/PDB fixture and not a frame-value product contract. The language modifier on
/// <see cref="Run"/> exists only to emit the fixed pointer local consumed by the physical PDB oracle.
/// </remarks>
public static class LexicalCatalogProbe
{
    /// <summary>Enters nested source scopes and pauses while every named witness remains live.</summary>
    /// <param name="profile">The selected target profile.</param>
    /// <param name="request">The named reference parameter.</param>
    /// <param name="number">The named value parameter.</param>
    /// <returns>A process exit code if the pause unexpectedly returns.</returns>
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    public static unsafe int Run(string profile, RequestContext request, int number)
    {
        const int localConstant = 0x1C027A01;
        var ordinaryLocal = new TypeDefinitionAlias(request.Name);
        TypeReferenceAlias typeReferenceLocal = new("lexical-type-reference");
        ConstructedAlias.Current = ordinaryLocal;
        var externalInterfaceAliasWitness = typeof(ExternalInterfaceAlias);
        VectorAlias vectorLocal = [ordinaryLocal];
        MatrixAlias matrixLocal = new RequestContext[1, 1];
        matrixLocal[0, 0] = ordinaryLocal;
        var nestedOwnerAliasWitness = typeof(NestedOwnerAlias);
        using var usingLocal = new MemoryStream([0x17, 0x29, 0x41, 0x53], writable: false);
        var (deconstructionNumber, deconstructionText) = (number ^ localConstant, request.Name);
        var byteVector = new byte[] { 0x17, 0x29, 0x41, 0x53 };

        try
        {
            throw new InvalidOperationException("lexical-catch-witness");
        }
        catch (InvalidOperationException catchLocal) when (catchLocal.Message.Length > 0)
        {
            foreach (var foreachLocal in vectorLocal)
            {
                if (foreachLocal is { Name: var patternLocal })
                {
                    for (var rangeLocal = 0; rangeLocal < byteVector.Length; rangeLocal++)
                    {
                        fixed (byte* fixedLocal = byteVector)
                        {
                            var localFunctionResult = LocalFunction(
                                deconstructionNumber,
                                rangeLocal,
                                fixedLocal[rangeLocal]);
                            var genericImportedLocal = ImportedSentinel;
                            var nonGenericImportedLocal = NonGenericImportedSentinel;
                            var importedNestedLocal = new ImportedNested(patternLocal);
                            var nonGenericNestedLocal = new NonGenericNested(localFunctionResult);

                            Console.WriteLine("READY");
                            Console.Out.Flush();
                            Thread.Sleep(Timeout.Infinite);

                            localFunctionResult ^= fixedLocal[rangeLocal];
                            GC.KeepAlive(profile);
                            GC.KeepAlive(ordinaryLocal);
                            GC.KeepAlive(typeReferenceLocal);
                            GC.KeepAlive(externalInterfaceAliasWitness);
                            GC.KeepAlive(vectorLocal);
                            GC.KeepAlive(matrixLocal);
                            GC.KeepAlive(nestedOwnerAliasWitness);
                            GC.KeepAlive(usingLocal.Position);
                            GC.KeepAlive(deconstructionText);
                            GC.KeepAlive(catchLocal);
                            GC.KeepAlive(foreachLocal);
                            GC.KeepAlive(patternLocal);
                            GC.KeepAlive(rangeLocal);
                            GC.KeepAlive(localFunctionResult);
                            GC.KeepAlive(genericImportedLocal);
                            GC.KeepAlive(nonGenericImportedLocal);
                            GC.KeepAlive(importedNestedLocal);
                            GC.KeepAlive(nonGenericNestedLocal);
                            number ^= localFunctionResult;
                        }
                    }
                }
            }
        }

        return 93;

        static int LocalFunction(int left, int right, byte element) => left ^ right ^ element;
    }
}
