using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using Interpreter.Product.DumpQuery;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Interpreter.IntegrationTests;

/// <summary>Exercises non-conflated draft compiler-name facts with a broad synthetic metadata-name matrix.</summary>
public sealed class W8CompilerNameMappingContractTests
{
    /// <summary>
    /// Proves strict CLS spelling, Roslyn suffix removal, C# addressability, and evaluator admission remain independent
    /// across valid, unusual, and malformed synthetic metadata names.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Complete_projection_matrix_keeps_four_dispositions_independent()
    {
        var cases = new[]
        {
            Case("canonical-one", "G`1", 1, true, "G", true, true, false, "G", true),
            Case("plain-generic", "G", 1, false, "G", false, true, false, "G", true),
            Case("suffix-mismatch", "G`2", 1, false, "G`2", false, false, false, null, true),
            Case("zero-suffix", "G`0", 1, false, "G`0", false, false, false, null, true),
            Case("leading-zero", "G`01", 1, false, "G`01", false, false, false, null, true),
            Case("empty-prefix", "`1", 1, false, "`1", false, false, false, null, true),
            Case("non-ascii-digit", "G`١", 1, false, "G`١", false, false, false, null, true),
            Case("plus-sign", "G`+1", 1, false, "G`+1", false, false, false, null, true),
            Case("minus-sign", "G`-1", 1, false, "G`-1", false, false, false, null, true),
            Case("leading-space", "G` 1", 1, false, "G` 1", false, false, false, null, true),
            Case("trailing-space", "G`1 ", 1, false, "G`1 ", false, false, false, null, true),
            Case("trailing-backtick", "G`", 1, false, "G`", false, false, false, null, true),
            Case("five-digit-maximum", "G`32767", 32_767, true, "G", true, true, false, "G", false),
            Case("roslyn-overflow", "G`32768", 32_768, false, "G`32768", false, false, false, null, false),
            Case("six-digits", "G`000001", 1, false, "G`000001", false, false, false, null, true),
            Case("mixed-suffix", "G`1x", 1, false, "G`1x", false, false, false, null, true),
            Case("earlier-backtick", "G`1`2", 2, false, "G`1", true, false, false, null, true),
            Case("plain-nongeneric", "Plain", 0, true, "Plain", false, true, false, "Plain", true),
            Case("empty-name", "", 0, true, "", false, false, false, null, true),
            Case("unexpected-nongeneric-suffix", "Odd`1", 0, false, "Odd`1", false, false, false, null, true),
            Case("reserved-keyword", "class", 0, true, "class", false, true, true, "@class", true),
            Case("contextual-record", "record", 0, true, "record", false, true, false, "record", true),
            Case("contextual-await", "await", 0, true, "await", false, true, false, "await", true),
            Case("greek-letter", "Δ", 0, true, "Δ", false, true, false, "Δ", true),
            Case("combining-mark", "A\u0301", 0, true, "A\u0301", false, true, false, "A\u0301", true),
            Case("dropped-zero-width-non-joiner", "A\u200C", 0, true, "A\u200C", false, false, false, null, true),
            Case("dropped-zero-width-joiner", "A\u200D", 0, true, "A\u200D", false, false, false, null, true),
            Case("underscore", "_", 0, true, "_", false, true, false, "_", true),
            Case("generated-angle-name", "<Generated>", 0, true, "<Generated>", false, false, false, null, true),
            Case("supplementary-symbol", "😀", 0, true, "😀", false, false, false, null, true),
            Case("qualified-text", "A.B", 0, true, "A.B", false, false, false, null, true),
            Case("embedded-space", "A B", 0, true, "A B", false, false, false, null, true),
        };

        foreach (var item in cases)
        {
            var mapping = MetadataCompilerNameMappingIdentity.Derive(
                item.RawMetadataName,
                item.TotalGenericArity);

            Assert.Equal(MetadataCompilerNameMappingResultKind.Exact, mapping.ResultKind);
            Assert.Equal(MetadataCompilerNameMappingIssue.None, mapping.Issue);
            Assert.Equal(item.TotalGenericArity, mapping.IntroducedGenericArity);
            Assert.Equal(item.RawMetadataName, mapping.Input.RawMetadataName);
            Assert.Equal(item.TotalGenericArity, mapping.Input.TotalGenericArity);
            Assert.Null(mapping.Input.EnclosingTotalGenericArity);
            Assert.False(mapping.Input.IsNested);

            var cls = Assert.IsType<MetadataClsAritySpellingIdentity>(mapping.ClsAritySpelling);
            Assert.Equal(
                item.ClsCanonical
                    ? MetadataClsAritySpellingStatus.Canonical
                    : MetadataClsAritySpellingStatus.NonCanonical,
                cls.Status);
            Assert.Equal(item.ClsCanonical, cls.IsCanonical);
            Assert.Equal(item.TotalGenericArity, cls.IntroducedGenericArity);

            var projection = Assert.IsType<MetadataRoslynNameProjectionIdentity>(mapping.RoslynProjection);
            Assert.Equal(item.ProjectedSimpleName, projection.ProjectedSimpleName);
            Assert.Equal(item.RemovedSuffix, projection.WasTerminalAritySuffixRemoved);
            Assert.Equal(
                item.RemovedSuffix
                    ? MetadataRoslynNameProjectionStatus.TerminalAritySuffixRemoved
                    : MetadataRoslynNameProjectionStatus.RawMetadataNameRetained,
                projection.Status);

            var addressability = Assert.IsType<MetadataCSharpSimpleNameAddressabilityIdentity>(
                mapping.CSharpAddressability);
            Assert.Equal(item.Addressable, addressability.IsAddressable);
            Assert.Equal(
                item.Addressable
                    ? MetadataCSharpSimpleNameAddressabilityStatus.Addressable
                    : MetadataCSharpSimpleNameAddressabilityStatus.NotAddressable,
                addressability.Status);
            Assert.True(
                item.RequiresVerbatimEscape == addressability.RequiresVerbatimEscape,
                $"Case '{item.Label}' expected RequiresVerbatimEscape={item.RequiresVerbatimEscape} " +
                $"but observed {addressability.RequiresVerbatimEscape}.");
            Assert.Equal(item.SourceSpelling, addressability.SourceSpelling);

            var admission = Assert.IsType<MetadataEvaluatorGenericArityAdmissionIdentity>(
                mapping.EvaluatorAdmission);
            Assert.Equal(item.Admitted, admission.IsAdmitted);
            Assert.Equal(item.TotalGenericArity, admission.ObservedTotalGenericArity);
            Assert.Equal(64, admission.MaximumAdmittedTotalGenericArity);
        }

        Assert.True(SyntaxFacts.IsValidIdentifier("class"));
        Assert.NotEqual(SyntaxKind.None, SyntaxFacts.GetKeywordKind("class"));
        Assert.True(SyntaxFacts.IsValidIdentifier("record"));
        Assert.NotEqual(SyntaxKind.None, SyntaxFacts.GetContextualKeywordKind("record"));
        Assert.True(SyntaxFacts.IsValidIdentifier("await"));
        Assert.NotEqual(SyntaxKind.None, SyntaxFacts.GetContextualKeywordKind("await"));
        Assert.True(SyntaxFacts.IsValidIdentifier("A\u200C"));
        Assert.True(SyntaxFacts.IsValidIdentifier("A\u200D"));
    }

