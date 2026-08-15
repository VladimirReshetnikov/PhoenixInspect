using System.Collections.Immutable;
using PhoenixInspect.Host.Dump.ClrMD;
using PhoenixInspect.Inspection;
using PhoenixInspect.Product.DumpQuery;
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

    /// <summary>Immutable collections complete: receivers, factories, and stored-variable surfaces.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Immutable_collections_complete()
    {
        var receivers = ExpressionCompletionService.Complete(CompletionCatalog.Empty, "Immutable", 9);
        Assert.Contains(receivers.Items, static item => item.Text == "ImmutableArray");
        Assert.Contains(receivers.Items, static item => item.Text == "ImmutableSortedSet");

        var factory = ExpressionCompletionService.Complete(CompletionCatalog.Empty, "ImmutableArray.", 15);
        Assert.Equal(
            ["Create", "CreateRange", "Empty"],
            factory.Items.Select(static item => item.Text).ToArray());

        // A stored ImmutableList completes its own surface plus the shared sequence one, without the
        // array-only members; a stored ImmutableArray keeps Length and has no Count property.
        var list = ExpressionCompletionService.InstanceMembersForStoredType("ImmutableList<Int32>");
        Assert.Contains(list, static item => item.Text == "Add");
        Assert.Contains(list, static item => item is { Text: "Count", Detail: "property" });
        Assert.Contains(list, static item => item.Text == "Where");
        Assert.DoesNotContain(list, static item => item.Text == "Length");

        var array = ExpressionCompletionService.InstanceMembersForStoredType("ImmutableArray<Int32>");
        Assert.Contains(array, static item => item is { Text: "Length", Detail: "property" });
        Assert.DoesNotContain(array, static item => item is { Text: "Count", Detail: "property" });

        // Chains keep their type through the persistent operations: xs.Add(3). completes the list surface.
        var locals = new CompletionContext
        {
            Locals = [new CompletionItem("xs", CompletionItemKind.Local, "ImmutableList<Int32>")],
        };
        var chained = ExpressionCompletionService.Complete(
            CompletionCatalog.Empty, "xs.Add(3).", 10, locals);
        Assert.Contains(chained.Items, static item => item.Text == "Add");
        var indexed = ExpressionCompletionService.Complete(
            CompletionCatalog.Empty, "xs[0].", 6, locals);
        Assert.Contains(indexed.Items, static item => item.Text == "CompareTo");
    }

    /// <summary>Proves member completion after a dot follows the receiver.</summary>
    [Fact]
    public void Members_complete_after_a_dot()
    {
        // The prefix match ranks first; the camel-hump match on the same token follows it.
        var math = ExpressionCompletionService.Complete(CompletionCatalog.Empty, "Math.Sq", 7);
        Assert.Equal(
            ["Sqrt", "ReciprocalSqrtEstimate"],
            math.Items.Select(static item => item.Text).ToArray());
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
        var item = result.Items[0];
        Assert.Equal("Sqrt", item.Text);
        var (newText, newCaret) = result.Apply("1 + Math.Sq + 2", item);
        Assert.Equal("1 + Math.Sqrt + 2", newText);
        Assert.Equal(13, newCaret);
    }

    /// <summary>Proves camel-hump and substring matching, ranked below prefix matches, IDE-style.</summary>
    [Fact]
    public void Humps_and_substrings_match_and_rank_below_prefixes()
    {
        // Camel humps: 'DTO' and 'dto' both find DateTimeOffset, like ReSharper and Rider.
        Assert.Contains(
            ExpressionCompletionService.Complete(CompletionCatalog.Empty, "DTO", 3).Items,
            static item => item.Text == "DateTimeOffset");
        Assert.Contains(
            ExpressionCompletionService.Complete(CompletionCatalog.Empty, "dto", 3).Items,
            static item => item.Text == "DateTimeOffset");

        // Middle humps match too: 'SqEs' finds ReciprocalSqrtEstimate among Math's members.
        Assert.Contains(
            ExpressionCompletionService.Complete(CompletionCatalog.Empty, "Math.SqEs", 9).Items,
            static item => item.Text == "ReciprocalSqrtEstimate");

        // Substring matching finds a token buried mid-name.
        Assert.Contains(
            ExpressionCompletionService.Complete(CompletionCatalog.Empty, "double.finity", 13).Items,
            static item => item.Text == "NegativeInfinity");

        // A numeric literal is not an identifier: typing '32' must not surface Int32-shaped names.
        Assert.Empty(ExpressionCompletionService.Complete(CompletionCatalog.Empty, "32", 2).Items);

        // A single character stays strict, so one keystroke never floods the list with loose matches.
        Assert.DoesNotContain(
            ExpressionCompletionService.Complete(CompletionCatalog.Empty, "Math.q", 6).Items,
            static item => item.Text == "Sqrt");

        // A fully typed candidate stays visible and ranks first, so the selection never falls to a longer
        // neighbor — Enter over it submits instead of rewriting 's' into 'sbyte'.
        var exact = ExpressionCompletionService.Complete(CompletionCatalog.Empty, "string", 6);
        Assert.Equal("string", exact.Items[0].Text);
        Assert.Contains(exact.Items, static item => item.Text == "String");
    }

    /// <summary>Proves instance-member completion after a declared variable's dot mirrors the evaluator.</summary>
    [Fact]
    public void Locals_complete_instance_members_by_stored_type()
    {
        var context = new CompletionContext
        {
            Locals =
            [
                new CompletionItem("s", CompletionItemKind.Local, "String"),
                new CompletionItem("xs", CompletionItemKind.Local, "Int32[]"),
                new CompletionItem("f", CompletionItemKind.Local, "Func<int, int>"),
                new CompletionItem("day", CompletionItemKind.Local, "System.DayOfWeek"),
                new CompletionItem("t", CompletionItemKind.Local, "TimeSpan"),
            ],
        };

        // A string variable offers the folded string surface: the Length property and the instance methods.
        var text = ExpressionCompletionService.Complete(CompletionCatalog.Empty, "s.", 2, context);
        Assert.Contains(text.Items, static item => item is { Text: "Length", Detail: "property" });
        Assert.Contains(text.Items, static item => item is { Text: "Substring", Detail: "method" });

        // Prefix filtering and ranking apply to the instance surface too.
        var trimmed = ExpressionCompletionService.Complete(CompletionCatalog.Empty, "s.Tr", 4, context);
        Assert.Equal("Trim", trimmed.Items[0].Text);

        // An array variable offers the sequence surface, including the lambda query operators.
        var sequence = ExpressionCompletionService.Complete(CompletionCatalog.Empty, "xs.", 3, context);
        Assert.Contains(sequence.Items, static item => item.Text == "Length");
        Assert.Contains(sequence.Items, static item => item.Text == "Where");
        Assert.Contains(sequence.Items, static item => item.Text == "Average");

        // A delegate variable offers the delegate surface; a temporal variable its property set.
        Assert.Contains(
            ExpressionCompletionService.Complete(CompletionCatalog.Empty, "f.", 2, context).Items,
            static item => item.Text == "Invoke");
        Assert.Contains(
            ExpressionCompletionService.Complete(CompletionCatalog.Empty, "t.Total", 7, context).Items,
            static item => item.Text == "TotalSeconds");

        // A stored enum reads back as its underlying numeric value, so only the universal members apply.
        var enumMembers = ExpressionCompletionService.Complete(CompletionCatalog.Empty, "day.", 4, context);
        Assert.Equal(["GetType", "ToString"], enumMembers.Items.Select(static item => item.Text).ToArray());

        // An unknown receiver still completes nothing rather than guessing.
        Assert.Empty(ExpressionCompletionService.Complete(CompletionCatalog.Empty, "mystery.", 8, context).Items);
    }

    /// <summary>Proves root member chains realize instance fields per declared hop type, on demand.</summary>
    [Fact]
    public void Root_chains_walk_declared_field_types()
    {
        // The first hop past a root field asks the host to realize that field type's instance fields…
        var pending = ExpressionCompletionService.Complete(SampleCatalog, "root.CurrentBatch.", 18);
        Assert.Empty(pending.Items);
        Assert.Equal("Contoso.OrderService.Batch", pending.PendingInstanceMembers);

        // …and once realized, the hop completes from the catalog, chaining detail-to-detail.
        var realized = SampleCatalog with
        {
            TypeInstanceMembers = SampleCatalog.TypeInstanceMembers
                .SetItem(
                    "Contoso.OrderService.Batch",
                    [
                        new CompletionItem("Orders", CompletionItemKind.Field, "Contoso.OrderService.Order[]"),
                        new CompletionItem("BatchId", CompletionItemKind.Field, "System.Int32"),
                    ])
                .SetItem("System.Int32", []),
        };
        var members = ExpressionCompletionService.Complete(realized, "root.CurrentBatch.", 18);
        Assert.Equal(["BatchId", "Orders"], members.Items.Select(static item => item.Text).ToArray());
        Assert.Null(members.PendingInstanceMembers);

        var filtered = ExpressionCompletionService.Complete(realized, "root.CurrentBatch.Ord", 21);
        Assert.Equal(["Orders"], filtered.Items.Select(static item => item.Text).ToArray());

        // A deeper hop pends on the next declared type; a realized empty answer completes nothing, finally.
        var deeper = ExpressionCompletionService.Complete(realized, "root.CurrentBatch.Orders.", 25);
        Assert.Equal("Contoso.OrderService.Order[]", deeper.PendingInstanceMembers);
        var terminal = ExpressionCompletionService.Complete(realized, "root.CurrentBatch.BatchId.", 26);
        Assert.Empty(terminal.Items);
        Assert.Null(terminal.PendingInstanceMembers);

        // An unknown hop offers nothing rather than guessing.
        Assert.Empty(ExpressionCompletionService.Complete(realized, "root.Mystery.", 13).Items);
    }

    /// <summary>Proves a static member with a modeled value completes that value's instance surface.</summary>
    [Fact]
    public void Static_value_chains_complete_instance_surfaces()
    {
        Assert.Contains(
            ExpressionCompletionService.Complete(CompletionCatalog.Empty, "Guid.Empty.", 11).Items,
            static item => item is { Text: "Variant", Detail: "property" });
        Assert.Contains(
            ExpressionCompletionService.Complete(CompletionCatalog.Empty, "Encoding.UTF8.Get", 17).Items,
            static item => item.Text == "GetBytes");
        Assert.Contains(
            ExpressionCompletionService.Complete(CompletionCatalog.Empty, "TimeSpan.Zero.Tot", 17).Items,
            static item => item.Text == "TotalSeconds");

        // An enum member offers the enum value surface; a numeric bound the universal scalar members.
        Assert.Equal(
            ["CompareTo", "GetType", "HasFlag", "ToString"],
            ExpressionCompletionService.Complete(CompletionCatalog.Empty, "DayOfWeek.Monday.", 17)
                .Items.Select(static item => item.Text).ToArray());
        Assert.Equal(
            ["GetType", "ToString"],
            ExpressionCompletionService.Complete(CompletionCatalog.Empty, "int.MaxValue.", 13)
                .Items.Select(static item => item.Text).ToArray());

        // A member that produces an explained stop folds no value, so nothing chains after it.
        Assert.Empty(ExpressionCompletionService.Complete(CompletionCatalog.Empty, "DateTime.Now.", 13).Items);

        // A deeper chain keeps folding member result types: Guid.Empty.Version is an Int32.
        Assert.Equal(
            ["GetType", "ToString"],
            ExpressionCompletionService.Complete(CompletionCatalog.Empty, "Guid.Empty.Version.", 19)
                .Items.Select(static item => item.Text).ToArray());
    }

    /// <summary>Proves chains fold through method calls, index accesses, and literal heads.</summary>
    [Fact]
    public void Chains_fold_through_calls_indexes_and_literals()
    {
        var context = new CompletionContext
        {
            Locals =
            [
                new CompletionItem("s", CompletionItemKind.Local, "String"),
                new CompletionItem("xs", CompletionItemKind.Local, "Int32[]"),
                new CompletionItem("t", CompletionItemKind.Local, "TimeSpan"),
            ],
        };

        // A method call folds to its result type: Trim() is a String again, Length an Int32.
        Assert.Contains(
            ExpressionCompletionService.Complete(CompletionCatalog.Empty, "s.Trim().Len", 12, context).Items,
            static item => item.Text == "Length");
        Assert.Equal(
            ["GetType", "ToString"],
            ExpressionCompletionService.Complete(CompletionCatalog.Empty, "s.Length.", 9, context)
                .Items.Select(static item => item.Text).ToArray());

        // Calls with arguments and index accesses fold too: Split(',') is a String[], its element a String.
        Assert.Contains(
            ExpressionCompletionService.Complete(CompletionCatalog.Empty, "s.Split(',').", 13, context).Items,
            static item => item.Text == "Where");
        Assert.Contains(
            ExpressionCompletionService.Complete(CompletionCatalog.Empty, "s.Split(',')[0].Sub", 19, context)
                .Items,
            static item => item.Text == "Substring");
        Assert.Equal(
            ["GetType", "ToString"],
            ExpressionCompletionService.Complete(CompletionCatalog.Empty, "xs[0].", 6, context)
                .Items.Select(static item => item.Text).ToArray());

        // A literal head carries its own type.
        Assert.Contains(
            ExpressionCompletionService.Complete(CompletionCatalog.Empty, "\"phoenix\".Len", 13, context).Items,
            static item => item.Text == "Length");
        Assert.Contains(
            ExpressionCompletionService.Complete(CompletionCatalog.Empty, "\"a,b\".Split(',').", 17, context)
                .Items,
            static item => item.Text == "First");

        // GetType() opens the reflective surface, and its members keep chaining.
        Assert.Contains(
            ExpressionCompletionService.Complete(CompletionCatalog.Empty, "s.GetType().Full", 16, context).Items,
            static item => item.Text == "FullName");
        Assert.Contains(
            ExpressionCompletionService.Complete(CompletionCatalog.Empty, "s.GetType().Name.", 17, context)
                .Items,
            static item => item.Text == "Substring");

        // Temporal chains: Duration() folds a TimeSpan; an enum-typed property offers the enum surface.
        Assert.Contains(
            ExpressionCompletionService.Complete(CompletionCatalog.Empty, "t.Duration().Tot", 16, context).Items,
            static item => item.Text == "TotalSeconds");
        Assert.Equal(
            ["CompareTo", "GetType", "HasFlag", "ToString"],
            ExpressionCompletionService.Complete(
                CompletionCatalog.Empty, "DateTime.MaxValue.DayOfWeek.", 28)
                .Items.Select(static item => item.Text).ToArray());
        Assert.Contains(
            ExpressionCompletionService.Complete(
                CompletionCatalog.Empty, "DateTime.MaxValue.AddDays(1).", 29).Items,
            static item => item.Text == "Year");

        // A method group without parentheses folds no value, and a property spelled as a call folds none either.
        Assert.Empty(
            ExpressionCompletionService.Complete(CompletionCatalog.Empty, "s.Trim.", 7, context).Items);
        Assert.Empty(
            ExpressionCompletionService.Complete(CompletionCatalog.Empty, "s.Length().", 11, context).Items);
    }

    /// <summary>Proves using directives admit prefix-less completion: namespaces, aliases, static imports.</summary>
    [Fact]
    public void Using_directives_admit_prefixless_names()
    {
        var usings = UsingDirectiveSet.Empty
            .WithImportedNamespace("Contoso.OrderService.Diagnostics")
            .WithImportedNamespace("Contoso")
            .WithAlias("D", "Contoso.OrderService.Dispatching")
            .WithAlias("S", "System.String")
            .WithStaticImport("Contoso.OrderService.Diagnostics.ServiceState");
        var context = new CompletionContext { Usings = usings };
        var realizedCatalog = SampleCatalog with
        {
            TypeMembers = SampleCatalog.TypeMembers.SetItem(
                "Contoso.OrderService.Diagnostics.ServiceState",
                [new CompletionItem("Dispatcher", CompletionItemKind.Field, "static field")]),
        };

        // A type from an imported namespace completes bare, annotated with the namespace that admits it;
        // a shallower import offers its next namespace segment.
        Assert.Contains(
            ExpressionCompletionService.Complete(SampleCatalog, "ServiceSt", 9, context).Items,
            static item => item is
            { Text: "ServiceState", Kind: CompletionItemKind.Type, Detail: "Contoso.OrderService.Diagnostics" });
        Assert.Contains(
            ExpressionCompletionService.Complete(SampleCatalog, "OrderSer", 8, context).Items,
            static item => item is { Text: "OrderService", Kind: CompletionItemKind.Namespace });

        // A prefix-less member access resolves through the import: unrealized pends, realized answers.
        Assert.Equal(
            "Contoso.OrderService.Diagnostics.ServiceState",
            ExpressionCompletionService.Complete(SampleCatalog, "ServiceState.", 13, context).PendingTypeMembers);
        Assert.Contains(
            ExpressionCompletionService.Complete(realizedCatalog, "ServiceState.", 13, context).Items,
            static item => item.Text == "Dispatcher");

        // A namespace alias completes as an identifier and drills down like its target; a type alias pends
        // its target's members.
        Assert.Contains(
            ExpressionCompletionService.Complete(SampleCatalog, "D", 1, context).Items,
            static item => item is { Text: "D", Detail: "Contoso.OrderService.Dispatching" });
        Assert.Contains(
            ExpressionCompletionService.Complete(SampleCatalog, "D.", 2, context).Items,
            static item => item is { Text: "CarrierGateway", Kind: CompletionItemKind.Type });
        Assert.Equal(
            "System.String",
            ExpressionCompletionService.Complete(SampleCatalog, "S.", 2, context).PendingTypeMembers);

        // A static import offers the type's members bare once realized, and pends their fetch before.
        Assert.Equal(
            "Contoso.OrderService.Diagnostics.ServiceState",
            ExpressionCompletionService.Complete(SampleCatalog, "Disp", 4, context).PendingTypeMembers);
        Assert.Contains(
            ExpressionCompletionService.Complete(realizedCatalog, "Disp", 4, context).Items,
            static item => item.Text == "Dispatcher");

        // Without the directives, none of those spellings complete.
        Assert.Empty(ExpressionCompletionService.Complete(SampleCatalog, "ServiceState.", 13).Items);
    }

    /// <summary>Proves '#r' references contribute their types, namespaces, and static members.</summary>
    [Fact]
    public void References_contribute_types_and_members()
    {
        var loaded = ReferenceAssembly.TryLoad(
            typeof(ExpressionCompletionService).Assembly.Location, alias: null, out var error);
        Assert.Null(error);
        var index = new ReferenceCompletionIndex([loaded!]);
        var context = new CompletionContext { References = index };

        // The reference's namespaces complete at the top level and drill down like dump-module names.
        Assert.Contains(
            ExpressionCompletionService.Complete(CompletionCatalog.Empty, "Phoen", 5, context).Items,
            static item => item is
            { Text: "PhoenixInspect", Kind: CompletionItemKind.Namespace, Detail: "from references" });
        Assert.Contains(
            ExpressionCompletionService.Complete(CompletionCatalog.Empty, "PhoenixInspect.", 15, context).Items,
            static item => item.Text == "Inspection");

        // A referenced type's static members realize synchronously — no pending handshake.
        var members = ExpressionCompletionService.Complete(
            CompletionCatalog.Empty,
            "PhoenixInspect.Inspection.ExpressionCompletionService.Maximum",
            "PhoenixInspect.Inspection.ExpressionCompletionService.Maximum".Length,
            context);
        Assert.Contains(members.Items, static item => item is { Text: "MaximumItems", Detail: "const" });
        Assert.Null(members.PendingTypeMembers);

        // Using directives reach into references exactly as they reach into dump modules.
        var usingContext = context with
        {
            Usings = UsingDirectiveSet.Empty.WithImportedNamespace("PhoenixInspect.Inspection"),
        };
        Assert.Contains(
            ExpressionCompletionService.Complete(
                CompletionCatalog.Empty, "ExpressionCompletionServ", 24, usingContext).Items,
            static item => item is { Text: "ExpressionCompletionService", Kind: CompletionItemKind.Type });
        Assert.Contains(
            ExpressionCompletionService.Complete(
                CompletionCatalog.Empty, "ExpressionCompletionService.Maximum", 35, usingContext).Items,
            static item => item.Text == "MaximumItems");

        // An aliased reference is reachable only through its extern alias, so it contributes nothing bare.
        var aliased = ReferenceAssembly.TryLoad(
            typeof(ExpressionCompletionService).Assembly.Location, alias: "ext", out _);
        Assert.True(new ReferenceCompletionIndex([aliased!]).IsEmpty);
    }

    /// <summary>Proves signature help resolves modeled calls, counts parameters, and stays quiet elsewhere.</summary>
    [Fact]
    public void Signature_help_describes_modeled_calls()
    {
        // A static receiver's method shows its overloads, aligned at the opening parenthesis.
        var sqrt = ExpressionCompletionService.GetSignatureHelp(null, "Math.Sqrt(", 10);
        Assert.NotNull(sqrt);
        Assert.Equal("Math", sqrt!.ReceiverDisplay);
        Assert.Equal(0, sqrt.ActiveParameter);
        Assert.Equal(9, sqrt.OpenParenOffset);
        var overload = Assert.Single(sqrt.Signatures);
        Assert.Equal("Sqrt", overload.MethodName);
        Assert.Equal("double", Assert.Single(overload.Parameters).TypeText);

        // Commas advance the active parameter; nested calls bind to the innermost open parenthesis.
        Assert.Equal(1, ExpressionCompletionService.GetSignatureHelp(null, "Math.Clamp(1, ", 14)!.ActiveParameter);
        var inner = ExpressionCompletionService.GetSignatureHelp(null, "Math.Max(Math.Min(1, ", 21);
        Assert.Equal("Min", inner!.Signatures[0].MethodName);

        // Instance methods resolve through the typed chain, including literal heads.
        var context = new CompletionContext
        {
            Locals = [new CompletionItem("s", CompletionItemKind.Local, "String")],
        };
        var substring = ExpressionCompletionService.GetSignatureHelp(context, "s.Substring(", 12);
        Assert.Equal("String", substring!.ReceiverDisplay);
        Assert.Contains(substring.Signatures, static signature => signature.Parameters.Length == 2);
        Assert.NotNull(ExpressionCompletionService.GetSignatureHelp(null, "\"abc\".Substring(", 16));

        // A sequence operator shows its LINQ shape without the extension-method source parameter.
        var whereContext = new CompletionContext
        {
            Locals = [new CompletionItem("xs", CompletionItemKind.Local, "Int32[]")],
        };
        var where = ExpressionCompletionService.GetSignatureHelp(whereContext, "xs.Where(", 9);
        Assert.NotNull(where);
        Assert.All(where!.Signatures, static signature => Assert.Single(signature.Parameters));

        // A grouping parenthesis, an unmodeled call, and a closed call all stay quiet.
        Assert.Null(ExpressionCompletionService.GetSignatureHelp(null, "(1 + 2", 6));
        Assert.Null(ExpressionCompletionService.GetSignatureHelp(null, "mystery.Frob(", 13));
        Assert.Null(ExpressionCompletionService.GetSignatureHelp(null, "Math.Sqrt(4) + ", 15));
    }

    /// <summary>Proves the immediate-window context: locals, statement keywords, and explicit invocation.</summary>
    [Fact]
    public void Immediate_context_offers_locals_and_statement_keywords()
    {
        var context = new CompletionContext
        {
            AllowsStatements = true,
            Locals =
            [
                new CompletionItem("total", CompletionItemKind.Local, "Int32"),
                new CompletionItem("x", CompletionItemKind.Local, "Double"),
            ],
        };

        // Declared variables complete as identifiers, annotated with their value types.
        Assert.Contains(
            ExpressionCompletionService.Complete(CompletionCatalog.Empty, "tot", 3, context).Items,
            static item => item is { Text: "total", Kind: CompletionItemKind.Local, Detail: "Int32" });

        // Statement keywords offer at the start of a line, and only in editors that admit statements.
        Assert.Contains(
            ExpressionCompletionService.Complete(CompletionCatalog.Empty, "va", 2, context).Items,
            static item => item is { Text: "var", Kind: CompletionItemKind.Keyword });
        Assert.Contains(
            ExpressionCompletionService.Complete(CompletionCatalog.Empty, "  us", 4, context).Items,
            static item => item is { Text: "using", Kind: CompletionItemKind.Keyword });
        Assert.DoesNotContain(
            ExpressionCompletionService.Complete(CompletionCatalog.Empty, "1 + va", 6, context).Items,
            static item => item.Text == "var");
        Assert.DoesNotContain(
            ExpressionCompletionService.Complete(CompletionCatalog.Empty, "va", 2).Items,
            static item => item.Text == "var");

        // An explicit ask (Ctrl+Space) completes an empty token, offering the whole applicable universe.
        var explicitAll = ExpressionCompletionService.Complete(
            SampleCatalog, "", 0, context, explicitInvocation: true);
        Assert.Contains(explicitAll.Items, static item => item is { Text: "root", Kind: CompletionItemKind.Root });
        Assert.Contains(explicitAll.Items, static item => item is { Text: "total", Kind: CompletionItemKind.Local });
        Assert.Contains(explicitAll.Items, static item => item is { Text: "true", Kind: CompletionItemKind.Keyword });
        Assert.Equal(0, explicitAll.ReplaceStart);
        Assert.Equal(0, explicitAll.ReplaceLength);

        // As-you-type completion still waits for a first character.
        Assert.Empty(ExpressionCompletionService.Complete(SampleCatalog, "", 0, context).Items);
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

            // Instance fields realize by runtime type name, carrying each field's type for the next hop.
            var heapObject = promoted.PromotableRoot!.TryResolveHeapObject(session);
            Assert.NotNull(heapObject);
            var instanceMembers = ExpressionCompletionService.ListInstanceMemberCompletions(
                session, heapObject!.TypeName);
            Assert.Contains(
                instanceMembers,
                static item => item is { Text: "RecentDispatchDurationsMs", Kind: CompletionItemKind.Field });
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
