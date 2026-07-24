using System.Runtime.InteropServices;

namespace PhoenixInspect.IntegrationTests;

/// <summary>
/// Resolves the optimized W8 compiler-fixture artifacts produced alongside the integration test build.
/// </summary>
/// <remarks>
/// These paths identify draft physical-oracle artifacts. They are not product discovery rules.
/// </remarks>
internal static class W8TestTargetPaths
{
    internal const string AssemblyFileName = "PhoenixInspect.W8TestTarget.dll";
    internal const string AliasAssemblyFileName = "PhoenixInspect.W8AliasTarget.dll";
    internal const string ForwarderAssemblyFileName = "PhoenixInspect.W8ForwarderTarget.dll";
    internal const string NamedRvaAssemblyFileName = "PhoenixInspect.W8NamedRvaTarget.dll";
    internal const string PortablePdbFileName = "PhoenixInspect.W8TestTarget.pdb";

    internal static string ResolveExecutable()
    {
        var executableName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "PhoenixInspect.W8TestTarget.exe"
            : "PhoenixInspect.W8TestTarget";
        return Path.Combine(ResolveOutputDirectory(), executableName);
    }

    internal static string ResolveAssembly() =>
        Path.Combine(ResolveOutputDirectory(), AssemblyFileName);

    internal static string ResolveAliasAssembly() =>
        Path.Combine(ResolveOutputDirectory(), AliasAssemblyFileName);

    internal static string ResolveForwarderAssembly() =>
        Path.Combine(ResolveOutputDirectory(), ForwarderAssemblyFileName);

    internal static string ResolveNamedRvaAssembly() =>
        Path.Combine(ResolveOutputDirectory(), NamedRvaAssemblyFileName);

    internal static string ResolvePortablePdb() =>
        Path.Combine(ResolveOutputDirectory(), PortablePdbFileName);

    private static string ResolveOutputDirectory()
    {
        var testsRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));
        var targetFramework = new DirectoryInfo(AppContext.BaseDirectory).Name;
        return Path.Combine(
            testsRoot,
            "PhoenixInspect.W8TestTarget",
            "bin",
            "Release",
            targetFramework);
    }
}