    /// <summary>
    /// Proves top-level, equal-arity nested, introducing nested, and underflowing nested segments preserve physical
    /// input while distinguishing exact projections from a non-exact mapping stop.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Nested_arity_chain_derives_delta_and_erases_no_physical_input_on_underflow()
    {
        var outer = MetadataCompilerNameMappingIdentity.Derive("Outer`2", 2);
        var inner = MetadataCompilerNameMappingIdentity.Derive("Inner", 2, enclosingTotalGenericArity: 2);
        var leaf = MetadataCompilerNameMappingIdentity.Derive("Leaf`1", 3, enclosingTotalGenericArity: 2);
        var zeroWithSuffix = MetadataCompilerNameMappingIdentity.Derive(
            "Unexpected`1",
            3,
            enclosingTotalGenericArity: 3);
        var underflow = MetadataCompilerNameMappingIdentity.Derive(
            "Broken`1",
            1,
            enclosingTotalGenericArity: 2);

        Assert.Equal(2, outer.IntroducedGenericArity);
        Assert.Equal("Outer", outer.RoslynProjection!.ProjectedSimpleName);
        Assert.True(outer.ClsAritySpelling!.IsCanonical);

        Assert.Equal(0, inner.IntroducedGenericArity);
        Assert.True(inner.Input.IsNested);
        Assert.Equal(2, inner.Input.EnclosingTotalGenericArity);
        Assert.Equal("Inner", inner.RoslynProjection!.ProjectedSimpleName);
        Assert.False(inner.RoslynProjection.WasTerminalAritySuffixRemoved);
        Assert.True(inner.ClsAritySpelling!.IsCanonical);

        Assert.Equal(1, leaf.IntroducedGenericArity);
        Assert.Equal(3, leaf.Input.TotalGenericArity);
        Assert.Equal("Leaf", leaf.RoslynProjection!.ProjectedSimpleName);
        Assert.True(leaf.RoslynProjection.WasTerminalAritySuffixRemoved);
        Assert.True(leaf.ClsAritySpelling!.IsCanonical);

        Assert.Equal(0, zeroWithSuffix.IntroducedGenericArity);
        Assert.False(zeroWithSuffix.ClsAritySpelling!.IsCanonical);
        Assert.Equal("Unexpected`1", zeroWithSuffix.RoslynProjection!.ProjectedSimpleName);
        Assert.False(zeroWithSuffix.RoslynProjection.WasTerminalAritySuffixRemoved);
        Assert.False(zeroWithSuffix.CSharpAddressability!.IsAddressable);

        Assert.Equal(MetadataCompilerNameMappingResultKind.NonExact, underflow.ResultKind);
        Assert.Equal(MetadataCompilerNameMappingIssue.NestedTotalArityUnderflow, underflow.Issue);
        Assert.Equal("Broken`1", underflow.Input.RawMetadataName);
        Assert.Equal(1, underflow.Input.TotalGenericArity);
        Assert.Equal(2, underflow.Input.EnclosingTotalGenericArity);
        Assert.Null(underflow.IntroducedGenericArity);
        Assert.Null(underflow.ClsAritySpelling);
        Assert.Null(underflow.RoslynProjection);
        Assert.Null(underflow.CSharpAddressability);
        Assert.Null(underflow.EvaluatorAdmission);
    }

