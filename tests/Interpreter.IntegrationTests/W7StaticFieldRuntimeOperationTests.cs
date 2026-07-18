using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Interpreter.Host.Abstractions;
using Interpreter.Host.Dump.ClrMD;
using Interpreter.Product.DumpQuery;
using Xunit;

namespace Interpreter.IntegrationTests;

/// <summary>Exercises the real W7 runtime declaration mapping over optimized synthetic dumps.</summary>
public sealed class W7StaticFieldRuntimeOperationTests
{
    /// <summary>
    /// Exercises the public one-call evaluator over all seven scalar, nullable, string, class, interface, array-base,
    /// and object-base declarations, proves static object provenance, retains typed syntax/symbol stops, and replays
    /// every complete pipeline envelope byte-for-byte from a freshly opened session.
    /// </summary>
    [Fact]
    [Trait("Category", "Dump")]
    [Trait("Corpus", "W7StaticExpressionPipelineV1")]
    public void Public_static_expression_pipeline_is_typed_composable_and_replayable()
    {
        var executable = W7TestTargetPaths.ResolveExecutable();
        Assert.True(File.Exists(executable), $"Expected the W7 target at '{executable}'.");
        var dumpPath = Path.Combine(Path.GetTempPath(), $"w7-static-pipeline-{Guid.NewGuid():N}.dmp");
        try
        {
            using (var target = TestTargetRunner.StartAndWaitReady(
                       executable,
                       ["--incident", "batch-imported-direct-field"],
                       isolatedDirectory: null))
            {
                DumpWriter.WriteFullDump(target.Pid, dumpPath);
            }

            var first = EvaluatePipeline(dumpPath);
            var replay = EvaluatePipeline(dumpPath);
            Assert.Equal(first.Length, replay.Length);
            for (var index = 0; index < first.Length; index++)
            {
                Assert.Equal(first[index], replay[index]);
                Assert.Equal(first[index].Sha256, replay[index].Sha256);
                Assert.True(first[index].CanonicalBytes.AsSpan().SequenceEqual(replay[index].CanonicalBytes.AsSpan()));
            }

            Assert.Collection(
                first,
                result => AssertExact(result, "TotalItems", ClrmdStaticFieldTerminalKind.Int32),
                result => AssertExact(result, "State", ClrmdStaticFieldTerminalKind.String),
                result =>
                {
                    AssertExact(result, "Progress", ClrmdStaticFieldTerminalKind.NullableInt32NoValue);
                    Assert.NotNull(result.RawNullableLayout);
                    Assert.NotNull(result.NullableLayout);
                    Assert.NotEmpty(result.NullableLayoutBounds);
                },
                result => AssertStaticObject(
                    result,
                    "Root",
                    StaticFieldRuntimeAssignabilityKind.ExactRuntimeType),
                result => AssertStaticObject(
                    result,
                    "InterfaceRoot",
                    StaticFieldRuntimeAssignabilityKind.InterfaceClosure),
                result => AssertStaticObject(
                    result,
                    "NumberArray",
                    StaticFieldRuntimeAssignabilityKind.SystemArray),
                result => AssertStaticObject(
                    result,
                    "ObjectArray",
                    StaticFieldRuntimeAssignabilityKind.SystemObject),
                result =>
                {
                    Assert.Equal(StaticFieldExpressionEvaluationStage.Syntax, result.Stage);
                    Assert.Equal(StaticFieldExpressionEvaluationStatus.Invalid, result.Status);
                    Assert.Null(result.SymbolBinding);
                    Assert.Null(result.Observation);
                    Assert.Equal(StaticFieldSyntaxIssue.ParserDiagnostic, result.Syntax.Issue);
                },
                result =>
                {
                    Assert.Equal(StaticFieldExpressionEvaluationStage.Syntax, result.Stage);
                    Assert.Equal(StaticFieldExpressionEvaluationStatus.Unsupported, result.Status);
                    Assert.Null(result.SymbolBinding);
                    Assert.Null(result.Observation);
                    Assert.Equal(StaticFieldSyntaxIssue.TreeShapeUnsupported, result.Syntax.Issue);
                },
                result =>
                {
                    Assert.Equal(StaticFieldExpressionEvaluationStage.SymbolBinding, result.Stage);
                    Assert.Equal(StaticFieldExpressionEvaluationStatus.Absent, result.Status);
                    Assert.Equal(StaticFieldBindingStatus.Absent, result.SymbolBinding!.Status);
                    Assert.Equal(StaticFieldSymbolIdentityStatus.Failed, result.Observation!.SymbolStatus);
                    Assert.Null(result.HostObservation);
                });

            static void AssertExact(
                StaticFieldExpressionEvaluationResult result,
                string fieldName,
                ClrmdStaticFieldTerminalKind terminalKind)
            {
                Assert.Equal(StaticFieldExpressionEvaluationStage.Complete, result.Stage);
                Assert.Equal(StaticFieldExpressionEvaluationStatus.Exact, result.Status);
                Assert.Equal(fieldName, result.SymbolBinding!.SelectedDeclaration!.FieldName);
                Assert.NotNull(result.RuntimeDeclaration);
                Assert.NotEmpty(result.RuntimeDeclarationEvidence);
                Assert.NotEmpty(result.RuntimeDeclarationBounds);
                Assert.Equal(ClrmdStaticFieldObservationStatus.Exact, result.HostObservation!.Status);
                Assert.Equal(terminalKind, result.HostObservation.Value!.Kind);
                Assert.Equal(result.HostObservation, result.Observation!.HostObservation);
                Assert.Null(result.RuntimeIssue);
                Assert.Null(result.DiagnosticCode);
                Assert.Null(result.DiagnosticMessage);
            }

            static void AssertStaticObject(
                StaticFieldExpressionEvaluationResult result,
                string fieldName,
                StaticFieldRuntimeAssignabilityKind assignabilityKind)
            {
                AssertExact(result, fieldName, ClrmdStaticFieldTerminalKind.ObjectReference);
                var proof = Assert.IsType<StaticFieldRuntimeAssignabilityProof>(
                    result.Observation!.RuntimeAssignabilityProof);
                Assert.Equal(assignabilityKind, proof.Kind);
                var binding = Assert.IsType<DumpObjectBinding>(result.ObjectBinding);
                Assert.Equal(DumpObjectProvenanceKind.StaticFieldExpression, binding.Provenance.Kind);
                Assert.Equal(proof.ObjectReference.Address, binding.Identity.Address);
                Assert.Equal(result.Observation, binding.Provenance.StaticField!.Observation);
            }
        }
        finally
        {
            if (File.Exists(dumpPath))
            {
                File.Delete(dumpPath);
            }
        }
    }

