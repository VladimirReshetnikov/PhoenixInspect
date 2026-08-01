using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using PhoenixInspect.Core.Abstractions;
using PhoenixInspect.Host.Abstractions;
using PhoenixInspect.Host.Dump.ClrMD;
using PhoenixInspect.Product.DumpDebugging;
using PhoenixInspect.Product.DumpQuery;

namespace PhoenixInspect.Inspection;

/// <summary>Identifies which implemented expression entry point the demo should invoke.</summary>
public enum ExpressionPath
{
    /// <summary>
    /// The static-field pipeline, which parses a namespace-qualified static-field expression and binds it from counted
    /// module metadata plus optional selected-frame and Portable-PDB name context.
    /// </summary>
    StaticField,

    /// <summary>
    /// The root-relative pipeline, which evaluates a member expression against one exact heap object selected as the
    /// expression root.
    /// </summary>
    RootRelative,
}

/// <summary>
/// Invokes the implemented expression entry points and projects their complete results into display reports.
/// </summary>
/// <remarks>
/// The service never collapses a partial, absent, ambiguous, or conflicting outcome into an error string. Each report
/// carries the product's own status vocabulary, the evidence that produced it, the deterministic bounds actually
/// reached, and the canonical replay digest, so a viewer can tell an exact answer from a bounded one.
/// </remarks>
public static class ExpressionEvaluationService
{
    /// <summary>Evaluates a static-field expression, optionally consulting one selected frame for name context.</summary>
    /// <param name="session">The open dump session.</param>
    /// <param name="expression">The raw expression text, submitted without normalization.</param>
    /// <param name="contextSelector">
    /// The snapshot-scoped frame whose namespace, import, and alias facts may bind a contextual name, or null to
    /// require a context-independent fully qualified name.
    /// </param>
    /// <param name="portablePdbCandidates">Caller-discovered Portable-PDB candidate paths, possibly empty.</param>
    /// <returns>
    /// A complete display report for the outcome. When the field held a validated object reference the report also
    /// carries that object, because the strong-handle catalog cannot reach an object that no handle roots and
    /// promoting a static-field value is then the only supported way to obtain an instance root.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is null.</exception>
    public static EvaluationReport EvaluateStaticField(
        ClrmdDumpSession session,
        string expression,
        DumpSelectedFrameSelector? contextSelector,
        ImmutableArray<string> portablePdbCandidates)
    {
        ArgumentNullException.ThrowIfNull(session);
        var candidates = portablePdbCandidates.IsDefault ? [] : portablePdbCandidates;
        var stopwatch = Stopwatch.StartNew();

        // Constant expressions — folded arithmetic, metadata literal fields, and composed expressions consuming
        // stored static values as operands — are a pre-stage the frozen static-field pipeline rejects by design.
        // A not-constant disposition falls through untouched, so every bare stored-field answer stays exactly
        // what it was.
        var resolvers = new ConstantOperandResolvers
        {
            StaticName = name => MapStaticOperand(name, contextSelector is null
                ? StaticFieldExpressionEvaluator.Evaluate(session, name)
                : StaticFieldExpressionEvaluator.Evaluate(session, name, contextSelector, candidates)),
        };
        var constant = ConstantExpressionEvaluator.Evaluate(session, expression, resolvers);
        if (constant.Status != ConstantExpressionStatus.NotConstant)
        {
            stopwatch.Stop();
            return BuildConstantReport(expression, constant, stopwatch.Elapsed);
        }

        var result = contextSelector is null
            ? StaticFieldExpressionEvaluator.Evaluate(session, expression)
            : StaticFieldExpressionEvaluator.Evaluate(session, expression, contextSelector, candidates);
        stopwatch.Stop();
        var promotable =
            result.SuffixKind == StaticFieldExpressionSuffixKind.None &&
            result.ObjectBinding is { } objectBinding &&
            result.HostObservation?.Value?.ObjectReference is { } reference
                ? RootSelection.FromObjectBinding(reference, objectBinding, expression)
                : null;
        return BuildStaticFieldReport(expression, contextSelector, result, stopwatch.Elapsed) with
        {
            PromotableRoot = promotable,
        };
    }

    /// <summary>Evaluates a root-relative expression against one exact heap object.</summary>
    /// <param name="session">The open dump session.</param>
    /// <param name="expression">The raw expression text, submitted without normalization.</param>
    /// <param name="root">The selected root, resolved to a product binding on the session thread.</param>
    /// <param name="rootIdentifier">The case-sensitive identifier the expression uses for the root.</param>
    /// <param name="policy">The closed policy applied to the method path; field paths retain its limits unused.</param>
    /// <param name="languageProfile">The frozen or opt-in fixed-depth member-chain admission profile.</param>
    /// <returns>
    /// A complete display report for whichever path the classifier selected, or a report describing why the selected
    /// root could not be bound.
    /// </returns>
    /// <exception cref="ArgumentNullException">A required argument is null.</exception>
    public static EvaluationReport EvaluateRootRelative(
        ClrmdDumpSession session,
        string expression,
        RootSelection root,
        string rootIdentifier,
        DumpExpressionPolicy policy,
        DumpExpressionLanguageProfile languageProfile)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(policy);
        var (rootBinding, stop) = root.Resolve(session, rootIdentifier);
        if (rootBinding is null)
        {
            return new EvaluationReport
            {
                Expression = expression,
                Path = "Root-relative expression",
                Severity = EvaluationSeverity.Stopped,
                Status = "Root unavailable",
                Stage = "Root binding stopped before classification",
                Value = "No value was produced.",
                Facts = [new PropertyRow("Root", "Selection", root.Description)],
                Diagnostics = [new DiagnosticRow("EXPLORER_ROOT_UNRESOLVED", stop!)],
            };
        }

