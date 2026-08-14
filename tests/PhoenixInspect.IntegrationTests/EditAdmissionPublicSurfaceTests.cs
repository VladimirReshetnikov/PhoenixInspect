using System.Reflection;
using PhoenixInspect.Host.Dump.ClrMD;
using PhoenixInspect.Product.DumpDebugging;
using PhoenixInspect.Product.DumpQuery;
using Xunit;

namespace PhoenixInspect.IntegrationTests;

/// <summary>Freezes the supported public session boundary for fail-closed edit-state admission.</summary>
public sealed class EditAdmissionPublicSurfaceTests
{
    /// <summary>Component metadata/composition seams stay internal; supported top-level evaluators stay public.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Only_guarded_top_level_session_evaluators_are_public()
    {
        Assert.Empty(PublicSessionMethods(typeof(StaticFieldFullyQualifiedBinder)));
        Assert.Empty(PublicSessionMethods(typeof(StaticFieldContextualBinder)));
        Assert.Empty(PublicSessionMethods(typeof(StaticFieldRuntimeComposer)));

        Assert.NotEmpty(PublicSessionMethods(typeof(StaticFieldExpressionEvaluator)));
        Assert.NotEmpty(PublicSessionMethods(typeof(ExpressionEvaluator)));
        Assert.NotEmpty(PublicSessionMethods(typeof(DumpQueryEngine)));
        Assert.NotEmpty(PublicSessionMethods(typeof(DumpMemberChainEngine)));
        Assert.NotEmpty(PublicSessionMethods(typeof(DumpExpressionEvaluator)));
        Assert.NotEmpty(PublicSessionMethods(typeof(DumpMemberChainPathEvaluator)));
        Assert.NotEmpty(PublicSessionMethods(typeof(DumpMemberChainPreparationFacade)));
        Assert.NotEmpty(PublicSessionMethods(typeof(DumpMethodAcquisitionFacade)));
    }

    /// <summary>New union values append without moving the frozen result vocabulary.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Admission_union_values_are_append_only()
    {
        Assert.Equal(1, (int)ExpressionEvaluationStatus.NotFolded);
        Assert.Equal(2, (int)ExpressionEvaluationStatus.Exact);
        Assert.Equal(3, (int)ExpressionEvaluationStatus.Invalid);
        Assert.Equal(4, (int)ExpressionEvaluationStatus.Unavailable);
        Assert.Equal(10, (int)StaticFieldExpressionEvaluationStage.EditStateAdmission);
        Assert.Equal(6, (int)DumpExpressionEvaluationOutcomeKind.AdmissionFailure);
        Assert.Equal(9, (int)DumpMethodAcquisitionFailureKind.EditStateAdmission);
    }

    /// <summary>Tagged Product failure unions reject admission kinds without a retained Host refusal.</summary>
    [Fact]
    [Trait("Category", "Fast")]
    public void Admission_failure_tags_require_their_host_payload()
    {
        Assert.Throws<ArgumentException>(() => new DumpMethodAcquisitionFailure(
            DumpMethodAcquisitionFailureKind.EditStateAdmission,
            "DUMP_MODULE_EDIT_STATE_UNAVAILABLE",
            "Unavailable.",
            ClrmdEvidenceStatus.Unavailable,
            ClrmdValueIssue.RuntimeContractUnavailable));

        Assert.Throws<ArgumentException>(() => new DumpMemberChainPreparationFailure(
            "DUMP_MODULE_EDITED_GENERATIONS_NOT_COMPOSED",
            "Edited.",
            ClrmdEvidenceStatus.Exact,
            ClrmdValueIssue.None));
    }

    private static MethodInfo[] PublicSessionMethods(Type type) => type
        .GetMethods(BindingFlags.Public | BindingFlags.Static)
        .Where(method => method.GetParameters().Any(parameter => parameter.ParameterType == typeof(ClrmdDumpSession)))
        .ToArray();
}
