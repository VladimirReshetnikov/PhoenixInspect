using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using PhoenixInspect.Inspection;
using PhoenixInspect.Product.DumpQuery;
using Xunit;

namespace PhoenixInspect.IntegrationTests;

/// <summary>
/// Proves the immediate window's <c>using</c> directives: an imported namespace, a statically imported type, and
/// an alias each let a referenced assembly's constants and enums bind without their fully qualified prefixes, with
/// no snapshot loaded.
/// </summary>
/// <remarks>
/// The reference assembly is compiled in-test with the pinned compiler so its declared const and enum shapes are
/// known exactly, and every arm evaluates through the sessionless entry so the directives are the only thing under
/// test.
/// </remarks>
public sealed class UsingDirectiveTests
{
    private const string ReferenceSource = """
        namespace Contoso.Widgets;

        public static class Limits
        {
            public const int MaxWidgets = 4096;
        }

        public enum WidgetState
        {
            Idle = 0,
            Running = 7,
        }
        """;

    /// <summary>An imported namespace lets a const and an enum bind without the namespace prefix.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Imported_namespace_binds_prefixless_constants_and_enums()
    {
        var references = LoadReference(alias: null, out var path);
        try
        {
            var usings = ConstantUsingDirectiveSet.Empty.WithImportedNamespace("Contoso.Widgets");

            // Without the import, the prefix-less name is beyond the subset; with it, both bind.
            Assert.Equal(
                EvaluationSeverity.Stopped,
                ExpressionEvaluationService.EvaluateWithoutSnapshot(
                    "Limits.MaxWidgets", references).Severity);

            var constant = ExpressionEvaluationService.EvaluateWithoutSnapshot(
                "Limits.MaxWidgets + 4", references, usings);
            Assert.Equal(EvaluationSeverity.Exact, constant.Severity);
            Assert.Equal("4100", constant.Value);

            var enumMember = ExpressionEvaluationService.EvaluateWithoutSnapshot(
                "(int)WidgetState.Running", references, usings);
            Assert.Equal(EvaluationSeverity.Exact, enumMember.Severity);
            Assert.Equal("7", enumMember.Value);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>A static import lets a bare member name bind.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Static_import_binds_a_bare_member_name()
    {
        var references = LoadReference(alias: null, out var path);
        try
        {
            var usings = ConstantUsingDirectiveSet.Empty.WithStaticImport("Contoso.Widgets.Limits");
            var constant = ExpressionEvaluationService.EvaluateWithoutSnapshot(
                "MaxWidgets", references, usings);
            Assert.Equal(EvaluationSeverity.Exact, constant.Severity);
            Assert.Equal("4096", constant.Value);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>A namespace alias and a type alias each bind their target.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Aliases_bind_namespace_and_type_targets()
    {
        var references = LoadReference(alias: null, out var path);
        try
        {
            var namespaceAlias = ConstantUsingDirectiveSet.Empty.WithAlias("W", "Contoso.Widgets");
            var viaNamespaceAlias = ExpressionEvaluationService.EvaluateWithoutSnapshot(
                "W.Limits.MaxWidgets", references, namespaceAlias);
            Assert.Equal(EvaluationSeverity.Exact, viaNamespaceAlias.Severity);
            Assert.Equal("4096", viaNamespaceAlias.Value);

            var typeAlias = ConstantUsingDirectiveSet.Empty.WithAlias("WS", "Contoso.Widgets.WidgetState");
            var viaTypeAlias = ExpressionEvaluationService.EvaluateWithoutSnapshot(
                "(int)WS.Running", references, typeAlias);
            Assert.Equal(EvaluationSeverity.Exact, viaTypeAlias.Severity);
            Assert.Equal("7", viaTypeAlias.Value);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>The directive parser applies each form and rejects malformed input.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Directive_parser_applies_each_form()
    {
        Assert.True(UsingDirective.IsDirective("using System;"));
        Assert.False(UsingDirective.IsDirective("1 + 1"));

        var set = ConstantUsingDirectiveSet.Empty;
        Assert.True(UsingDirective.TryApply("using Contoso.Widgets;", set, out set, out var nsMessage));
        Assert.Equal("using Contoso.Widgets", nsMessage);

        Assert.True(UsingDirective.TryApply("using static Contoso.Widgets.Limits", set, out set, out var staticMessage));
        Assert.Equal("using static Contoso.Widgets.Limits", staticMessage);

        Assert.True(UsingDirective.TryApply("using W = Contoso.Widgets;", set, out set, out var aliasMessage));
        Assert.Equal("using W = Contoso.Widgets", aliasMessage);
        Assert.False(set.IsEmpty);

        Assert.False(UsingDirective.TryApply("using 9bad;", set, out _, out var invalidMessage));
        Assert.Contains("not a valid", invalidMessage, StringComparison.Ordinal);
    }

    private static ImmutableArray<ConstantReferenceAssembly> LoadReference(string? alias, out string path)
    {
        path = CompileReference();
        var reference = ConstantReferenceAssembly.TryLoad(path, alias, out var loadError);
        Assert.Null(loadError);
        return ImmutableArray.Create(reference!);
    }

    private static string CompileReference()
    {
        var compilation = CSharpCompilation.Create(
            "Contoso.Widgets",
            [CSharpSyntaxTree.ParseText(ReferenceSource)],
            TrustedPlatformReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, deterministic: true));
        var path = Path.Combine(Path.GetTempPath(), $"Contoso.Widgets-{Guid.NewGuid():N}.dll");
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