        var stopwatch = Stopwatch.StartNew();

        // A composed expression — arithmetic, comparison, or string composition around one or more root-relative
        // chains — folds in the constant pre-stage, with each chain evaluated by the frozen root-relative pipeline
        // and consumed as an exact operand. A bare chain, invocation, or chain-with-fallback never enters the
        // pre-stage, so every existing answer keeps its evidence report and replay identity.
        var resolvers = new ConstantOperandResolvers
        {
            RootIdentifier = rootIdentifier,
            RootChain = chain => MapRootOperand(
                chain,
                DumpExpressionEvaluator.Evaluate(session, chain, rootBinding, policy, languageProfile)),
            StaticName = name => MapStaticOperand(name, StaticFieldExpressionEvaluator.Evaluate(session, name)),
        };
        var constant = ConstantExpressionEvaluator.Evaluate(session, expression, resolvers);
        if (constant.Status != ConstantExpressionStatus.NotConstant)
        {
            stopwatch.Stop();
            return BuildConstantReport(expression, constant, stopwatch.Elapsed);
        }

        var outcome = DumpExpressionEvaluator.Evaluate(session, expression, rootBinding, policy, languageProfile);
        stopwatch.Stop();
        return BuildRootRelativeReport(expression, rootBinding, languageProfile, outcome, stopwatch.Elapsed);
    }

    private static ConstantOperandResolution MapStaticOperand(
        string name,
        StaticFieldExpressionEvaluationResult result)
    {
        if (result.Status != StaticFieldExpressionEvaluationStatus.Exact)
        {
            return ConstantOperandResolution.ForStop(
                result.DiagnosticCode ?? "CONSTANT_DUMP_OPERAND_NOT_EXACT",
                result.DiagnosticMessage
                    ?? $"Operand '{name}' did not produce an exact value; the static-field pipeline reported "
                    + $"{result.Status}.");
        }

        if (result.SuffixResult is { } suffix)
        {
            return MapQueryValue(name, suffix.Value);
        }

        var value = result.HostObservation?.Value;
        return value?.Kind switch
        {
            ClrmdStaticFieldTerminalKind.Int32 or ClrmdStaticFieldTerminalKind.NullableInt32Value =>
                ConstantOperandResolution.FromInt32(value.Int32Value!.Value),
            ClrmdStaticFieldTerminalKind.Null or ClrmdStaticFieldTerminalKind.NullableInt32NoValue =>
                ConstantOperandResolution.ExactNull(),
            ClrmdStaticFieldTerminalKind.String =>
                ConstantOperandResolution.FromString(value.StringValue!.Value),
            ClrmdStaticFieldTerminalKind.ObjectReference => ConstantOperandResolution.ForStop(
                "CONSTANT_DUMP_OPERAND_TYPE_UNSUPPORTED",
                $"Operand '{name}' is an object reference; only Int32, string, and null dump values compose "
                + "into expressions."),
            _ => ConstantOperandResolution.ForStop(
                "CONSTANT_DUMP_OPERAND_NOT_EXACT",
                $"Operand '{name}' produced no value."),
        };
    }

    private static ConstantOperandResolution MapRootOperand(string chain, DumpExpressionEvaluationOutcome outcome)
    {
        if (outcome.Kind != DumpExpressionEvaluationOutcomeKind.DerivedQuery ||
            outcome.DerivedQueryResult is not { } derived)
        {
            // A chain the root-relative classifier does not admit keeps the whole expression not-constant, so the
            // frozen path reports the rejection with its own vocabulary.
            return ConstantOperandResolution.OutsideDomain();
        }

        if (MapSeverity(derived) != EvaluationSeverity.Exact)
        {
            var code = "CONSTANT_DUMP_OPERAND_NOT_EXACT";
            var message = $"Operand '{chain}' did not produce an exact value.";
            foreach (var diagnostic in derived.Diagnostics)
            {
                code = diagnostic.Code;
                message = diagnostic.Message;
                break;
            }

            return ConstantOperandResolution.ForStop(code, message);
        }

        return MapQueryValue(chain, derived.Value);
    }

    private static ConstantOperandResolution MapQueryValue(string name, DumpQueryValue? value) => value?.Kind switch
    {
        DumpQueryValueKind.Null => ConstantOperandResolution.ExactNull(),
        DumpQueryValueKind.Int32 => ConstantOperandResolution.FromInt32(value.Int32Value!.Value),
        DumpQueryValueKind.String => ConstantOperandResolution.FromString(value.StringValue!),
        _ => ConstantOperandResolution.ForStop(
            "CONSTANT_DUMP_OPERAND_NOT_EXACT",
            $"Operand '{name}' produced no value."),
    };

    private static EvaluationReport BuildConstantReport(
        string expression,
        ConstantExpressionEvaluation constant,
        TimeSpan duration)
    {
        var facts = ImmutableArray.CreateBuilder<PropertyRow>();
        facts.Add(new PropertyRow("Routing", "Entry point", "ConstantExpressionEvaluator"));
        facts.Add(new PropertyRow(
            "Routing",
            "Domain",
            "Constant expression",
            "No runtime value is read: folded arithmetic depends on no dump evidence, and a literal field's value "
            + "comes from the module's metadata Constant table."));
        facts.Add(new PropertyRow(
            "Syntax",
            "Profile",
            "RoslynCSharpExpressionV1",
            "One complete pinned Roslyn C# expression parse; checked Int32 semantics for arithmetic."));

        var isLiteralField = constant.FieldToken is not null;
        if (isLiteralField)
        {
            facts.Add(new PropertyRow("Constant", "Module", constant.ModuleName ?? DisplayFormatting.Absent));
            facts.Add(new PropertyRow(
                "Constant",
                "Metadata digest",
                DisplayFormatting.ShortDigest(constant.ModuleContentSha256)));
            facts.Add(new PropertyRow("Constant", "TypeDef", DisplayFormatting.Token(constant.TypeToken!.Value)));
            facts.Add(new PropertyRow("Constant", "FieldDef", DisplayFormatting.Token(constant.FieldToken!.Value)));
            facts.Add(new PropertyRow(
                "Constant",
                "Constant type code",
                constant.UnderlyingTypeName ?? DisplayFormatting.Absent));
        }

        if (constant.ModuleCount > 0)
        {
            facts.Add(new PropertyRow(
                "Constant",
                "Modules scanned",
                $"{DisplayFormatting.Count(constant.ModulesScanned)} of {DisplayFormatting.Count(constant.ModuleCount)}",
                "The uniqueness claim spans the module instances whose complete metadata was exactly readable."));
        }

        if (constant.Status == ConstantExpressionStatus.Invalid)
        {
            return new EvaluationReport
            {
                Expression = expression,
                Path = "Constant expression",
                Severity = EvaluationSeverity.Stopped,
                Status = "Blocked",
                Stage = "The expression is constant-shaped but has no value under C# constant semantics",
                Value = "No value was produced.",
                Facts = facts.ToImmutable(),
                Diagnostics = [new DiagnosticRow(constant.DiagnosticCode!, constant.DiagnosticMessage!)],
                Duration = duration,
                Sha256 = constant.Sha256,
            };
        }

        var (value, valueKind) = constant.Kind switch
        {
            ConstantValueKind.EnumMember => (
                $"{ShortTypeName(constant.EnumTypeFullName!)}.{constant.EnumMemberName} "
                + $"({constant.Int32Value!.Value.ToString(CultureInfo.InvariantCulture)})",
                $"Enum {constant.EnumTypeFullName} · underlying {constant.UnderlyingTypeName}"),
            ConstantValueKind.String => (
                DisplayFormatting.QuotedString(constant.StringValue!),
                $"String constant (length {DisplayFormatting.Count(constant.StringValue!.Length)})"),
            ConstantValueKind.Char => (
                $"'{DisplayFormatting.QuotedString(constant.CharValue!.Value.ToString())[1..^1]}'",
                "Char constant"),
            ConstantValueKind.Boolean => (
                constant.BooleanValue!.Value ? "true" : "false",
                "Boolean constant"),
            ConstantValueKind.Numeric => (
                constant.ValueText!,
                $"{constant.ValueTypeName} constant"),
            ConstantValueKind.Null => (
                "null",
                "Exactly null"),
            ConstantValueKind.Sequence => (
                constant.ValueText!,
                $"{constant.ValueTypeName} · virtual array "
                + $"(length {constant.Int32Value!.Value.ToString(CultureInfo.InvariantCulture)})"),
            _ => (
                constant.Int32Value!.Value.ToString(CultureInfo.InvariantCulture),
                isLiteralField
                    ? $"Int32 constant literal field ({constant.UnderlyingTypeName})"
                    : "Int32 · checked constant folding"),
        };
        if (!isLiteralField && constant.MetadataLiteralsConsumed > 0)
        {
            facts.Add(new PropertyRow(
                "Constant",
                "Metadata literals consumed",
                DisplayFormatting.Count(constant.MetadataLiteralsConsumed),
                "Qualified names inside the expression resolved as literal fields from module metadata."));
        }

        if (constant.DumpValuesConsumed > 0)
        {
            facts.Add(new PropertyRow(
                "Constant",
                "Dump values consumed",
                DisplayFormatting.Count(constant.DumpValuesConsumed),
                "Names inside the expression resolved to exact values through the frozen dump pipelines."));
        }

        return new EvaluationReport
        {
            Expression = expression,
            Path = "Constant expression",
            Severity = EvaluationSeverity.Exact,
            Status = "Exact",
            Stage = isLiteralField
                ? "Literal read from complete module metadata"
                : constant.DumpValuesConsumed > 0
                    ? "Folded with exact dump-value operands"
                    : constant.MetadataLiteralsConsumed > 0
                        ? "Folded with metadata literal operands"
                        : "Folded without dump evidence",
            Value = value,
            ValueKind = valueKind,
            Facts = facts.ToImmutable(),
            Duration = duration,
            Sha256 = constant.Sha256,
        };
    }

    private static string ShortTypeName(string fullName)
    {
        var separator = fullName.LastIndexOf('.');
        return separator >= 0 ? fullName[(separator + 1)..] : fullName;
    }

    private static EvaluationReport BuildStaticFieldReport(
        string expression,
        DumpSelectedFrameSelector? contextSelector,
        StaticFieldExpressionEvaluationResult result,
        TimeSpan duration)
    {
        var facts = ImmutableArray.CreateBuilder<PropertyRow>();
        var reads = ImmutableArray.CreateBuilder<MemoryReadRow>();
        var bounds = ImmutableArray.CreateBuilder<BoundRow>();
        var diagnostics = ImmutableArray.CreateBuilder<DiagnosticRow>();

        facts.Add(new PropertyRow("Routing", "Entry point", "StaticFieldExpressionEvaluator"));
        facts.Add(new PropertyRow(
            "Routing",
            "Name context",
            contextSelector is null
                ? "Not consulted (fully qualified names only)"
                : $"Thread #{contextSelector.ThreadOrdinal}, frame #{contextSelector.FrameOrdinal}"));
        facts.Add(new PropertyRow("Routing", "Suffix", DisplayFormatting.Humanize(result.SuffixKind.ToString())));

        facts.Add(new PropertyRow("Syntax", "Status", result.Syntax.Status.ToString()));
        facts.Add(new PropertyRow("Syntax", "Issue", result.Syntax.Issue.ToString()));
        if (result.Syntax.Descriptor is { } descriptor)
        {
            facts.Add(new PropertyRow(
                "Syntax",
                "Access segments",
                DisplayFormatting.Count(descriptor.Segments.Length),
                descriptor.HasGlobalQualifier ? "The expression carries an explicit global:: qualifier." : null));
            facts.Add(new PropertyRow(
                "Syntax",
                "Candidate shapes",
                DisplayFormatting.Count(descriptor.CandidateShapes.Length),
                "Distinct type-name/field-name splits the binder must search before it may select one declaration."));
            facts.Add(new PropertyRow("Syntax", "Expression digest", descriptor.Sha256));
        }

        AddBounds(bounds, result.Syntax.ReachedBounds);
        AddDiagnostic(diagnostics, result.Syntax.DiagnosticCode, result.Syntax.DiagnosticMessage);

        if (result.SymbolBinding is { } binding)
        {
            facts.Add(new PropertyRow("Symbol binding", "Status", binding.Status.ToString()));
            facts.Add(new PropertyRow("Symbol binding", "Issue", binding.Issue.ToString()));
            facts.Add(new PropertyRow(
                "Symbol binding",
                "Module catalog exhaustive",
                binding.ModuleCatalogExhaustive ? "Yes" : "No"));
            facts.Add(new PropertyRow(
                "Symbol binding",
                "Name expansions attempted",
                DisplayFormatting.Count(binding.Expansions.Length)));
            facts.Add(new PropertyRow(
                "Symbol binding",
                "Located candidates",
                DisplayFormatting.Count(binding.Candidates.Length)));
            facts.Add(new PropertyRow(
                "Symbol binding",
                "Rejected declarations",
                DisplayFormatting.Count(binding.RejectedDeclarations.Length)));
            facts.Add(new PropertyRow("Symbol binding", "Consulted-context digest", binding.ConsultedContextSha256));
            AddDiagnostic(diagnostics, binding.DiagnosticCode, binding.DiagnosticMessage);

            if (binding.SelectedDeclaration is { } declaration)
            {
                facts.Add(new PropertyRow(
                    "Selected declaration",
                    "Declaring type",
                    declaration.DeclaringTypeName));
                facts.Add(new PropertyRow("Selected declaration", "Field", declaration.FieldDefinition.Name));
                facts.Add(new PropertyRow(
                    "Selected declaration",
                    "TypeDef token",
                    DisplayFormatting.Token(declaration.TypeDefinitionToken)));
                facts.Add(new PropertyRow(
                    "Selected declaration",
                    "FieldDef token",
                    DisplayFormatting.Token(declaration.FieldDefinition.FieldDefinitionToken)));
                facts.Add(new PropertyRow(
                    "Selected declaration",
                    "Thread static",
                    declaration.FieldDefinition.IsThreadStatic ? "Yes" : "No"));
                facts.Add(new PropertyRow(
                    "Selected declaration",
                    "Module MVID",
                    declaration.ModuleContent.Mvid.ToString("D", CultureInfo.InvariantCulture)));
            }
        }

        if (result.RuntimeDeclaration is not null || result.RuntimeIssue is not null)
        {
            facts.Add(new PropertyRow(
                "Runtime correlation",
                "Declaration mapped",
                result.RuntimeDeclaration is null ? "No" : "Yes"));
            if (result.RuntimeIssue is { } runtimeIssue)
            {
                facts.Add(new PropertyRow("Runtime correlation", "Issue", runtimeIssue.ToString()));
            }
        }

        AddReads(reads, result.RuntimeDeclarationEvidence);
        AddReads(reads, result.NullableLayoutEvidence);
        AddBounds(bounds, result.RuntimeDeclarationBounds);
        AddBounds(bounds, result.NullableLayoutBounds);

        if (result.HostObservation is { } host)
        {
            facts.Add(new PropertyRow("Storage read", "Status", host.Status.ToString()));
            facts.Add(new PropertyRow("Storage read", "Issue", host.Issue.ToString()));
            facts.Add(new PropertyRow("Storage read", "Slot address", DisplayFormatting.Address(host.SlotAddress ?? 0)));
            if (host.Request is { } request)
            {
                facts.Add(new PropertyRow(
                    "Storage read",
                    "Decoder",
                    DisplayFormatting.Humanize(request.ValueShape.ToString())));
            }

            AddRawReads(reads, host.Reads);
            AddBounds(bounds, host.ReachedBounds);
        }

        if (result.ObjectBinding is { } objectBinding)
        {
            facts.Add(new PropertyRow(
                "Object binding",
                "Address",
                DisplayFormatting.Address(objectBinding.Identity.Address)));
            facts.Add(new PropertyRow("Object binding", "Runtime type", objectBinding.Identity.TypeName));
            facts.Add(new PropertyRow(
                "Object binding",
                "Method table",
                DisplayFormatting.Address(objectBinding.Identity.MethodTable)));
            facts.Add(new PropertyRow(
                "Object binding",
                "Provenance",
                DisplayFormatting.Humanize(objectBinding.Provenance.Kind.ToString()),
                "This binding can be used as the root of a root-relative expression."));
        }

        var suffix = result.SuffixResult;
        if (suffix is not null)
        {
            AppendDerivedQueryFacts(facts, suffix, "Suffix evaluation");
            AddProvenance(reads, suffix.Provenance);
            AddBounds(bounds, suffix.Context.Bounds);
            foreach (var diagnostic in suffix.Diagnostics)
            {
                diagnostics.Add(new DiagnosticRow(diagnostic.Code, diagnostic.Message));
            }
        }

        AddDiagnostic(diagnostics, result.DiagnosticCode, result.DiagnosticMessage);

        var (value, valueKind) = suffix is not null
            ? RenderDumpQueryValue(suffix)
            : RenderStaticFieldValue(result.HostObservation?.Value);

        return new EvaluationReport
        {
            Expression = expression,
            Path = "Static field expression",
            Severity = MapSeverity(result.Status),
            Status = DisplayFormatting.Humanize(result.Status.ToString()),
            Stage = DisplayFormatting.Humanize(result.Stage.ToString()),
            Value = value,
            ValueKind = valueKind,
            Sha256 = result.Sha256,
            Facts = facts.ToImmutable(),
            MemoryReads = reads.ToImmutable(),
            Bounds = Normalize(bounds),
            Diagnostics = Normalize(diagnostics),
            Duration = duration,
        };
    }

    private static EvaluationReport BuildRootRelativeReport(
        string expression,
        DumpQueryRootBinding rootBinding,
        DumpExpressionLanguageProfile languageProfile,
        DumpExpressionEvaluationOutcome outcome,
        TimeSpan duration)
    {
        var facts = ImmutableArray.CreateBuilder<PropertyRow>();
        var reads = ImmutableArray.CreateBuilder<MemoryReadRow>();
        var bounds = ImmutableArray.CreateBuilder<BoundRow>();
        var diagnostics = ImmutableArray.CreateBuilder<DiagnosticRow>();

        facts.Add(new PropertyRow("Routing", "Entry point", "DumpExpressionEvaluator"));
        facts.Add(new PropertyRow("Routing", "Language profile", DisplayFormatting.Humanize(languageProfile.ToString())));
        facts.Add(new PropertyRow("Routing", "Outcome kind", DisplayFormatting.Humanize(outcome.Kind.ToString())));
        facts.Add(new PropertyRow("Root", "Identifier", rootBinding.Name ?? DisplayFormatting.Absent));
        facts.Add(new PropertyRow("Root", "Binding status", DisplayFormatting.Humanize(rootBinding.Status.ToString())));
        if (rootBinding.Root is { } root)
        {
            facts.Add(new PropertyRow("Root", "Runtime type", root.TypeName));
            facts.Add(new PropertyRow("Root", "Address", DisplayFormatting.Address(root.Address)));
            facts.Add(new PropertyRow("Root", "Handle kind", root.RootKind));
        }

        if (outcome.Request is { } request)
        {
            facts.Add(new PropertyRow("Request", "Expression digest", request.Sha256));
        }

        AddReads(reads, rootBinding.Evidence);
        AddBounds(bounds, rootBinding.AppliedBounds);

        string status;
        string stage;
        EvaluationSeverity severity;
        var value = "No value was produced.";
        string? valueKind = null;
        string? sha256 = null;

        switch (outcome.Kind)
        {
            case DumpExpressionEvaluationOutcomeKind.DerivedQuery:
            {
                var derived = outcome.DerivedQueryResult!;
                AppendDerivedQueryFacts(facts, derived, "Derived query");
                AddProvenance(reads, derived.Provenance);
                AddBounds(bounds, derived.Context.Bounds);
                foreach (var diagnostic in derived.Diagnostics)
                {
                    diagnostics.Add(new DiagnosticRow(diagnostic.Code, diagnostic.Message));
                }

                (value, valueKind) = RenderDumpQueryValue(derived);
                status = DisplayFormatting.Humanize(derived.Completion.ToString());
                stage = $"{DisplayFormatting.Humanize(derived.Completeness.ToString())} answer from "
                    + $"{DisplayFormatting.Humanize(derived.Evidence.ToString())} evidence";
                severity = MapSeverity(derived);
                sha256 = EvaluationResultReplay.ComputeSha256(derived, static value => value.ToCanonicalReplayProjection());
                break;
            }

            case DumpExpressionEvaluationOutcomeKind.CounterfactualExecution:
            {
                var execution = outcome.CounterfactualExecutionResult!;
                facts.Add(new PropertyRow("Execution", "Semantic mode", DisplayFormatting.Humanize(execution.SemanticMode.ToString())));
                facts.Add(new PropertyRow("Execution", "Completion", DisplayFormatting.Humanize(execution.Completion.ToString())));
                facts.Add(new PropertyRow("Execution", "Completeness", DisplayFormatting.Humanize(execution.Completeness.ToString())));
                facts.Add(new PropertyRow("Execution", "Evidence", DisplayFormatting.Humanize(execution.Evidence.ToString())));
                facts.Add(new PropertyRow("Execution", "Effects", DisplayFormatting.Humanize(execution.Effects.ToString())));
                facts.Add(new PropertyRow("Execution", "Deterministic replay", execution.IsDeterministicReplay ? "Yes" : "No"));
                facts.Add(new PropertyRow("Execution", "Origin", DisplayFormatting.Humanize(execution.Context.Origin.ToString())));
                facts.Add(new PropertyRow(
                    "Execution",
                    "Interpreted methods",
                    DisplayFormatting.Count(execution.Context.InterpretedMethods.Length)));
                facts.Add(new PropertyRow(
                    "Execution",
                    "Modeled methods",
                    DisplayFormatting.Count(execution.Context.ModeledMethods.Length)));

                var accounting = execution.Context.Accounting;
                AddOptionalCount(facts, "Accounting", "Instructions used", accounting.InstructionUsed);
                AddOptionalCount(facts, "Accounting", "Instruction limit", accounting.InstructionLimit);
                AddOptionalCount(facts, "Accounting", "Traversal used", accounting.TraversalUsed);
                AddOptionalCount(facts, "Accounting", "Traversal limit", accounting.TraversalLimit);
                AddOptionalCount(facts, "Accounting", "Logical depth required", accounting.RequiredLogicalDepth);
                AddOptionalCount(facts, "Accounting", "Frame depth high water", accounting.ActiveFrameDepthHighWater);

                AddProvenance(reads, execution.Provenance);
                AddBounds(bounds, execution.Context.EvidenceContext.Bounds);
                foreach (var diagnostic in execution.Diagnostics)
                {
                    diagnostics.Add(new DiagnosticRow(diagnostic.Code, diagnostic.Message));
                }

                (value, valueKind) = RenderCounterfactualValue(execution.Value);
                status = DisplayFormatting.Humanize(execution.Completion.ToString());
                stage = $"{DisplayFormatting.Humanize(execution.Completeness.ToString())} answer from "
                    + $"{DisplayFormatting.Humanize(execution.Evidence.ToString())} evidence";
                severity = MapSeverity(execution.Completion, execution.Completeness);
                sha256 = execution.Sha256;
                break;
            }

            case DumpExpressionEvaluationOutcomeKind.ClassificationFailure:
            {
                var classification = outcome.ClassificationFailure!;
                facts.Add(new PropertyRow("Classification", "Status", DisplayFormatting.Humanize(classification.Status.ToString())));
                facts.Add(new PropertyRow(
                    "Classification",
                    "Admitted kind",
                    classification.Kind is { } kind ? DisplayFormatting.Humanize(kind.ToString()) : DisplayFormatting.Absent));
                AddDiagnostic(diagnostics, classification.DiagnosticCode, classification.DiagnosticMessage);
                status = DisplayFormatting.Humanize(classification.Status.ToString());
                stage = "Rejected before any evidence was bound";
                severity = EvaluationSeverity.Rejected;
                break;
            }

            case DumpExpressionEvaluationOutcomeKind.AcquisitionFailure:
            {
                var failure = outcome.AcquisitionFailure!;
                facts.Add(new PropertyRow("Acquisition", "Kind", DisplayFormatting.Humanize(failure.Kind.ToString())));
                if (failure.EvidenceStatus is { } evidenceStatus)
                {
                    facts.Add(new PropertyRow("Acquisition", "Evidence status", evidenceStatus.ToString()));
                }

                if (failure.EvidenceIssue is { } evidenceIssue)
                {
                    facts.Add(new PropertyRow("Acquisition", "Evidence issue", evidenceIssue.ToString()));
                }

                diagnostics.Add(new DiagnosticRow(failure.Code, failure.Message));
                status = DisplayFormatting.Humanize(failure.Kind.ToString());
                stage = "Method evidence acquisition stopped before execution preparation";
                severity = failure.Kind == DumpMethodAcquisitionFailureKind.Partial
                    ? EvaluationSeverity.Partial
                    : EvaluationSeverity.Stopped;
                break;
            }

            default:
            {
                var failure = outcome.CounterfactualPreparationFailure!;
                facts.Add(new PropertyRow("Preparation", "Completion", DisplayFormatting.Humanize(failure.Completion.ToString())));
                facts.Add(new PropertyRow("Preparation", "Evidence", DisplayFormatting.Humanize(failure.Evidence.ToString())));
                facts.Add(new PropertyRow("Preparation", "Effects", DisplayFormatting.Humanize(failure.Effects.ToString())));
                facts.Add(new PropertyRow(
                    "Preparation",
                    "Request digest",
                    failure.RequestSha256 ?? DisplayFormatting.Absent));
                AddProvenance(reads, failure.Provenance);
                AddBounds(bounds, failure.Context.Bounds);
                foreach (var diagnostic in failure.Diagnostics)
                {
                    diagnostics.Add(new DiagnosticRow(diagnostic.Code, diagnostic.Message));
                }

                status = DisplayFormatting.Humanize(failure.Completion.ToString());
                stage = "Execution preparation produced a value-free failure";
                severity = EvaluationSeverity.Stopped;
                break;
            }
        }

        return new EvaluationReport
        {
            Expression = expression,
            Path = "Root-relative expression",
            Severity = severity,
            Status = status,
            Stage = stage,
            Value = value,
            ValueKind = valueKind,
            Sha256 = sha256,
            Facts = facts.ToImmutable(),
            MemoryReads = reads.ToImmutable(),
            Bounds = Normalize(bounds),
            Diagnostics = Normalize(diagnostics),
            Duration = duration,
        };
    }

    private static void AppendDerivedQueryFacts(
        ImmutableArray<PropertyRow>.Builder facts,
        EvaluationResult<DumpQueryValue> result,
        string group)
    {
        facts.Add(new PropertyRow(group, "Semantic mode", DisplayFormatting.Humanize(result.SemanticMode.ToString())));
        facts.Add(new PropertyRow(group, "Completion", DisplayFormatting.Humanize(result.Completion.ToString())));
        facts.Add(new PropertyRow(group, "Completeness", DisplayFormatting.Humanize(result.Completeness.ToString())));
        facts.Add(new PropertyRow(group, "Evidence", DisplayFormatting.Humanize(result.Evidence.ToString())));
        facts.Add(new PropertyRow(group, "Effects", DisplayFormatting.Humanize(result.Effects.ToString())));
        facts.Add(new PropertyRow(
            group,
            "Evidence source",
            DisplayFormatting.Humanize(result.Context.SourceKind.ToString())));
        facts.Add(new PropertyRow(
            group,
            "Fallback",
            result.Context.Fallback.Status == EvaluationFallbackStatus.None
                ? "None applied"
                : $"{DisplayFormatting.Humanize(result.Context.Fallback.Status.ToString())}: {result.Context.Fallback.Name}"));
    }

    private static (string Value, string? Kind) RenderDumpQueryValue(EvaluationResult<DumpQueryValue> result)
    {
        if (result.Value is not { } value)
        {
            return ("No value was produced.", null);
        }

        var kind = value.Kind.ToString();
        return value.Kind switch
        {
            DumpQueryValueKind.Null => ("null", kind),
            DumpQueryValueKind.Int32 => (
                value.Int32Value!.Value.ToString(CultureInfo.InvariantCulture),
                kind),
            _ => (
                DisplayFormatting.QuotedString(value.StringValue!),
                $"{kind} (length {DisplayFormatting.Count(value.StringValue!.Length)})"),
        };
    }

    private static (string Value, string? Kind) RenderStaticFieldValue(ClrmdStaticFieldValue? value)
    {
        if (value is null)
        {
            return ("No value was produced.", null);
        }

        var kind = DisplayFormatting.Humanize(value.Kind.ToString());
        return value.Kind switch
        {
            ClrmdStaticFieldTerminalKind.Null => ("null", kind),
            ClrmdStaticFieldTerminalKind.Int32 => (
                value.Int32Value!.Value.ToString(CultureInfo.InvariantCulture),
                kind),
            ClrmdStaticFieldTerminalKind.NullableInt32NoValue => ("null", $"{kind} (HasValue = false)"),
            ClrmdStaticFieldTerminalKind.NullableInt32Value => (
                value.Int32Value!.Value.ToString(CultureInfo.InvariantCulture),
                $"{kind} (HasValue = true)"),
            ClrmdStaticFieldTerminalKind.String => (
                DisplayFormatting.QuotedString(value.StringValue!.Value),
                $"{kind} (length {DisplayFormatting.Count(value.StringValue!.Value.Length)})"),
            _ => (
                $"{value.ObjectReference!.HeaderRuntimeType.FullName} @ "
                + DisplayFormatting.Address(value.ObjectReference!.Address),
                kind),
        };
    }

    private static (string Value, string? Kind) RenderCounterfactualValue(CounterfactualExecutionValue? value)
    {
        if (value is null)
        {
            return ("No value was produced.", null);
        }

        var kind = DisplayFormatting.Humanize(value.Kind.ToString());
        return value.Kind switch
        {
            CounterfactualExecutionValueKind.ExactReturn => (
                value.ExactInt32!.Value.ToString(CultureInfo.InvariantCulture),
                kind),
            CounterfactualExecutionValueKind.UnknownReturn => (
                "unknown (explained)",
                kind),
            CounterfactualExecutionValueKind.TargetException => (
                "terminated at a certified target-exception boundary",
                kind),
            _ => ("a nonempty execution prefix ran; partial machine state is not a product value", kind),
        };
    }

    private static EvaluationSeverity MapSeverity(StaticFieldExpressionEvaluationStatus status) => status switch
    {
        StaticFieldExpressionEvaluationStatus.Exact => EvaluationSeverity.Exact,
        StaticFieldExpressionEvaluationStatus.Absent => EvaluationSeverity.Absent,
        StaticFieldExpressionEvaluationStatus.Partial => EvaluationSeverity.Partial,
        _ => EvaluationSeverity.Stopped,
    };

    private static EvaluationSeverity MapSeverity(EvaluationResult<DumpQueryValue> result) =>
        MapSeverity(result.Completion, result.Completeness);

    private static EvaluationSeverity MapSeverity(
        EvaluationCompletionStatus completion,
        EvaluationCompleteness completeness)
    {
        if (completion != EvaluationCompletionStatus.Completed)
        {
            return EvaluationSeverity.Stopped;
        }

        return completeness switch
        {
            EvaluationCompleteness.Complete => EvaluationSeverity.Exact,
            EvaluationCompleteness.Partial => EvaluationSeverity.Partial,
            _ => EvaluationSeverity.Stopped,
        };
    }

    private static void AddOptionalCount(
        ImmutableArray<PropertyRow>.Builder facts,
        string group,
        string name,
        long? value)
    {
        if (value is { } present)
        {
            facts.Add(new PropertyRow(group, name, DisplayFormatting.Count(present)));
        }
    }

    private static void AddDiagnostic(
        ImmutableArray<DiagnosticRow>.Builder diagnostics,
        string? code,
        string? message)
    {
        if (!string.IsNullOrEmpty(code) || !string.IsNullOrEmpty(message))
        {
            diagnostics.Add(new DiagnosticRow(code ?? DisplayFormatting.Absent, message ?? string.Empty));
        }
    }

    private static void AddReads(
        ImmutableArray<MemoryReadRow>.Builder reads,
        ImmutableArray<MemoryReadResult> evidence)
    {
        if (evidence.IsDefaultOrEmpty)
        {
            return;
        }

        foreach (var read in evidence)
        {
            reads.Add(new MemoryReadRow(
                DisplayFormatting.Address(read.Address),
                DisplayFormatting.Count(read.RequestedLength),
                DisplayFormatting.Count(read.BytesRead),
                read.Status.ToString(),
                DisplayFormatting.HexPreview(read.Bytes)));
        }
    }

    private static void AddRawReads(
        ImmutableArray<MemoryReadRow>.Builder reads,
        ImmutableArray<ClrmdRawMemoryEvidence> evidence)
    {
        if (evidence.IsDefaultOrEmpty)
        {
            return;
        }

        foreach (var read in evidence)
        {
            var bytes = read.Bytes;
            reads.Add(new MemoryReadRow(
                DisplayFormatting.Address(read.Address),
                DisplayFormatting.Count(read.RequestedLength),
                DisplayFormatting.Count(bytes.Length),
                read.Status.ToString(),
                DisplayFormatting.HexPreview(bytes)));
        }
    }

    private static void AddProvenance(
        ImmutableArray<MemoryReadRow>.Builder reads,
        ImmutableArray<EvaluationProvenance> provenance)
    {
        if (provenance.IsDefaultOrEmpty)
        {
            return;
        }

        foreach (var entry in provenance)
        {
            reads.Add(new MemoryReadRow(
                entry.Address is { } address ? DisplayFormatting.Address(address) : DisplayFormatting.Absent,
                entry.RequestedLength is { } requested ? DisplayFormatting.Count(requested) : DisplayFormatting.Absent,
                entry.ObservedLength is { } observed ? DisplayFormatting.Count(observed) : DisplayFormatting.Absent,
                DisplayFormatting.Humanize(entry.Kind.ToString()),
                entry.SourceId));
        }
    }

    private static void AddBounds(
        ImmutableArray<BoundRow>.Builder bounds,
        ImmutableArray<EvaluationDeterministicBound> applied)
    {
        if (applied.IsDefaultOrEmpty)
        {
            return;
        }

        foreach (var bound in applied)
        {
            bounds.Add(new BoundRow(bound.Name, DisplayFormatting.Count(bound.Value)));
        }
    }

    private static ImmutableArray<DiagnosticRow> Normalize(ImmutableArray<DiagnosticRow>.Builder diagnostics) =>
        diagnostics
            .DistinctBy(static diagnostic => (diagnostic.Code, diagnostic.Message))
            .ToImmutableArray();

    private static ImmutableArray<BoundRow> Normalize(ImmutableArray<BoundRow>.Builder bounds) =>
        bounds
            .DistinctBy(static bound => bound.Name, StringComparer.Ordinal)
            .OrderBy(static bound => bound.Name, StringComparer.Ordinal)
            .ToImmutableArray();
}
