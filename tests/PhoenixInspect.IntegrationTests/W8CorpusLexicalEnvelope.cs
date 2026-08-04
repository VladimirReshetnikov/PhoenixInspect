using System.Collections.Immutable;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using PhoenixInspect.Core.Abstractions;
using PhoenixInspect.Host.Dump.ClrMD;

namespace PhoenixInspect.IntegrationTests;

/// <summary>
/// Projects one real selected-method lexical envelope for the W8.9 corpus runner out of the physical tables the
/// dump-resident module metadata and the identity-validated Portable PDB actually carry.
/// </summary>
/// <remarks>
/// This is the host-side acquisition role the composed pipeline deliberately does not own: the bare <c>using
/// static</c> route consumes a caller-supplied <see cref="DumpSelectedMethodLexicalObservation"/>, and this runner
/// supplies one built entirely from observed rows. Nothing here is fabricated — every row, count, and blob is read
/// from the module's own MethodDef/Param/GenericParam/StandAloneSig tables and the PDB's own
/// LocalScope/LocalVariable/LocalConstant tables, and the list-interval starts the reader does not surface directly
/// are recovered from the physical nondecreasing-list invariant rather than guessed.
/// </remarks>
internal static class W8CorpusLexicalEnvelope
{
    /// <summary>Projects the complete lexical envelope of the selected frame's method.</summary>
    /// <param name="moduleReader">The dump-resident metadata of the module that declares the selected method.</param>
    /// <param name="modulePath">The content-identical on-disk artifact whose method bodies name the StandAloneSig.</param>
    /// <param name="portablePdbPath">The identity-validated Portable PDB of that module.</param>
    /// <param name="portablePdbContext">The exact selected-frame PDB context whose active scopes must be reproduced.</param>
    /// <returns>The typed observation the pipeline's bare route consumes.</returns>
    internal static DumpSelectedMethodLexicalObservation Project(
        MetadataReader moduleReader,
        string modulePath,
        string portablePdbPath,
        DumpPortablePdbContextFacts portablePdbContext)
    {
        ArgumentNullException.ThrowIfNull(moduleReader);
        ArgumentNullException.ThrowIfNull(portablePdbContext);

        var selectedMethodToken = portablePdbContext.SelectedFrame.MethodDefinitionToken;
        var selectedTypeToken = portablePdbContext.SelectedFrame.DeclaringTypeDefinitionToken;
        var methods = ProjectMethodDefinitionTable(moduleReader);
        var typeGenericParameters = ProjectGenericParameters(
            moduleReader,
            moduleReader
                .GetTypeDefinition((TypeDefinitionHandle)MetadataTokens.Handle(selectedTypeToken))
                .GetGenericParameters(),
            selectedTypeToken);
        var selectedDefinition = moduleReader.GetMethodDefinition(
            (MethodDefinitionHandle)MetadataTokens.Handle(selectedMethodToken));
        var methodGenericParameters = ProjectGenericParameters(
            moduleReader,
            selectedDefinition.GetGenericParameters(),
            selectedMethodToken);
        var localSignature = ProjectLocalSignature(moduleReader, modulePath, selectedDefinition);

        using var pdbStream = File.OpenRead(portablePdbPath);
        using var pdbProvider = MetadataReaderProvider.FromPortablePdbStream(pdbStream);
        var pdbReader = pdbProvider.GetMetadataReader();
        var (scopes, variables, constants) = ProjectPortablePdbTables(pdbReader);

        var selectedName = methods
            .Single(method => method.MethodDefinitionToken == selectedMethodToken)
            .Name;
        var generatedLocalFunctionRowCount = methods.Count(method =>
            DumpGeneratedLocalFunctionRowFact.TryCreate(method, out var generated) &&
            string.Equals(generated!.ContainingMethodName, selectedName, StringComparison.Ordinal));

        var sourceEnds = DumpSelectedMethodLexicalSourceEnds.Create(
            methodDefinitionRowCount: methods.Length,
            parameterRowCount: moduleReader.GetTableRowCount(TableIndex.Param),
            declaringTypeGenericParameterRowCount: typeGenericParameters.Length,
            methodGenericParameterRowCount: methodGenericParameters.Length,
            localScopeRowCount: scopes.Length,
            localVariableRowCount: variables.Length,
            localConstantRowCount: constants.Length,
            maximumMethodSignatureByteCount: methods.Max(static method => method.Signature.Length),
            aggregateMethodSignatureByteCount: methods.Sum(static method => (long)method.Signature.Length),
            maximumSignatureTypeDepth: Math.Max(
                methods.Max(static method => method.SignatureTypeDepth),
                localSignature?.SignatureTypeDepth ?? 0),
            maximumSignatureTypeNodeCount: Math.Max(
                methods.Max(static method => method.SignatureTypeNodeCount),
                localSignature?.SignatureTypeNodeCount ?? 0),
            standaloneSignatureByteCount: localSignature?.Signature.Length ?? 0,
            localSlotCount: localSignature?.LocalSlotCount ?? 0,
            maximumLocalConstantSignatureByteCount: constants.IsEmpty
                ? 0
                : constants.Max(static constant => constant.Signature.Length),
            aggregateLocalConstantSignatureByteCount: constants.Sum(
                static constant => (long)constant.Signature.Length),
            generatedLocalFunctionRowCount: generatedLocalFunctionRowCount);

        return DumpSelectedMethodLexicalObservation.Acquire(
            portablePdbContext,
            sourceEnds,
            methods,
            typeGenericParameters,
            methodGenericParameters,
            localSignature,
            scopes,
            variables,
            constants);
    }

