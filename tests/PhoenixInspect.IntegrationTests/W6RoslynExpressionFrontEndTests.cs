using System.Collections.Immutable;
using System.Text.Json;
using PhoenixInspect.Core.Abstractions;
using PhoenixInspect.Host.Abstractions;
using PhoenixInspect.Host.Dump.ClrMD;
using PhoenixInspect.Product.DumpDebugging;
using PhoenixInspect.Product.DumpQuery;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace PhoenixInspect.IntegrationTests;

/// <summary>
/// Proves that one pinned complete C# expression parse feeds narrow project-owned W2, W5, and opt-in W6 recognizers.
/// </summary>
/// <remarks>
/// These are W6.2 front-end tests only. Admission neither binds a metadata member nor reads target memory.
/// </remarks>
public sealed class W6RoslynExpressionFrontEndTests
{
    private static readonly CSharpParseOptions ExpectedOptions = new(
        LanguageVersion.CSharp14,
        DocumentationMode.None,
        SourceCodeKind.Regular,
        preprocessorSymbols: Array.Empty<string>());

    /// <summary>
    /// Proves the source-controlled three-bucket corpus distinguishes admitted project shapes, rich valid C# trees,
    /// and malformed near-neighbors before any evidence capability exists.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void ThreeBucketCorpusSeparatesParsingFromSubsetAdmission()
    {
        using var corpus = JsonDocument.Parse(File.ReadAllBytes(ResolveCorpusPath()));
        var root = corpus.RootElement;
        Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
        Assert.Equal(DumpCSharpExpressionProfile.Id, root.GetProperty("profileId").GetString());
        Assert.Equal(
            DumpMemberChainExpressionIdentity.AdmissionProfileId,
            root.GetProperty("admissionProfileId").GetString());

        var cases = root.GetProperty("cases").EnumerateArray().ToArray();
        Assert.True(cases.Length >= 30);
        Assert.Equal(cases.Length, cases.Select(item => item.GetProperty("id").GetString()).Distinct().Count());
        Assert.Contains(cases, item => item.GetProperty("bucket").GetString() == "ValidAdmitted");
        Assert.Contains(cases, item => item.GetProperty("bucket").GetString() == "ValidUnsupported");
        Assert.Contains(cases, item => item.GetProperty("bucket").GetString() == "Invalid");

        foreach (var item in cases)
        {
            var expression = item.GetProperty("expression").GetString()!;
            var bucket = item.GetProperty("bucket").GetString()!;
            var syntax = SyntaxFactory.ParseExpression(
                expression,
                offset: 0,
                options: ExpectedOptions,
                consumeFullText: true);
            var hasError = syntax.GetDiagnostics().Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
            Assert.Equal(bucket == "Invalid", hasError);

            var classification = DumpExpressionClassifier.Classify(
                expression,
                CreateRootBinding(),
                CreatePolicy(),
                DumpExpressionLanguageProfile.FixedDepthMemberChainV1);
            Assert.NotNull(classification.Request);
            Assert.Equal(DumpExpressionLanguageProfile.FixedDepthMemberChainV1, classification.Request.LanguageProfile);

            switch (bucket)
            {
                case "ValidAdmitted":
                    Assert.Equal(DumpExpressionClassificationStatus.Accepted, classification.Status);
                    Assert.Equal(
                        Enum.Parse<DumpExpressionKind>(item.GetProperty("expectedKind").GetString()!),
                        classification.Kind);
                    Assert.Null(classification.DiagnosticCode);
                    Assert.NotNull(classification.Request.ParsedExpression);
                    if (classification.Kind == DumpExpressionKind.FixedDepthMemberChain)
                    {
                        Assert.NotNull(classification.Request.MemberChainExpressionIdentity);
                    }

                    break;
                case "ValidUnsupported":
                    Assert.Equal(DumpExpressionClassificationStatus.Unsupported, classification.Status);
                    Assert.Equal(item.GetProperty("expectedCode").GetString(), classification.DiagnosticCode);
                    Assert.Null(classification.Request.ParsedExpression);
                    break;
                case "Invalid":
                    Assert.Equal(DumpExpressionClassificationStatus.Invalid, classification.Status);
                    Assert.Equal(item.GetProperty("expectedCode").GetString(), classification.DiagnosticCode);
                    Assert.Null(classification.Request.ParsedExpression);
                    break;
                default:
                    throw new InvalidOperationException("The corpus contains an unknown admission bucket.");
            }
        }
    }

