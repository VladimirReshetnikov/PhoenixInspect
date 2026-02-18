using System.Collections.Immutable;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Interpreter.Core.Abstractions;
using Interpreter.Core.Execution;
using Interpreter.Host.Dump.ClrMD;
using Interpreter.Metadata.SRM;
using Microsoft.Diagnostics.NETCore.Client;
using Xunit;

namespace Interpreter.IntegrationTests;

/// <summary>
/// End-to-end integration coverage for dump loading, metadata resolution, and one-step ret execution.
/// </summary>
public sealed class RetOnlyDumpIntegrationTests
{
    /// <summary>
    /// Verifies the minimal ret-only integration seam from dump generation through VM execution.
    /// </summary>
    [Fact]
    public void RetOnly_dump_flow_loads_module_reads_il_and_completes_after_one_step()
    {
        var targetExecutablePath = ResolveTargetExecutablePath();
        var targetAssemblyPath = ResolveTargetAssemblyPath(targetExecutablePath);
        Assert.True(File.Exists(targetExecutablePath), $"Expected test target executable at '{targetExecutablePath}'.");
        Assert.True(File.Exists(targetAssemblyPath), $"Expected test target assembly at '{targetAssemblyPath}'.");

        using var verificationModule = SrmMetadataModule.LoadFromFile(targetAssemblyPath);
        Assert.True(verificationModule.TryFindMethodDefToken("Program", "RetOnly", out var verificationToken),
            "Could not resolve Program.RetOnly in Interpreter.TestTarget metadata.");

        var emptyGenericContext = new Interpreter.Types.GenericContext(Array.Empty<Interpreter.Types.TypeSig>(), Array.Empty<Interpreter.Types.TypeSig>());
        var verificationMethodHandle = verificationModule.GetMethodHandle(verificationToken, emptyGenericContext);
        Assert.True(verificationModule.TryGetMethodBody(verificationMethodHandle, out var verificationBody),
            "Could not read method body for Program.RetOnly during pre-dump verification.");

        var verificationBytes = verificationBody.CodeBytes.ToArray();
        Assert.True(verificationBytes.SequenceEqual(new byte[] { 0x2A }),
            $"RetOnly expected [2A], got [{FormatBytes(verificationBytes)}].");

        var dumpPath = Path.Combine(Path.GetTempPath(), $"ret-only-{Guid.NewGuid():N}.dmp");
        try
        {
            using var targetRunner = TestTargetRunner.StartAndWaitReady(targetExecutablePath);
            DumpWriter.WriteFullDump(targetRunner.Pid, dumpPath);

            using var dumpSession = ClrmdDumpSession.Load(dumpPath);
            Assert.True(dumpSession.TryFindModuleByFileName("Interpreter.TestTarget.dll", out var runtimeModule),
                "ClrMD runtime module enumeration did not include Interpreter.TestTarget.dll.");
            Assert.False(string.IsNullOrWhiteSpace(runtimeModule.FilePath), "ClrMD module path was null/empty.");

            using var runtimeModuleMetadata = SrmMetadataModule.LoadFromFile(runtimeModule.FilePath!);
            Assert.Equal(verificationModule.Id.Mvid, runtimeModuleMetadata.Id.Mvid);
            Assert.True(runtimeModuleMetadata.TryFindMethodDefToken("Program", "RetOnly", out var methodToken),
                "Could not resolve Program.RetOnly from runtime module metadata.");

            var methodHandle = runtimeModuleMetadata.GetMethodHandle(methodToken, emptyGenericContext);
            Assert.True(runtimeModuleMetadata.TryGetMethodBody(methodHandle, out var methodBody),
                "Could not read IL body for Program.RetOnly from runtime module metadata.");

            var ilBytes = methodBody.CodeBytes.ToArray();
            Assert.True(ilBytes.SequenceEqual(new byte[] { 0x2A }),
                $"Runtime module RetOnly expected [2A], got [{FormatBytes(ilBytes)}].");

            var machine = new IlMachine<UnitValue, UnitMemory>(
                new MetadataResolutionServices(runtimeModuleMetadata),
                new InstructionBudgetPolicy());

            var state = new MachineState<UnitValue, UnitMemory>(
                ImmutableArray.Create(new FrameState<UnitValue>(methodHandle, 0, ImmutableArray<UnitValue>.Empty)),
                default,
                new BudgetState(InstructionBudget: 4, AllocationBudget: 0, MaxCallDepth: 1, MaxForks: 0));

            var outcome = machine.StepOne(state);
            Assert.Equal(StopReason.Completed, outcome.StopReason);
            Assert.Empty(outcome.State.CallStack);
            Assert.Equal(2, outcome.Events.Length);
            Assert.Equal(DebugEventKind.InstructionExecuted, outcome.Events[0].Kind);
            Assert.Equal(DebugEventKind.FramePopped, outcome.Events[1].Kind);
        }
        finally
        {
            if (File.Exists(dumpPath))
            {
                File.Delete(dumpPath);
            }
        }
    }