    /// <summary>Reads every MethodDef row with its complete physical ParamList ownership interval.</summary>
    private static ImmutableArray<DumpLexicalMethodDefinitionRowFact> ProjectMethodDefinitionTable(
        MetadataReader reader)
    {
        var owners = new Dictionary<int, int>();
        foreach (var typeHandle in reader.TypeDefinitions)
        {
            var typeToken = MetadataTokens.GetToken(typeHandle);
            foreach (var methodHandle in reader.GetTypeDefinition(typeHandle).GetMethods())
            {
                owners[MetadataTokens.GetToken(methodHandle)] = typeToken;
            }
        }

        var handles = reader.MethodDefinitions.ToArray();
        var parameterRowCount = reader.GetTableRowCount(TableIndex.Param);
        var parameterListStarts = RecoverListStarts(
            handles.Length,
            parameterRowCount,
            index => FirstRowId(reader.GetMethodDefinition(handles[index]).GetParameters()));

        var methods = ImmutableArray.CreateBuilder<DumpLexicalMethodDefinitionRowFact>(handles.Length);
        for (var index = 0; index < handles.Length; index++)
        {
            var handle = handles[index];
            var definition = reader.GetMethodDefinition(handle);
            var signature = ImmutableArray.CreateRange(reader.GetBlobBytes(definition.Signature));
            if (!BoundedEcmaSignatureProjection.TryDecodeMethodDefinition(
                    signature.AsSpan(),
                    DumpSelectedMethodLexicalLimits.MaximumMethodSignatureByteCount,
                    DumpSelectedMethodLexicalLimits.MaximumSignatureTypeDepth,
                    DumpSelectedMethodLexicalLimits.MaximumSignatureTypeNodeCount,
                    out var decoded))
            {
                throw new InvalidOperationException(
                    "The shape module declares a MethodDef signature the shared bounded grammar cannot decode.");
            }

            var parameters = ImmutableArray.CreateBuilder<DumpLexicalParameterRowFact>();
            foreach (var parameterHandle in definition.GetParameters())
            {
                var parameter = reader.GetParameter(parameterHandle);
                parameters.Add(DumpLexicalParameterRowFact.Create(
                    MetadataTokens.GetToken(parameterHandle),
                    parameter.SequenceNumber,
                    parameter.Attributes,
                    reader.GetString(parameter.Name)));
            }

            methods.Add(DumpLexicalMethodDefinitionRowFact.Create(
                MetadataTokens.GetToken(handle),
                owners[MetadataTokens.GetToken(handle)],
                definition.RelativeVirtualAddress,
                definition.Attributes,
                definition.ImplAttributes,
                reader.GetString(definition.Name),
                signature,
                decoded.ParameterCount,
                parameterListStarts[index],
                parameters.ToImmutable()));
        }

        return methods.MoveToImmutable();
    }

    /// <summary>Reads the complete GenericParam rows of one selected TypeDef or MethodDef owner.</summary>
    private static ImmutableArray<DumpLexicalGenericParameterRowFact> ProjectGenericParameters(
        MetadataReader reader,
        GenericParameterHandleCollection handles,
        int ownerToken)
    {
        var rows = ImmutableArray.CreateBuilder<DumpLexicalGenericParameterRowFact>(handles.Count);
        foreach (var handle in handles)
        {
            var parameter = reader.GetGenericParameter(handle);
            rows.Add(DumpLexicalGenericParameterRowFact.Create(
                MetadataTokens.GetToken(handle),
                ownerToken,
                parameter.Index,
                parameter.Attributes,
                reader.GetString(parameter.Name)));
        }

        return rows.MoveToImmutable();
    }

    /// <summary>
    /// Reads the selected method's complete local StandAloneSig, naming it through the content-identical on-disk
    /// artifact's own method body because a method-body header is the only physical place that token appears.
    /// </summary>
    private static DumpLexicalStandaloneSignatureFact? ProjectLocalSignature(
        MetadataReader reader,
        string modulePath,
        MethodDefinition selectedDefinition)
    {
        if (selectedDefinition.RelativeVirtualAddress == 0)
        {
            return null;
        }

        using var stream = File.OpenRead(modulePath);
        using var peReader = new PEReader(stream);
        var body = peReader.GetMethodBody(selectedDefinition.RelativeVirtualAddress);
        if (body.LocalSignature.IsNil)
        {
            return null;
        }

        var signature = ImmutableArray.CreateRange(
            reader.GetBlobBytes(reader.GetStandaloneSignature(body.LocalSignature).Signature));
        if (!BoundedEcmaSignatureProjection.TryDecodeLocal(
                signature.AsSpan(),
                DumpSelectedMethodLexicalLimits.MaximumStandAloneSignatureByteCount,
                DumpSelectedMethodLexicalLimits.MaximumSignatureTypeDepth,
                DumpSelectedMethodLexicalLimits.MaximumSignatureTypeNodeCount,
                out var decoded))
        {
            throw new InvalidOperationException(
                "The selected method declares a local signature the shared bounded grammar cannot decode.");
        }

        return DumpLexicalStandaloneSignatureFact.Create(
            MetadataTokens.GetToken(body.LocalSignature),
            decoded.LocalSlotCount,
            signature);
    }