    /// <summary>
    /// Freezes the package/profile constants, regular-source parse options, lock-file resolution, and dependency
    /// placement on the sole front-end assembly.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void CompleteExpressionProfileAndDependencyPlacementArePinned()
    {
        Assert.Equal("RoslynCSharpExpressionV1", DumpCSharpExpressionProfile.Id);
        Assert.Equal("Microsoft.CodeAnalysis.CSharp", DumpCSharpExpressionProfile.PackageId);
        Assert.Equal("5.3.0", DumpCSharpExpressionProfile.PackageVersion);
        Assert.Equal("CSharp14", DumpCSharpExpressionProfile.LanguageVersion);
        Assert.Equal(512, DumpCSharpExpressionProfile.MaximumExpressionCharacters);
        Assert.Equal(256, DumpCSharpExpressionProfile.MaximumNodesAndTokens);
        Assert.Equal(64, DumpCSharpExpressionProfile.MaximumSyntaxDepth);
        Assert.Equal(64, DumpCSharpExpressionProfile.MaximumIdentifierCharacters);
        Assert.Equal(256, DumpCSharpExpressionProfile.MaximumStringCharacters);
        Assert.Equal(DumpCSharpExpressionProfile.Id, CSharpExpressionFrontEnd.ProfileId);
        Assert.Equal(DumpCSharpExpressionProfile.PackageId, CSharpExpressionFrontEnd.PackageId);
        Assert.Equal(DumpCSharpExpressionProfile.PackageVersion, CSharpExpressionFrontEnd.PackageVersion);
        Assert.Equal(DumpCSharpExpressionProfile.LanguageVersion, CSharpExpressionFrontEnd.LanguageVersionName);
        Assert.Equal(DumpCSharpExpressionProfile.MaximumExpressionCharacters, CSharpExpressionFrontEnd.MaximumExpressionLength);
        Assert.Equal(DumpCSharpExpressionProfile.MaximumNodesAndTokens, CSharpExpressionFrontEnd.MaximumNodeTokenCount);
        Assert.Equal(DumpCSharpExpressionProfile.MaximumSyntaxDepth, CSharpExpressionFrontEnd.MaximumSyntaxDepth);
        Assert.Equal(DumpCSharpExpressionProfile.MaximumIdentifierCharacters, CSharpExpressionFrontEnd.MaximumIdentifierLength);
        Assert.Equal(DumpCSharpExpressionProfile.MaximumStringCharacters, CSharpExpressionFrontEnd.MaximumStringLiteralLength);
        Assert.Equal(LanguageVersion.CSharp14, ExpectedOptions.LanguageVersion);
        Assert.Equal(DocumentationMode.None, ExpectedOptions.DocumentationMode);
        Assert.Equal(SourceCodeKind.Regular, ExpectedOptions.Kind);
        Assert.Empty(ExpectedOptions.PreprocessorSymbolNames);
        Assert.Empty(ExpectedOptions.Features);

        using var lockFile = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(
            ResolveRepositoryRoot(),
            "src",
            "PhoenixInspect.Product.DumpQuery",
            "packages.lock.json")));
        var dependency = lockFile.RootElement
            .GetProperty("dependencies")
            .GetProperty("net10.0")
            .GetProperty(DumpCSharpExpressionProfile.PackageId);
        Assert.Equal("Direct", dependency.GetProperty("type").GetString());
        Assert.Equal(DumpCSharpExpressionProfile.PackageVersion, dependency.GetProperty("resolved").GetString());

        var queryReferences = typeof(DumpQueryEngine).Assembly.GetReferencedAssemblies().Select(item => item.Name);
        var debuggingReferences = typeof(DumpExpressionClassifier).Assembly.GetReferencedAssemblies().Select(item => item.Name);
        Assert.Contains("Microsoft.CodeAnalysis.CSharp", queryReferences);
        Assert.DoesNotContain("Microsoft.CodeAnalysis.CSharp", debuggingReferences);
    }

    /// <summary>
    /// Proves the opt-in profile admits only the declared two-member forms, freezes decoded chain semantics and raw
    /// text independently, and leaves the default W5 profile unchanged.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void OptInChainIdentityReplaysWithoutChangingTheFrozenDefaultProfile()
    {
        const string expression = "root.Failure?.Code ?? \"request-failed\"";
        var legacy = DumpExpressionClassifier.Classify(expression, CreateRootBinding(), CreatePolicy());
        var first = DumpExpressionClassifier.Classify(
            expression,
            CreateRootBinding(),
            CreatePolicy(),
            DumpExpressionLanguageProfile.FixedDepthMemberChainV1);
        var repeated = DumpExpressionClassifier.Classify(
            expression,
            CreateRootBinding(),
            CreatePolicy(),
            DumpExpressionLanguageProfile.FixedDepthMemberChainV1);
        var distinctRawText = DumpExpressionClassifier.Classify(
            "root.Failure?.Code??\"request-failed\"",
            CreateRootBinding(),
            CreatePolicy(),
            DumpExpressionLanguageProfile.FixedDepthMemberChainV1);

        Assert.Equal(DumpExpressionClassificationStatus.Unsupported, legacy.Status);
        Assert.Equal(DumpExpressionLanguageProfile.FrozenW5, legacy.Request!.LanguageProfile);
        Assert.Equal(DumpExpressionClassificationStatus.Accepted, first.Status);
        Assert.Equal(DumpExpressionKind.FixedDepthMemberChain, first.Kind);
        var identity = Assert.IsType<DumpMemberChainExpressionIdentity>(first.Request!.MemberChainExpressionIdentity);
        Assert.Equal("root", identity.RootName);
        Assert.Equal("Failure", identity.ReferenceMemberName);
        Assert.Equal("Code", identity.TerminalMemberName);
        Assert.Equal(DumpMemberChainAccessKind.Conditional, identity.AccessKind);
        Assert.Equal(DumpMemberChainFallbackKind.String, identity.FallbackKind);
        Assert.Null(identity.Int32Fallback);
        Assert.Equal("request-failed", identity.StringFallback);
        Assert.Equal(identity.CanonicalBytes.AsSpan().ToArray(), repeated.Request!.MemberChainExpressionIdentity!.CanonicalBytes.AsSpan().ToArray());
        Assert.Equal(first.Request.CanonicalBytes.AsSpan().ToArray(), repeated.Request.CanonicalBytes.AsSpan().ToArray());
        Assert.NotEqual(first.Request.Sha256, distinctRawText.Request!.Sha256);
        Assert.NotEqual(identity.Sha256, distinctRawText.Request.MemberChainExpressionIdentity!.Sha256);
    }

    /// <summary>
    /// Proves every pre-parse and post-parse structural/value bound produces one stable invalid outcome before shape
    /// recognition, while valid syntax outside the admitted shapes remains unsupported.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void FrontEndBoundsFailBeforeSubsetRecognition()
    {
        AssertInvalid(
            new string('x', DumpCSharpExpressionProfile.MaximumExpressionCharacters + 1),
            "QUERY_EXPRESSION_TOO_LONG",
            requestExpected: false);
        AssertInvalid(
            $"root.{new string('m', DumpCSharpExpressionProfile.MaximumIdentifierCharacters + 1)}.Code",
            "QUERY_IDENTIFIER_TOO_LONG");
        AssertInvalid(
            $"root.Failure?.Code ?? \"{new string('v', DumpCSharpExpressionProfile.MaximumStringCharacters + 1)}\"",
            "QUERY_STRING_LITERAL_TOO_LONG");
        AssertInvalid(
            string.Concat(Enumerable.Repeat("(", 70)) + "root" + string.Concat(Enumerable.Repeat(")", 70)),
            "QUERY_SYNTAX_DEPTH_LIMIT_EXCEEDED");
        AssertInvalid(
            string.Join('+', Enumerable.Repeat("r", 130)),
            "QUERY_SYNTAX_NODE_LIMIT_EXCEEDED");

        var validOutsideSubset = DumpExpressionClassifier.Classify(
            "root switch { null => 0, _ => 1 }",
            CreateRootBinding(),
            CreatePolicy(),
            DumpExpressionLanguageProfile.FixedDepthMemberChainV1);
        Assert.Equal(DumpExpressionClassificationStatus.Unsupported, validOutsideSubset.Status);
        Assert.Equal("QUERY_SYNTAX_UNSUPPORTED", validOutsideSubset.DiagnosticCode);
    }

    /// <summary>
    /// Proves directive-bearing or disabled-text trees do not inherit an implicit preprocessor environment and are
    /// rejected as valid-but-unsupported profile input rather than entering a shape recognizer.
    /// </summary>
    [Theory]
    [InlineData("#if true\nroot.Failure.Code\n#endif")]
    [InlineData("#if false\nignored tokens\n#else\nroot.Failure.Code\n#endif")]
    [Trait("Category", "Fast")]
    public void DirectiveAndDisabledTextTriviaAreUnsupported(string expression)
    {
        var classification = DumpExpressionClassifier.Classify(
            expression,
            CreateRootBinding(),
            CreatePolicy(),
            DumpExpressionLanguageProfile.FixedDepthMemberChainV1);
        Assert.Equal(DumpExpressionClassificationStatus.Unsupported, classification.Status);
        Assert.Equal("QUERY_CSHARP_DIRECTIVE_UNSUPPORTED", classification.DiagnosticCode);
    }

    /// <summary>
    /// Guards every public front-end and product signature against leaking compiler nodes, tokens, diagnostics,
    /// symbols, or parse-option objects beyond the query assembly.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void PublicProductSurfaceContainsOnlyProjectOwnedDescriptors()
    {
        var assemblies = new[] { typeof(DumpQueryEngine).Assembly, typeof(DumpExpressionClassifier).Assembly };
        foreach (var assembly in assemblies)
        {
            foreach (var type in assembly.GetExportedTypes())
            {
                Assert.False(IsCompilerType(type));
                foreach (var method in type.GetMethods())
                {
                    Assert.False(IsCompilerType(method.ReturnType));
                    Assert.All(method.GetParameters(), parameter => Assert.False(IsCompilerType(parameter.ParameterType)));
                }

                foreach (var property in type.GetProperties())
                {
                    Assert.False(IsCompilerType(property.PropertyType));
                }
            }
        }
    }

    /// <summary>
    /// Proves production has one complete-expression parse site, no retained handwritten reader/literal decoder, no
    /// W5 diagnostic-dependent raw-string fallback, and no parse call in the descriptor-consuming evaluator.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void ProductionSourceHasOneParserAndPreparationConsumesItsDescriptor()
    {
        var sourceRoot = Path.Combine(ResolveRepositoryRoot(), "src");
        var sources = Directory.EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .ToArray();
        Assert.Equal(
            1,
            sources.Sum(path => CountOccurrences(File.ReadAllText(path), "SyntaxFactory.ParseExpression(")));

        var queryParser = File.ReadAllText(Path.Combine(
            sourceRoot,
            "PhoenixInspect.Product.DumpQuery",
            "DumpQueryParser.cs"));
        Assert.DoesNotContain("ref struct Reader", queryParser, StringComparison.Ordinal);
        Assert.DoesNotContain("TryReadLiteral", queryParser, StringComparison.Ordinal);
        Assert.DoesNotContain("TryReadStringLiteral", queryParser, StringComparison.Ordinal);

        var requestSource = File.ReadAllText(Path.Combine(
            sourceRoot,
            "PhoenixInspect.Product.DumpDebugging",
            "DumpExpressionRequest.cs"));
        Assert.DoesNotContain("expectedMethodExpression", requestSource, StringComparison.Ordinal);

        var evaluatorSource = File.ReadAllText(Path.Combine(
            sourceRoot,
            "PhoenixInspect.Product.DumpDebugging",
            "DumpExpressionEvaluator.cs"));
        Assert.Contains("PrepareParsed(", evaluatorSource, StringComparison.Ordinal);
        Assert.DoesNotContain("DumpQueryEngine.Prepare(session, expression", evaluatorSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ParseExpression", evaluatorSource, StringComparison.Ordinal);
    }

    private static void AssertInvalid(string expression, string code, bool requestExpected = true)
    {
        var classification = DumpExpressionClassifier.Classify(
            expression,
            CreateRootBinding(),
            CreatePolicy(),
            DumpExpressionLanguageProfile.FixedDepthMemberChainV1);
        Assert.Equal(DumpExpressionClassificationStatus.Invalid, classification.Status);
        Assert.Equal(code, classification.DiagnosticCode);
        Assert.Equal(requestExpected, classification.Request is not null);
    }

    private static bool IsCompilerType(Type type)
    {
        if (type.IsByRef || type.IsPointer || type.IsArray)
        {
            return IsCompilerType(type.GetElementType()!);
        }

        if (type.IsGenericType && type.GetGenericArguments().Any(IsCompilerType))
        {
            return true;
        }

        return type.Namespace?.StartsWith("Microsoft.CodeAnalysis", StringComparison.Ordinal) == true;
    }

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        var offset = 0;
        while ((offset = text.IndexOf(value, offset, StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += value.Length;
        }

        return count;
    }

    private static DumpExpressionPolicy CreatePolicy() =>
        DumpExpressionPolicy.Create(
            DumpMethodEvaluationMode.Interpreted,
            instructionLimit: 100,
            logicalDepthLimit: 2,
            traversalLimit: 10);

    private static DumpQueryRootBinding CreateRootBinding()
    {
        var snapshot = new ClrmdSnapshotIdentity(new string('a', 64));
        var moduleIdentity = new ClrmdRuntimeModuleIdentity(
            snapshot,
            AppDomainAddress: 0x2000,
            ModuleAddress: 0x3000,
            ImageBase: 0x4000,
            ImageSize: 0x5000);
        var module = new ClrmdModuleInfo(
            moduleIdentity,
            "PhoenixInspect.TestTarget.dll",
            targetPathHint: null,
            appDomainId: 1,
            metadataAddress: 0x6000,
            metadataLength: 0x7000,
            layout: "SyntheticMapped");
        const ulong objectAddress = 0x1000;
        var rootSlotRead = MemoryReadResult.Create(
            snapshot.MemorySourceId,
            address: 0x8000,
            requestedLength: sizeof(ulong),
            BitConverter.GetBytes(objectAddress));
        var methodTableRead = MemoryReadResult.Create(
            snapshot.MemorySourceId,
            objectAddress,
            requestedLength: sizeof(ulong),
            BitConverter.GetBytes(0x9000UL));
        var root = new ClrmdHeapObjectInfo(
            snapshot,
            objectAddress,
            "DumpProbe",
            typeMetadataToken: 0x02000002,
            methodTable: 0x9000,
            rootAddress: 0x8000,
            rootKind: "Strong",
            module,
            ImmutableArray.Create(rootSlotRead, methodTableRead));
        return DumpQueryRootBinding.FromExactObject(
            "root",
            root,
            ImmutableArray.Create(
                new EvaluationDeterministicBound("root-selection.maximum-handles", 100_000),
                new EvaluationDeterministicBound("root-selection.maximum-matches", 2)));
    }

    private static string ResolveCorpusPath() => Path.Combine(
        ResolveRepositoryRoot(),
        "tests",
        "corpus",
        "w6-csharp-expression-front-end-v1.json");

    private static string ResolveRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PhoenixInspect.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ??
            throw new InvalidOperationException("Could not locate the repository root from the test output directory.");
    }
}
