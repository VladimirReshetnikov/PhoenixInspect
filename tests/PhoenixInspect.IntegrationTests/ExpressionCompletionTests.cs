using System.Collections.Immutable;
using PhoenixInspect.Host.Dump.ClrMD;
using PhoenixInspect.Inspection;
using Xunit;

namespace PhoenixInspect.IntegrationTests;

/// <summary>
/// Freezes the watch completion surface: the lexical token and receiver-chain reading, the keyword and modeled
/// identifier universe, the per-receiver member tables, prefix filtering with replacement spans, and the
/// evidence-derived catalog — root fields from the runtime type catalog and namespaces, types, and static fields
/// from dump-module metadata.
/// </summary>
public sealed class ExpressionCompletionTests
{
    private static readonly CompletionCatalog SampleCatalog = new()
    {
        HasRoot = true,
        RootIdentifier = "root",
        RootMembers =
        [
            new CompletionItem("CurrentBatch", CompletionItemKind.Field, "Contoso.OrderService.Batch"),
            new CompletionItem("RecentDispatchDurationsMs", CompletionItemKind.Field, "System.Int32[]"),
        ],
        TypeFullNames =
        [
            "Contoso.OrderService.Diagnostics.ServiceState",
            "Contoso.OrderService.Dispatching.CarrierGateway",
            "System.String",
        ],
    };

    /// <summary>Proves keyword and identifier completion filters by prefix, case-insensitively.</summary>
    [Fact]
    public void Keywords_and_identifiers_complete_by_prefix()
    {
        var result = ExpressionCompletionService.Complete(CompletionCatalog.Empty, "tru", 3);
        Assert.Contains(result.Items, static item => item is { Text: "true", Kind: CompletionItemKind.Keyword });
        Assert.Equal(0, result.ReplaceStart);
        Assert.Equal(3, result.ReplaceLength);

        var types = ExpressionCompletionService.Complete(CompletionCatalog.Empty, "1 + Da", 6);
        Assert.Equal(
            ["DateOnly", "DateTime", "DateTimeKind", "DateTimeOffset", "DayOfWeek"],
            types.Items.Select(static item => item.Text).ToArray());
        Assert.Equal(4, types.ReplaceStart);
        Assert.Equal(2, types.ReplaceLength);

        // The root identifier completes only when a root is adopted; namespaces come from the catalog.
        Assert.Contains(
            ExpressionCompletionService.Complete(SampleCatalog, "ro", 2).Items,
            static item => item is { Text: "root", Kind: CompletionItemKind.Root });
        Assert.DoesNotContain(
            ExpressionCompletionService.Complete(CompletionCatalog.Empty, "ro", 2).Items,
            static item => item.Kind == CompletionItemKind.Root);
        Assert.Contains(
            ExpressionCompletionService.Complete(SampleCatalog, "Con", 3).Items,
            static item => item is { Text: "Contoso", Kind: CompletionItemKind.Namespace });

        // An empty token offers nothing, so the drop-down never opens over an untouched editor.
        Assert.Empty(ExpressionCompletionService.Complete(SampleCatalog, "", 0).Items);
    }

    /// <summary>Proves member completion after a dot follows the receiver.</summary>
    [Fact]
    public void Members_complete_after_a_dot()
    {
        var math = ExpressionCompletionService.Complete(CompletionCatalog.Empty, "Math.Sq", 7);
        Assert.Equal(["Sqrt"], math.Items.Select(static item => item.Text).ToArray());
        Assert.Equal(5, math.ReplaceStart);

        Assert.Contains(
            ExpressionCompletionService.Complete(CompletionCatalog.Empty, "DayOfWeek.", 10).Items,
            static item => item.Text == "Monday");
        Assert.Contains(
            ExpressionCompletionService.Complete(CompletionCatalog.Empty, "int.", 4).Items,
            static item => item.Text == "MaxValue");
        Assert.Contains(
            ExpressionCompletionService.Complete(CompletionCatalog.Empty, "double.N", 8).Items,
            static item => item.Text == "NaN");

        var rootMembers = ExpressionCompletionService.Complete(SampleCatalog, "root.", 5);
        Assert.Equal(
            ["CurrentBatch", "RecentDispatchDurationsMs"],
            rootMembers.Items.Select(static item => item.Text).ToArray());
        Assert.Contains(
            ExpressionCompletionService.Complete(SampleCatalog, "1 + root.Rec", 12).Items,
            static item => item.Text == "RecentDispatchDurationsMs");

        // An unknown receiver completes nothing rather than guessing.
        Assert.Empty(ExpressionCompletionService.Complete(SampleCatalog, "mystery.", 8).Items);
    }