    /// <summary>Reads the complete physical LocalScope, LocalVariable, and LocalConstant tables.</summary>
    private static (
        ImmutableArray<DumpLexicalLocalScopeRowFact> Scopes,
        ImmutableArray<DumpLexicalLocalVariableRowFact> Variables,
        ImmutableArray<DumpLexicalLocalConstantRowFact> Constants) ProjectPortablePdbTables(MetadataReader reader)
    {
        var handles = reader.LocalScopes.ToArray();
        var variableRowCount = reader.GetTableRowCount(TableIndex.LocalVariable);
        var constantRowCount = reader.GetTableRowCount(TableIndex.LocalConstant);
        var variableStarts = RecoverListStarts(
            handles.Length,
            variableRowCount,
            index => FirstRowId(reader.GetLocalScope(handles[index]).GetLocalVariables()));
        var constantStarts = RecoverListStarts(
            handles.Length,
            constantRowCount,
            index => FirstRowId(reader.GetLocalScope(handles[index]).GetLocalConstants()));

        var scopes = ImmutableArray.CreateBuilder<DumpLexicalLocalScopeRowFact>(handles.Length);
        var variables = ImmutableArray.CreateBuilder<DumpLexicalLocalVariableRowFact>(variableRowCount);
        var constants = ImmutableArray.CreateBuilder<DumpLexicalLocalConstantRowFact>(constantRowCount);
        for (var index = 0; index < handles.Length; index++)
        {
            var scopeToken = MetadataTokens.GetToken(handles[index]);
            var scope = reader.GetLocalScope(handles[index]);
            scopes.Add(DumpLexicalLocalScopeRowFact.Create(
                scopeToken,
                MetadataTokens.GetToken(scope.Method),
                scope.ImportScope.IsNil ? null : MetadataTokens.GetToken(scope.ImportScope),
                scope.StartOffset,
                scope.Length,
                variableStarts[index],
                constantStarts[index]));

            // Ownership is read from the scope's own child list rather than re-derived from the recovered interval,
            // so a variable or constant row can never be attributed to a scope the PDB did not give it to.
            foreach (var variableHandle in scope.GetLocalVariables())
            {
                var variable = reader.GetLocalVariable(variableHandle);
                variables.Add(DumpLexicalLocalVariableRowFact.Create(
                    MetadataTokens.GetToken(variableHandle),
                    scopeToken,
                    variable.Index,
                    variable.Attributes,
                    reader.GetString(variable.Name)));
            }

            foreach (var constantHandle in scope.GetLocalConstants())
            {
                var constant = reader.GetLocalConstant(constantHandle);
                constants.Add(DumpLexicalLocalConstantRowFact.Create(
                    MetadataTokens.GetToken(constantHandle),
                    scopeToken,
                    reader.GetString(constant.Name),
                    ImmutableArray.CreateRange(reader.GetBlobBytes(constant.Signature))));
            }
        }

        return (scopes.MoveToImmutable(), variables.ToImmutable(), constants.ToImmutable());
    }

    /// <summary>
    /// Recovers every row's child-list start RID from the physical nondecreasing-list invariant: a row that owns
    /// children starts at its first child, and an empty row starts exactly where the next row does.
    /// </summary>
    private static ImmutableArray<int> RecoverListStarts(
        int rowCount,
        int childRowCount,
        Func<int, int?> firstOwnedRowId)
    {
        var starts = new int[rowCount];
        var next = childRowCount + 1;
        for (var index = rowCount - 1; index >= 0; index--)
        {
            starts[index] = firstOwnedRowId(index) ?? next;
            next = starts[index];
        }

        return [.. starts];
    }

    private static int? FirstRowId(ParameterHandleCollection handles)
    {
        foreach (var handle in handles)
        {
            return MetadataTokens.GetRowNumber(handle);
        }

        return null;
    }

    private static int? FirstRowId(LocalVariableHandleCollection handles)
    {
        foreach (var handle in handles)
        {
            return MetadataTokens.GetRowNumber(handle);
        }

        return null;
    }

    private static int? FirstRowId(LocalConstantHandleCollection handles)
    {
        foreach (var handle in handles)
        {
            return MetadataTokens.GetRowNumber(handle);
        }

        return null;
    }
}
