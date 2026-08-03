using PhoenixInspect.Host.Dump.ClrMD;
using PhoenixInspect.Product.DumpQuery;
using Xunit;

namespace PhoenixInspect.IntegrationTests;

/// <summary>
/// Proves the enum surface against enum declarations read from a real dump's module metadata: a cast names the
/// value through the declaration, <c>typeof</c> resolves the declaration as an enum, and the <c>System.Enum</c>
/// API answers from the counted metadata rather than from analysis-machine reflection.
/// </summary>
public sealed class EnumMetadataIntegrationTests
{
    /// <summary>Resolves <c>System.ConsoleColor</c> from the dump's own System.Console metadata.</summary>
    [Fact]
    [Trait("Category", "Dump")]
    [Trait("Corpus", "EnumMetadataV1")]
    public void Dump_declared_enums_answer_casts_typeof_and_the_enum_api()
    {
        var executable = PreviewDemoPaths.ResolveExecutable();
        Assert.True(File.Exists(executable), $"Expected the demo target at '{executable}'.");
        var dumpPath = Path.Combine(Path.GetTempPath(), $"enum-metadata-{Guid.NewGuid():N}.dmp");
        try
        {
            using (var target = TestTargetRunner.StartAndWaitReady(executable, [], isolatedDirectory: null))
            {
                DumpWriter.WriteFullDump(target.Pid, dumpPath);
            }

            var opened = ClrmdDumpSession.Open(dumpPath);
            Assert.Equal(ClrmdEvidenceStatus.Exact, opened.Status);
            using var session = opened.Value!;

            // ConsoleColor is not in the evaluator's closed BCL table, so every answer below comes from the
            // dump module's counted metadata.
            var cast = ConstantExpressionEvaluator.Evaluate(session, "(System.ConsoleColor)10");
            Assert.Equal(ConstantExpressionStatus.Exact, cast.Status);
            Assert.Equal(ConstantValueKind.EnumMember, cast.Kind);
            Assert.Equal("Green", cast.EnumMemberName);
            Assert.Equal("System.ConsoleColor", cast.EnumTypeFullName);

            var isEnum = ConstantExpressionEvaluator.Evaluate(session, "typeof(System.ConsoleColor).IsEnum");
            Assert.Equal(ConstantExpressionStatus.Exact, isEnum.Status);
            Assert.True(isEnum.BooleanValue);

            var names = ConstantExpressionEvaluator.Evaluate(
                session, "Enum.GetNames(typeof(System.ConsoleColor)).Length");
            Assert.Equal(ConstantExpressionStatus.Exact, names.Status);
            Assert.Equal(16, names.Int32Value);

            var parsed = ConstantExpressionEvaluator.Evaluate(
                session, "Enum.Parse(typeof(System.ConsoleColor), \"DarkBlue\")");
            Assert.Equal(ConstantExpressionStatus.Exact, parsed.Status);
            Assert.Equal("DarkBlue", parsed.EnumMemberName);
            Assert.Equal(1, parsed.Int32Value);

            var defined = ConstantExpressionEvaluator.Evaluate(
                session, "Enum.IsDefined(typeof(System.ConsoleColor), 99)");
            Assert.Equal(ConstantExpressionStatus.Exact, defined.Status);
            Assert.False(defined.BooleanValue);
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
