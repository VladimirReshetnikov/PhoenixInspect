using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using PhoenixInspect.Inspection;
using PhoenixInspect.Product.DumpQuery;
using Xunit;

namespace PhoenixInspect.IntegrationTests;

/// <summary>
/// Proves the immediate window's <c>#r</c> directive: a referenced assembly's constants, enums, and type names
/// resolve in constant evaluation with no snapshot loaded, both in the global scope and through an extern alias.
/// </summary>
/// <remarks>
/// The reference assembly is compiled in-test with the pinned compiler so its declared shapes are known exactly;
/// no delta byte or metadata is hand-authored.
/// </remarks>
public sealed class ReferenceDirectiveTests
{
    private const string ReferenceSource = """
        namespace Contoso.Widgets;

        public static class Limits
        {
            public const int MaxWidgets = 4096;
            public const string Banner = "contoso-widgets";
        }

        public enum WidgetState
        {
            Idle = 0,
            Running = 7,
            Faulted = 13,
        }
        """;

    /// <summary>A global-scope reference contributes its const, enum, typeof, and nameof to no-snapshot evaluation.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Global_reference_contributes_constants_enums_and_type_names()
    {
        var path = CompileReference("Contoso.Widgets");
        try
        {
            var reference = ConstantReferenceAssembly.TryLoad(path, alias: null, out var loadError);
            Assert.Null(loadError);
            Assert.NotNull(reference);
            var references = ImmutableArray.Create(reference!);

            // Without the reference, the qualified name and enum are beyond the evidence-free subset.
            Assert.Equal(
                EvaluationSeverity.Stopped,
                ExpressionEvaluationService.EvaluateWithoutSnapshot(
                    "Contoso.Widgets.Limits.MaxWidgets").Severity);

            // With it, the const folds and participates in arithmetic, the string const folds, the enum member
            // resolves, and nameof/typeof name the referenced type.
            var constant = ExpressionEvaluationService.EvaluateWithoutSnapshot(
                "Contoso.Widgets.Limits.MaxWidgets / 2",
                references);
            Assert.Equal(EvaluationSeverity.Exact, constant.Severity);
            Assert.Equal("2048", constant.Value);

            var banner = ExpressionEvaluationService.EvaluateWithoutSnapshot(
                "Contoso.Widgets.Limits.Banner",
                references);
            Assert.Equal(EvaluationSeverity.Exact, banner.Severity);
            Assert.Contains("contoso-widgets", banner.Value, StringComparison.Ordinal);

            var enumMember = ExpressionEvaluationService.EvaluateWithoutSnapshot(
                "(int)Contoso.Widgets.WidgetState.Faulted",
                references);
            Assert.Equal(EvaluationSeverity.Exact, enumMember.Severity);
            Assert.Equal("13", enumMember.Value);

            var named = ExpressionEvaluationService.EvaluateWithoutSnapshot(
                "nameof(Contoso.Widgets.WidgetState)",
                references);
            Assert.Equal(EvaluationSeverity.Exact, named.Severity);
            Assert.Contains("WidgetState", named.Value, StringComparison.Ordinal);

            var typed = ExpressionEvaluationService.EvaluateWithoutSnapshot(
                "typeof(Contoso.Widgets.WidgetState).Name",
                references);
            Assert.Equal(EvaluationSeverity.Exact, typed.Severity);
            Assert.Contains("WidgetState", typed.Value, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>An aliased reference is reachable only through its alias qualifier.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Aliased_reference_resolves_only_through_its_alias()
    {
        var path = CompileReference("Contoso.Widgets");
        try
        {
            var aliased = ConstantReferenceAssembly.TryLoad(path, alias: "widgets", out var loadError);
            Assert.Null(loadError);
            var references = ImmutableArray.Create(aliased!);

            // The unqualified name does not reach an aliased reference.
            Assert.Equal(
                EvaluationSeverity.Stopped,
                ExpressionEvaluationService.EvaluateWithoutSnapshot(
                    "Contoso.Widgets.Limits.MaxWidgets",
                    references).Severity);

            // The alias-qualified name does.
            var aliasedConst = ExpressionEvaluationService.EvaluateWithoutSnapshot(
                "widgets::Contoso.Widgets.Limits.MaxWidgets",
                references);
            Assert.Equal(EvaluationSeverity.Exact, aliasedConst.Severity);
            Assert.Equal("4096", aliasedConst.Value);

            var aliasedEnum = ExpressionEvaluationService.EvaluateWithoutSnapshot(
                "(int)widgets::Contoso.Widgets.WidgetState.Running",
                references);
            Assert.Equal(EvaluationSeverity.Exact, aliasedEnum.Severity);
            Assert.Equal("7", aliasedEnum.Value);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>The directive parser applies path and alias forms and rejects malformed input.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Directive_parser_applies_path_and_alias_forms()
    {
        var path = CompileReference("Contoso.Widgets");
        try
        {
            Assert.True(ReferenceDirective.IsDirective("#r \"a.dll\""));
            Assert.False(ReferenceDirective.IsDirective("1 + 1"));

            ImmutableArray<ConstantReferenceAssembly> set = [];
            Assert.True(ReferenceDirective.TryApply(
                $"#r \"{path}\"",
                set,
                out set,
                out var pathMessage));
            Assert.Contains("Referenced", pathMessage, StringComparison.Ordinal);
            Assert.Null(Assert.Single(set).Alias);

            Assert.True(ReferenceDirective.TryApply(
                $"#r \"{path}\" as widgets",
                set,
                out set,
                out var aliasMessage));
            Assert.Contains("as widgets", aliasMessage, StringComparison.Ordinal);
            Assert.Equal("widgets", set[^1].Alias);

            Assert.False(ReferenceDirective.TryApply(
                "#r \"C:\\does\\not\\exist.dll\"",
                set,
                out var unchanged,
                out var missingMessage));
            Assert.Equal(set.Length, unchanged.Length);
            Assert.Contains("does not exist", missingMessage, StringComparison.Ordinal);

            Assert.False(ReferenceDirective.TryApply(
                "#r bare",
                set,
                out _,
                out var quotedMessage));
            Assert.Contains("quoted string", quotedMessage, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string CompileReference(string assemblyName)
    {
        var compilation = CSharpCompilation.Create(
            assemblyName,
            [CSharpSyntaxTree.ParseText(ReferenceSource)],
            TrustedPlatformReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, deterministic: true));
        var path = Path.Combine(Path.GetTempPath(), $"{assemblyName}-{Guid.NewGuid():N}.dll");
        var emitted = compilation.Emit(path);
        Assert.True(emitted.Success, string.Join("; ", emitted.Diagnostics));
        return path;
    }

    private static ImmutableArray<MetadataReference> TrustedPlatformReferences()
    {
        var trusted = (string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!;
        var references = ImmutableArray.CreateBuilder<MetadataReference>();
        foreach (var candidate in trusted.Split(Path.PathSeparator))
        {
            var fileName = Path.GetFileName(candidate);
            if (string.Equals(fileName, "System.Private.CoreLib.dll", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(fileName, "System.Runtime.dll", StringComparison.OrdinalIgnoreCase))
            {
                references.Add(MetadataReference.CreateFromFile(candidate));
            }
        }

        return references.ToImmutable();
    }
}