    /// <summary>
    /// Proves scalar, bounded string, nullable no-value, and non-null object terminals are decoded from ordered raw
    /// memory reads after exact domain and slot acquisition, then replay byte-for-byte in a fresh session.
    /// </summary>
    [Fact]
    [Trait("Category", "Dump")]
    [Trait("Corpus", "W7StaticStorageReadV1")]
    public void Real_static_storage_decoders_produce_exact_replayable_terminals()
    {
        var executable = W7TestTargetPaths.ResolveExecutable();
        Assert.True(File.Exists(executable), $"Expected the W7 target at '{executable}'.");
        var dumpPath = Path.Combine(Path.GetTempPath(), $"w7-static-storage-{Guid.NewGuid():N}.dmp");
        try
        {
            using (var target = TestTargetRunner.StartAndWaitReady(
                       executable,
                       ["--incident", "batch-imported-direct-field"],
                       isolatedDirectory: null))
            {
                DumpWriter.WriteFullDump(target.Pid, dumpPath);
            }

            var first = ObserveValues(dumpPath);
            var replay = ObserveValues(dumpPath);

            Assert.Equal(first, replay);
            Assert.Collection(
                first.OrderBy(static value => value.FieldName, StringComparer.Ordinal),
                value => AssertReference(
                    value,
                    "InterfaceRoot",
                    StaticFieldRuntimeAssignabilityKind.InterfaceClosure),
                value => AssertReference(
                    value,
                    "NumberArray",
                    StaticFieldRuntimeAssignabilityKind.SystemArray),
                value => AssertReference(
                    value,
                    "ObjectArray",
                    StaticFieldRuntimeAssignabilityKind.SystemObject),
                value =>
                {
                    Assert.Equal("Progress", value.FieldName);
                    Assert.Equal(ClrmdStaticFieldTerminalKind.NullableInt32NoValue, value.TerminalKind);
                    Assert.Null(value.Int32Value);
                    Assert.Null(value.StringValue);
                    Assert.Null(value.ObjectAddress);
                    Assert.Null(value.AssignabilityKind);
                    Assert.Equal(3, value.RawReadCount);
                },
                value =>
                {
                    Assert.Equal("Root", value.FieldName);
                    Assert.Equal(ClrmdStaticFieldTerminalKind.ObjectReference, value.TerminalKind);
                    Assert.Null(value.Int32Value);
                    Assert.Null(value.StringValue);
                    Assert.NotNull(value.ObjectAddress);
                    Assert.NotEqual(0UL, value.ObjectAddress!.Value);
                    Assert.Equal(StaticFieldRuntimeAssignabilityKind.ExactRuntimeType, value.AssignabilityKind);
                    Assert.Equal(2, value.RawReadCount);
                },
                value =>
                {
                    Assert.Equal("State", value.FieldName);
                    Assert.Equal(ClrmdStaticFieldTerminalKind.String, value.TerminalKind);
                    Assert.Null(value.Int32Value);
                    Assert.Equal("processing", value.StringValue);
                    Assert.NotNull(value.ObjectAddress);
                    Assert.Equal(StaticFieldRuntimeAssignabilityKind.ExactRuntimeType, value.AssignabilityKind);
                    Assert.Equal(4, value.RawReadCount);
                },
                value =>
                {
                    Assert.Equal("TotalItems", value.FieldName);
                    Assert.Equal(ClrmdStaticFieldTerminalKind.Int32, value.TerminalKind);
                    Assert.Equal(0x7A17C042, value.Int32Value);
                    Assert.Null(value.StringValue);
                    Assert.Null(value.ObjectAddress);
                    Assert.Null(value.AssignabilityKind);
                    Assert.Equal(1, value.RawReadCount);
                });
            Assert.All(first, static value =>
            {
                Assert.Equal(ClrmdStaticFieldObservationStatus.Exact, value.Status);
                Assert.Equal(ClrmdStaticStorageAcquisitionKind.SlotAddressAcquired, value.AcquisitionKind);
                Assert.NotEqual(0UL, value.SlotAddress);
            });

            static void AssertReference(
                ValueObservation value,
                string fieldName,
                StaticFieldRuntimeAssignabilityKind assignabilityKind)
            {
                Assert.Equal(fieldName, value.FieldName);
                Assert.Equal(ClrmdStaticFieldTerminalKind.ObjectReference, value.TerminalKind);
                Assert.Null(value.Int32Value);
                Assert.Null(value.StringValue);
                Assert.NotNull(value.ObjectAddress);
                Assert.NotEqual(0UL, value.ObjectAddress!.Value);
                Assert.Equal(assignabilityKind, value.AssignabilityKind);
                Assert.Equal(2, value.RawReadCount);
            }
        }
        finally
        {
            if (File.Exists(dumpPath))
            {
                File.Delete(dumpPath);
            }
        }
    }