    /// <summary>Proves acceptance rewrites exactly the partial token and reports the caret after it.</summary>
    [Fact]
    public void Acceptance_replaces_the_partial_token()
    {
        var result = ExpressionCompletionService.Complete(CompletionCatalog.Empty, "1 + Math.Sq + 2", 11);
        var item = Assert.Single(result.Items);
        var (newText, newCaret) = result.Apply("1 + Math.Sq + 2", item);
        Assert.Equal("1 + Math.Sqrt + 2", newText);
        Assert.Equal(13, newCaret);
    }

    /// <summary>Proves namespace drill-down and the pending type-member handshake.</summary>
    [Fact]
    public void Namespaces_drill_down_and_type_members_realize_on_demand()
    {
        var top = ExpressionCompletionService.Complete(SampleCatalog, "Contoso.", 8);
        Assert.Equal(["OrderService"], top.Items.Select(static item => item.Text).ToArray());

        var nested = ExpressionCompletionService.Complete(SampleCatalog, "Contoso.OrderService.", 21);
        Assert.Equal(
            ["Diagnostics", "Dispatching"],
            nested.Items.Select(static item => item.Text).ToArray());
        Assert.All(nested.Items, static item => Assert.Equal(CompletionItemKind.Namespace, item.Kind));

        var leaf = ExpressionCompletionService.Complete(SampleCatalog, "Contoso.OrderService.Diagnostics.", 33);
        Assert.Equal(["ServiceState"], leaf.Items.Select(static item => item.Text).ToArray());
        Assert.Equal(CompletionItemKind.Type, leaf.Items[0].Kind);

        // A known type without realized members asks the host to fetch them…
        var pending = ExpressionCompletionService.Complete(
            SampleCatalog, "Contoso.OrderService.Diagnostics.ServiceState.", 46);
        Assert.Empty(pending.Items);
        Assert.Equal("Contoso.OrderService.Diagnostics.ServiceState", pending.PendingTypeMembers);

        // …and once realized, the same query answers from the catalog.
        var realized = SampleCatalog with
        {
            TypeMembers = SampleCatalog.TypeMembers.SetItem(
                "Contoso.OrderService.Diagnostics.ServiceState",
                [new CompletionItem("Dispatcher", CompletionItemKind.Field, "static field")]),
        };
        var members = ExpressionCompletionService.Complete(
            realized, "Contoso.OrderService.Diagnostics.ServiceState.", 46);
        Assert.Equal(["Dispatcher"], members.Items.Select(static item => item.Text).ToArray());
        Assert.Null(members.PendingTypeMembers);
    }

    /// <summary>
    /// Proves the evidence-derived catalog against a real dump: the adopted root's fields, the target's own type
    /// names, and a metadata type's static members all complete.
    /// </summary>
    [Fact]
    [Trait("Category", "Dump")]
    public void Catalog_realizes_root_fields_and_metadata_names_from_a_dump()
    {
        var executable = PreviewDemoPaths.ResolveExecutable();
        Assert.True(File.Exists(executable), $"Expected the demo target at '{executable}'.");
        var dumpPath = Path.Combine(Path.GetTempPath(), $"completion-{Guid.NewGuid():N}.dmp");
        try
        {
            using (var target = TestTargetRunner.StartAndWaitReady(executable, [], isolatedDirectory: null))
            {
                DumpWriter.WriteFullDump(target.Pid, dumpPath);
            }

            var opened = ClrmdDumpSession.Open(dumpPath);
            Assert.Equal(ClrmdEvidenceStatus.Exact, opened.Status);
            using var session = opened.Value!;

            var promoted = ExpressionEvaluationService.EvaluateStaticField(
                session,
                "Contoso.OrderService.Diagnostics.ServiceState.Dispatcher",
                contextSelector: null,
                portablePdbCandidates: []);
            Assert.NotNull(promoted.PromotableRoot);

            var catalog = ExpressionCompletionService.BuildCatalog(session, promoted.PromotableRoot, "root");
            Assert.True(catalog.HasRoot);
            Assert.Contains(catalog.RootMembers, static item => item.Text == "RecentDispatchDurationsMs");
            Assert.Contains(
                "Contoso.OrderService.Diagnostics.ServiceState",
                catalog.TypeFullNames);

            var rootCompletion = ExpressionCompletionService.Complete(catalog, "root.Recent", 11);
            Assert.Contains(rootCompletion.Items, static item => item.Text == "RecentDispatchDurationsMs");

            var members = ExpressionCompletionService.ListStaticMemberCompletions(
                session, "Contoso.OrderService.Diagnostics.ServiceState");
            Assert.Contains(members, static item => item.Text == "Dispatcher");
        }
        finally
        {
            if (File.Exists(dumpPath))
            {
                File.Delete(dumpPath);
            }
        }
    }
}