    /// <summary>
    /// Proves the evaluator's arity ceiling is applied after complete physical, CLS, Roslyn, and source-name facts.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Arity_sixty_five_remains_an_exact_mapping_but_is_not_evaluator_admitted()
    {
        var atLimit = MetadataCompilerNameMappingIdentity.Derive("Wide`64", 64);
        var overLimit = MetadataCompilerNameMappingIdentity.Derive("Wider`65", 65);

        Assert.Equal(MetadataCompilerNameMappingResultKind.Exact, atLimit.ResultKind);
        Assert.True(atLimit.ClsAritySpelling!.IsCanonical);
        Assert.Equal("Wide", atLimit.RoslynProjection!.ProjectedSimpleName);
        Assert.True(atLimit.CSharpAddressability!.IsAddressable);
        Assert.True(atLimit.EvaluatorAdmission!.IsAdmitted);

        Assert.Equal(MetadataCompilerNameMappingResultKind.Exact, overLimit.ResultKind);
        Assert.Equal(MetadataCompilerNameMappingIssue.None, overLimit.Issue);
        Assert.Equal(65, overLimit.IntroducedGenericArity);
        Assert.True(overLimit.ClsAritySpelling!.IsCanonical);
        Assert.Equal(65, overLimit.ClsAritySpelling.ParsedTerminalArity);
        Assert.Equal("Wider", overLimit.RoslynProjection!.ProjectedSimpleName);
        Assert.True(overLimit.RoslynProjection.WasTerminalAritySuffixRemoved);
        Assert.True(overLimit.CSharpAddressability!.IsAddressable);
        Assert.Equal(
            MetadataEvaluatorGenericArityAdmissionStatus.TotalArityLimitExceeded,
            overLimit.EvaluatorAdmission!.Status);
        Assert.False(overLimit.EvaluatorAdmission.IsAdmitted);
        Assert.Equal(64, overLimit.EvaluatorAdmission.MaximumAdmittedTotalGenericArity);
    }