    /// <summary>
    /// Proves one optimized incident maps scalar, string, nullable, and managed-reference FieldDefs through complete
    /// TypeDef-to-MethodTable and directly owned static-field catalogs, then replays identical canonical evidence in a
    /// fresh dump session.
    /// </summary>
    [Fact]
    [Trait("Category", "Dump")]
    [Trait("Corpus", "W7StaticRuntimeMappingV1")]
    public void Real_runtime_mapping_exhausts_catalogs_for_all_admitted_storage_shapes()
    {
        var executable = W7TestTargetPaths.ResolveExecutable();
        Assert.True(File.Exists(executable), $"Expected the W7 target at '{executable}'.");
        var dumpPath = Path.Combine(Path.GetTempPath(), $"w7-static-mapping-{Guid.NewGuid():N}.dmp");
        try
        {
            using (var target = TestTargetRunner.StartAndWaitReady(
                       executable,
                       ["--incident", "batch-imported-direct-field"],
                       isolatedDirectory: null))
            {
                DumpWriter.WriteFullDump(target.Pid, dumpPath);
            }

            var first = ObserveMappings(dumpPath);
            var replay = ObserveMappings(dumpPath);

            Assert.Equal(first, replay);
            Assert.Equal(3, first.Length);
            Assert.All(first, static mapping =>
            {
                Assert.True(mapping.RuntimeTypesExamined > 0);
                Assert.True(mapping.StaticFieldsExamined > 0);
                Assert.Equal(1, mapping.DeclaringTypeMatchesRetained);
                Assert.Equal(1, mapping.StaticFieldMatchesRetained);
                Assert.True(mapping.MetadataReadCount >= 1);
            });
            Assert.Collection(
                first.OrderBy(static mapping => mapping.FieldName, StringComparer.Ordinal),
                mapping =>
                {
                    Assert.Equal("Progress", mapping.FieldName);
                    Assert.Equal(ClrmdStaticExpectedDecoderKind.NullableInt32, mapping.Decoder);
                    Assert.Equal("System.Nullable<System.Int32>", mapping.ObservedFieldTypeName);
                    Assert.Equal(1, mapping.ObservedGenericArgumentCount);
                },
                mapping =>
                {
                    Assert.Equal("Root", mapping.FieldName);
                    Assert.Equal(ClrmdStaticExpectedDecoderKind.ManagedReference, mapping.Decoder);
                    Assert.Equal("Interpreter.W7TestTarget.Batch.BatchRoot", mapping.ObservedFieldTypeName);
                    Assert.Equal(0, mapping.ObservedGenericArgumentCount);
                },
                mapping =>
                {
                    Assert.Equal("State", mapping.FieldName);
                    Assert.Equal(ClrmdStaticExpectedDecoderKind.String, mapping.Decoder);
                    Assert.Equal("System.String", mapping.ObservedFieldTypeName);
                    Assert.Equal(0, mapping.ObservedGenericArgumentCount);
                });
        }
        finally
        {
            if (File.Exists(dumpPath))
            {
                File.Delete(dumpPath);
            }
        }
    }