    private static string ResolveTargetExecutablePath()
    {
        var repositoryRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));
        var configuration = ResolveBuildConfiguration();
        var executableFileName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "Interpreter.TestTarget.exe"
            : "Interpreter.TestTarget";

        return Path.Combine(repositoryRoot, "Interpreter.TestTarget", "bin", configuration, "net8.0", executableFileName);
    }

    private static string ResolveTargetAssemblyPath(string executablePath)
    {
        var directory = Path.GetDirectoryName(executablePath)
            ?? throw new InvalidOperationException("Could not determine target executable directory.");
        return Path.Combine(directory, "Interpreter.TestTarget.dll");
    }

    private static string ResolveBuildConfiguration()
    {
        var baseDirectory = new DirectoryInfo(AppContext.BaseDirectory);
        while (baseDirectory is not null)
        {
            if (string.Equals(baseDirectory.Name, "Debug", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(baseDirectory.Name, "Release", StringComparison.OrdinalIgnoreCase))
            {
                return baseDirectory.Name;
            }

            baseDirectory = baseDirectory.Parent;
        }

        return "Debug";
    }

    private static string FormatBytes(IReadOnlyList<byte> bytes)
    {
        var builder = new StringBuilder();
        for (var i = 0; i < bytes.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(' ');
            }

            builder.Append(bytes[i].ToString("X2"));
        }

        return builder.ToString();
    }

    private readonly record struct UnitValue;

    private readonly record struct UnitMemory;

    private sealed class TestTargetRunner : IDisposable
    {
        private readonly Process _process;

        private TestTargetRunner(Process process)
        {
            _process = process;
        }

        public int Pid => _process.Id;

        public static TestTargetRunner StartAndWaitReady(string executablePath)
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = executablePath,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                },
            };

            if (!process.Start())
            {
                throw new InvalidOperationException($"Failed to start test target process '{executablePath}'.");
            }

            var readLineTask = Task.Run(() => process.StandardOutput.ReadLine());
            if (readLineTask.Wait(TimeSpan.FromSeconds(20)))
            {
                var line = readLineTask.Result;
                if (string.Equals(line?.Trim(), "READY", StringComparison.Ordinal))
                {
                    return new TestTargetRunner(process);
                }
            }

            var stderr = process.StandardError.ReadToEnd();
            try
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(5000);
            }
            catch
            {
                // Best-effort cleanup.
            }

            throw new InvalidOperationException($"Timed out waiting for READY from target process. Stderr: {stderr}");
        }

        public void Dispose()
        {
            if (_process.HasExited)
            {
                _process.Dispose();
                return;
            }

            try
            {
                _process.Kill(entireProcessTree: true);
                _process.WaitForExit(5000);
            }
            finally
            {
                _process.Dispose();
            }
        }
    }

    private static class DumpWriter
    {
        public static void WriteFullDump(int pid, string dumpPath)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(dumpPath)!);
            var diagnosticsClient = new DiagnosticsClient(pid);
            diagnosticsClient.WriteDump(DumpType.Full, dumpPath, logDumpGeneration: false);
        }
    }
}