    /// <summary>
    /// Proves canonical replay, defensive byte access, equality, and private mint capabilities for every draft fact.
    /// </summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Mapping_replays_canonically_and_rejects_direct_fact_minting()
    {
        var first = MetadataCompilerNameMappingIdentity.Derive(
            "Nested`2",
            5,
            enclosingTotalGenericArity: 3);
        var replay = MetadataCompilerNameMappingIdentity.Derive(
            "Nested`2",
            5,
            enclosingTotalGenericArity: 3);
        var changed = MetadataCompilerNameMappingIdentity.Derive(
            "Nested`2",
            6,
            enclosingTotalGenericArity: 3);

        Assert.Equal(first, replay);
        Assert.Equal(first.GetHashCode(), replay.GetHashCode());
        Assert.Equal(first.Sha256, replay.Sha256);
        Assert.True(first.CanonicalBytes.AsSpan().SequenceEqual(replay.CanonicalBytes.AsSpan()));
        Assert.NotEqual(first, changed);
        Assert.NotEqual(first.Sha256, changed.Sha256);
        Assert.Equal(first.Input, replay.Input);
        Assert.Equal(first.ClsAritySpelling, replay.ClsAritySpelling);
        Assert.Equal(first.RoslynProjection, replay.RoslynProjection);
        Assert.Equal(first.CSharpAddressability, replay.CSharpAddressability);
        Assert.Equal(first.EvaluatorAdmission, replay.EvaluatorAdmission);

        var beforeMutation = first.CanonicalBytes;
        var exposed = first.CanonicalBytes;
        var mutableArray = ImmutableCollectionsMarshal.AsArray(exposed)!;
        mutableArray[0] ^= 0x5A;
        Assert.True(beforeMutation.AsSpan().SequenceEqual(first.CanonicalBytes.AsSpan()));

        Assert.Throws<ArgumentException>(() => MetadataCompilerNameMappingInputIdentity.Create(
            new object(),
            "G`1",
            1,
            null));
        Assert.Throws<ArgumentException>(() => MetadataClsAritySpellingIdentity.Create(
            new object(),
            first.Input,
            2));
        Assert.Throws<ArgumentException>(() => MetadataRoslynNameProjectionIdentity.Create(
            new object(),
            first.Input,
            2));
        Assert.Throws<ArgumentException>(() => MetadataCSharpSimpleNameAddressabilityIdentity.Create(
            new object(),
            first.RoslynProjection!));
        Assert.Throws<ArgumentException>(() => MetadataEvaluatorGenericArityAdmissionIdentity.Create(
            new object(),
            first.Input));

        Assert.Throws<ArgumentException>(() => MetadataCompilerNameMappingIdentity.Derive("Nul\0Name", 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => MetadataCompilerNameMappingIdentity.Derive("G", -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => MetadataCompilerNameMappingIdentity.Derive("G", 0, -1));
    }

    /// <summary>Proves every public draft type and method has emitted XML and no public issuer is exposed.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Compiler_name_mapping_public_surface_has_draft_XML_and_no_public_issuer()
    {
        var assembly = typeof(MetadataCompilerNameMappingIdentity).Assembly;
        var documentation = XDocument.Load(Path.ChangeExtension(assembly.Location, ".xml"));
        var members = documentation.Descendants("member").ToArray();
        var publicTypes = new[]
        {
            typeof(MetadataCompilerNameMappingResultKind),
            typeof(MetadataCompilerNameMappingIssue),
            typeof(MetadataClsAritySpellingStatus),
            typeof(MetadataRoslynNameProjectionStatus),
            typeof(MetadataCSharpSimpleNameAddressabilityStatus),
            typeof(MetadataEvaluatorGenericArityAdmissionStatus),
            typeof(MetadataCompilerNameMappingInputIdentity),
            typeof(MetadataClsAritySpellingIdentity),
            typeof(MetadataRoslynNameProjectionIdentity),
            typeof(MetadataCSharpSimpleNameAddressabilityIdentity),
            typeof(MetadataEvaluatorGenericArityAdmissionIdentity),
            typeof(MetadataCompilerNameMappingIdentity),
        };

        foreach (var type in publicTypes)
        {
            var typeDocumentation = Assert.Single(members, member =>
                string.Equals((string?)member.Attribute("name"), $"T:{type.FullName}", StringComparison.Ordinal));
            Assert.Contains("draft", typeDocumentation.Value, StringComparison.OrdinalIgnoreCase);

            foreach (var method in type.GetMethods(
                         BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                     .Where(static method => !method.IsSpecialName))
            {
                var prefix = $"M:{type.FullName}.{method.Name}";
                var methodDocumentation = members.Where(member =>
                    ((string?)member.Attribute("name")) is { } name &&
                    (string.Equals(name, prefix, StringComparison.Ordinal) ||
                     name.StartsWith($"{prefix}(", StringComparison.Ordinal))).ToArray();
                Assert.NotEmpty(methodDocumentation);
                Assert.All(methodDocumentation, static member =>
                    Assert.Contains("draft", member.Value, StringComparison.OrdinalIgnoreCase));
            }

            Assert.Empty(type.GetConstructors(BindingFlags.Public | BindingFlags.Instance));
            Assert.Empty(type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly));
        }
    }

    private static ProjectionCase Case(
        string label,
        string rawMetadataName,
        int totalGenericArity,
        bool clsCanonical,
        string projectedSimpleName,
        bool removedSuffix,
        bool addressable,
        bool requiresVerbatimEscape,
        string? sourceSpelling,
        bool admitted) =>
        new(
            label,
            rawMetadataName,
            totalGenericArity,
            clsCanonical,
            projectedSimpleName,
            removedSuffix,
            addressable,
            requiresVerbatimEscape,
            sourceSpelling,
            admitted);

    private sealed record ProjectionCase(
        string Label,
        string RawMetadataName,
        int TotalGenericArity,
        bool ClsCanonical,
        string ProjectedSimpleName,
        bool RemovedSuffix,
        bool Addressable,
        bool RequiresVerbatimEscape,
        string? SourceSpelling,
        bool Admitted);
}