    private static MappingObservation[] ObserveMappings(string dumpPath)
    {
        var opened = ClrmdDumpSession.Open(dumpPath);
        Assert.Equal(ClrmdEvidenceStatus.Exact, opened.Status);
        using var session = Assert.IsType<ClrmdDumpSession>(opened.Value);
        var module = Assert.Single(session.Modules, static candidate => string.Equals(
            candidate.Name,
            W7TestTargetPaths.AssemblyFileName,
            StringComparison.Ordinal));
        var content = session.ReadModuleContentIdentity(module);
        Assert.Equal(ClrmdEvidenceStatus.Exact, content.Status);
        var metadataRead = Assert.Single(content.Evidence);
        using var provider = MetadataReaderProvider.FromMetadataImage(metadataRead.Bytes);
        var reader = provider.GetMetadataReader();
        var typeHandle = Assert.Single(reader.TypeDefinitions, handle => IsType(
            reader,
            handle,
            "Interpreter.W7TestTarget.Batch",
            "BatchStatics"));
        var type = reader.GetTypeDefinition(typeHandle);

        return new[]
        {
            Map("State", ClrmdStaticExpectedDecoderKind.String),
            Map("Progress", ClrmdStaticExpectedDecoderKind.NullableInt32),
            Map("Root", ClrmdStaticExpectedDecoderKind.ManagedReference),
        };

        MappingObservation Map(string fieldName, ClrmdStaticExpectedDecoderKind decoder)
        {
            var fieldHandle = Assert.Single(type.GetFields(), handle => string.Equals(
                reader.GetString(reader.GetFieldDefinition(handle).Name),
                fieldName,
                StringComparison.Ordinal));
            var result = session.MapStaticFieldDeclaration(
                module,
                MetadataTokens.GetToken(typeHandle),
                "Interpreter.W7TestTarget.Batch.BatchStatics",
                MetadataTokens.GetToken(fieldHandle),
                fieldName,
                decoder);
            Assert.Equal(ClrmdEvidenceStatus.Exact, result.Status);
            Assert.Equal(ClrmdValueIssue.None, result.Issue);
            var mapping = Assert.IsType<ClrmdStaticRuntimeDeclarationMappingIdentity>(result.Value);
            Assert.All(result.Evidence, static read => Assert.Equal(MemoryReadStatus.Exact, read.Status));
            return new MappingObservation(
                mapping.Field.Name,
                mapping.Field.ExpectedDecoderKind,
                mapping.Field.ObservedFieldType.FullName,
                mapping.Field.ObservedFieldType.GenericArguments.Length,
                mapping.Counters.RuntimeTypesExamined,
                mapping.Counters.RuntimeStaticFieldsExamined,
                mapping.Counters.DeclaringTypeMatchesRetained,
                mapping.Counters.StaticFieldMatchesRetained,
                result.Evidence.Length,
                mapping.Sha256);
        }
    }

    private static StaticFieldExpressionEvaluationResult[] EvaluatePipeline(string dumpPath)
    {
        var opened = ClrmdDumpSession.Open(dumpPath);
        Assert.Equal(ClrmdEvidenceStatus.Exact, opened.Status);
        using var session = Assert.IsType<ClrmdDumpSession>(opened.Value);
        const string owner = "Interpreter.W7TestTarget.Batch.BatchStatics";
        return
        [
            StaticFieldExpressionEvaluator.Evaluate(session, $"global::{owner}.TotalItems"),
            StaticFieldExpressionEvaluator.Evaluate(session, $"global::{owner}.State"),
            StaticFieldExpressionEvaluator.Evaluate(session, $"global::{owner}.Progress"),
            StaticFieldExpressionEvaluator.Evaluate(session, $"global::{owner}.Root"),
            StaticFieldExpressionEvaluator.Evaluate(session, $"global::{owner}.InterfaceRoot"),
            StaticFieldExpressionEvaluator.Evaluate(session, $"global::{owner}.NumberArray"),
            StaticFieldExpressionEvaluator.Evaluate(session, $"global::{owner}.ObjectArray"),
            StaticFieldExpressionEvaluator.Evaluate(session, "global::"),
            StaticFieldExpressionEvaluator.Evaluate(session, $"global::{owner}.Root.ToString()"),
            StaticFieldExpressionEvaluator.Evaluate(
                session,
                "global::Interpreter.W7TestTarget.Missing.NoSuchType.Value"),
        ];
    }

    private static ValueObservation[] ObserveValues(string dumpPath)
    {
        var opened = ClrmdDumpSession.Open(dumpPath);
        Assert.Equal(ClrmdEvidenceStatus.Exact, opened.Status);
        using var session = Assert.IsType<ClrmdDumpSession>(opened.Value);
        var module = Assert.Single(session.Modules, static candidate => string.Equals(
            candidate.Name,
            W7TestTargetPaths.AssemblyFileName,
            StringComparison.Ordinal));
        var content = session.ReadModuleContentIdentity(module);
        Assert.Equal(ClrmdEvidenceStatus.Exact, content.Status);
        var metadataRead = Assert.Single(content.Evidence);
        using var provider = MetadataReaderProvider.FromMetadataImage(metadataRead.Bytes);
        var reader = provider.GetMetadataReader();

        return new[]
        {
            Read(
                "Interpreter.W7TestTarget.Batch",
                "BatchStatics",
                "TotalItems",
                ClrmdStaticExpectedDecoderKind.Int32),
            Read(
                "Interpreter.W7TestTarget.Batch",
                "BatchStatics",
                "State",
                ClrmdStaticExpectedDecoderKind.String),
            Read(
                "Interpreter.W7TestTarget.Batch",
                "BatchStatics",
                "Progress",
                ClrmdStaticExpectedDecoderKind.NullableInt32),
            Read(
                "Interpreter.W7TestTarget.Batch",
                "BatchStatics",
                "Root",
                ClrmdStaticExpectedDecoderKind.ManagedReference),
            Read(
                "Interpreter.W7TestTarget.Batch",
                "BatchStatics",
                "InterfaceRoot",
                ClrmdStaticExpectedDecoderKind.ManagedReference),
            Read(
                "Interpreter.W7TestTarget.Batch",
                "BatchStatics",
                "NumberArray",
                ClrmdStaticExpectedDecoderKind.ManagedReference),
            Read(
                "Interpreter.W7TestTarget.Batch",
                "BatchStatics",
                "ObjectArray",
                ClrmdStaticExpectedDecoderKind.ManagedReference),
        };

        ValueObservation Read(
            string namespaceName,
            string typeName,
            string fieldName,
            ClrmdStaticExpectedDecoderKind decoder)
        {
            var typeHandle = Assert.Single(reader.TypeDefinitions, handle => IsType(
                reader,
                handle,
                namespaceName,
                typeName));
            var type = reader.GetTypeDefinition(typeHandle);
            var fieldHandle = Assert.Single(type.GetFields(), handle => string.Equals(
                reader.GetString(reader.GetFieldDefinition(handle).Name),
                fieldName,
                StringComparison.Ordinal));
            var runtimeName = namespaceName.Length == 0 ? typeName : $"{namespaceName}.{typeName}";
            var mapped = session.MapStaticFieldDeclaration(
                module,
                MetadataTokens.GetToken(typeHandle),
                runtimeName,
                MetadataTokens.GetToken(fieldHandle),
                fieldName,
                decoder);
            Assert.True(
                mapped.Status == ClrmdEvidenceStatus.Exact,
                $"Mapping {runtimeName}.{fieldName} stopped as {mapped.Status}/{mapped.Issue}.");
            var mapping = Assert.IsType<ClrmdStaticRuntimeDeclarationMappingIdentity>(mapped.Value);
            var syntax = StaticFieldExpressionParser.Parse($"global::{runtimeName}.{fieldName}");
            var descriptor = Assert.IsType<StaticFieldExpressionDescriptor>(syntax.Descriptor);
            var binding = StaticFieldFullyQualifiedBinder.Bind(session, descriptor);
            Assert.Equal(StaticFieldBindingStatus.Exact, binding.Status);
            StaticFieldNullableInt32RuntimeLayoutIdentity? semanticLayout = null;
            if (decoder == ClrmdStaticExpectedDecoderKind.NullableInt32)
            {
                var rawLayoutResult = session.MapStaticNullableRuntimeLayout(mapping);
                Assert.Equal(ClrmdEvidenceStatus.Exact, rawLayoutResult.Status);
                var rawLayout = Assert.IsType<ClrmdStaticNullableRuntimeLayoutIdentity>(rawLayoutResult.Value);
                Assert.Equal(2, rawLayout.RuntimeFieldCount);
                var hasValue = Assert.Single(rawLayout.Fields, static field => string.Equals(
                    field.Name,
                    "hasValue",
                    StringComparison.Ordinal));
                var valueChild = Assert.Single(rawLayout.Fields, static field => string.Equals(
                    field.Name,
                    "value",
                    StringComparison.Ordinal));
                Assert.Equal("System.Boolean", hasValue.ObservedType.FullName);
                Assert.Equal("System.Int32", valueChild.ObservedType.FullName);
                semanticLayout = StaticFieldRuntimeComposer.ComposeNullableInt32Layout(
                    session,
                    binding,
                    rawLayout);
                Assert.Equal(hasValue.Offset, semanticLayout.HasValueRuntimeField.Offset);
                Assert.Equal(valueChild.Offset, semanticLayout.ValueRuntimeField.Offset);
            }
            var request = StaticFieldObservation.CreatePhysicalRequest(binding, mapping, semanticLayout);
            var observation = session.ReadStaticField(request);
            Assert.True(
                observation.Status == ClrmdStaticFieldObservationStatus.Exact,
                $"Reading {runtimeName}.{fieldName} stopped as {observation.Status}/{observation.Issue} " +
                $"after {observation.Reads.Length} raw reads: " +
                string.Join(", ", observation.Reads.Select(static read =>
                    $"0x{read.Address:x}:{Convert.ToHexString(read.Bytes.AsSpan())}")));
            Assert.Equal(ClrmdValueIssue.None, observation.Issue);
            var value = Assert.IsType<ClrmdStaticFieldValue>(observation.Value);
            var objectReference = value.Kind switch
            {
                ClrmdStaticFieldTerminalKind.String => value.StringValue!.ObjectReference,
                ClrmdStaticFieldTerminalKind.ObjectReference => value.ObjectReference,
                _ => null,
            };
            StaticFieldRuntimeAssignabilityProof? assignability = null;
            if (objectReference is not null)
            {
                assignability = StaticFieldRuntimeComposer.ProveReferenceAssignability(
                    session,
                    binding,
                    observation);
                Assert.Equal(objectReference, assignability.ObjectReference);
            }
            var productObservation = StaticFieldObservation.FromExactSymbol(
                binding,
                observation,
                semanticLayout,
                assignability);
            Assert.Equal(observation, productObservation.HostObservation);
            return new ValueObservation(
                fieldName,
                observation.Status,
                observation.StorageAcquisitionEvidence!.Kind,
                observation.SlotAddress!.Value,
                observation.Reads.Length,
                value.Kind,
                value.Int32Value,
                value.StringValue?.Value,
                objectReference?.Address,
                assignability?.Kind,
                observation.Sha256);
        }
    }

    private static bool IsType(
        MetadataReader reader,
        TypeDefinitionHandle handle,
        string namespaceName,
        string typeName)
    {
        var type = reader.GetTypeDefinition(handle);
        return string.Equals(reader.GetString(type.Namespace), namespaceName, StringComparison.Ordinal) &&
            string.Equals(reader.GetString(type.Name), typeName, StringComparison.Ordinal);
    }

    private sealed record MappingObservation(
        string FieldName,
        ClrmdStaticExpectedDecoderKind Decoder,
        string ObservedFieldTypeName,
        int ObservedGenericArgumentCount,
        int RuntimeTypesExamined,
        int StaticFieldsExamined,
        int DeclaringTypeMatchesRetained,
        int StaticFieldMatchesRetained,
        int MetadataReadCount,
        string Sha256);

    private sealed record ValueObservation(
        string FieldName,
        ClrmdStaticFieldObservationStatus Status,
        ClrmdStaticStorageAcquisitionKind AcquisitionKind,
        ulong SlotAddress,
        int RawReadCount,
        ClrmdStaticFieldTerminalKind TerminalKind,
        int? Int32Value,
        string? StringValue,
        ulong? ObjectAddress,
        StaticFieldRuntimeAssignabilityKind? AssignabilityKind,
        string Sha256);
}
