using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Json;
using System.Xml.Linq;
using Avalonia;
using Avalonia.Platform;
using PhoenixInspect.Inspection;

namespace PhoenixInspect.Desktop;

/// <summary>
/// Performs a bounded, non-UI loadability check over the packaged desktop surface. The caller supplies the
/// wall-clock bound; this code deliberately never initializes Avalonia or starts an application lifetime.
/// </summary>
internal static class DesktopSmokeTest
{
    internal const string Argument = "--smoke-test";

    private const string FailurePrefix = "PHOENIXINSPECT_DESKTOP_SMOKE_FAILED";
    private const int FailureExitCode = 70;

    private static readonly string[] ResourcePaths =
    [
        "/App.axaml",
        "/Views/CallStackPaneView.axaml",
        "/Views/EvaluatePaneView.axaml",
        "/Views/HeapSearchPaneView.axaml",
        "/Views/ImmediatePaneView.axaml",
        "/Views/LocalsPaneView.axaml",
        "/Views/MainWindow.axaml",
        "/Views/ModulesPaneView.axaml",
        "/Views/ProcessesPaneView.axaml",
        "/Views/PropertyGroupsView.axaml",
        "/Views/ResultPaneView.axaml",
        "/Views/SourceDocumentView.axaml",
        "/Views/ThreadsPaneView.axaml",
        "/Views/WatchPaneView.axaml",
        "/Views/WelcomeDocumentView.axaml",
    ];

    private static readonly (string Name, string FailureCodePrefix)[] RequiredUiAssemblies =
    [
        ("Avalonia", "AVALONIA_FACADE"),
        ("Avalonia.Base", "AVALONIA_BASE"),
        ("Avalonia.Controls", "AVALONIA_CONTROLS"),
        ("Avalonia.Controls.DataGrid", "AVALONIA_DATA_GRID"),
        ("Avalonia.Desktop", "AVALONIA_DESKTOP"),
        ("Avalonia.HarfBuzz", "AVALONIA_HARFBUZZ"),
        ("Avalonia.Markup.Xaml", "AVALONIA_XAML"),
        ("Avalonia.Skia", "AVALONIA_SKIA"),
        ("Avalonia.Themes.Simple", "AVALONIA_SIMPLE_THEME"),
        ("Avalonia.Win32", "AVALONIA_WIN32"),
        ("AvaloniaEdit", "AVALONIA_EDIT"),
        ("Dock.Avalonia", "DOCK_AVALONIA"),
        ("Dock.Avalonia.Themes.Simple", "DOCK_SIMPLE_THEME"),
        ("Dock.Controls.DeferredContentControl", "DOCK_DEFERRED_CONTENT"),
        ("Dock.Controls.ProportionalStackPanel", "DOCK_PROPORTIONAL_STACK"),
        ("Dock.Controls.Recycling", "DOCK_RECYCLING"),
        ("Dock.Controls.Recycling.Model", "DOCK_RECYCLING_MODEL"),
        ("Dock.MarkupExtension", "DOCK_MARKUP"),
        ("Dock.Model", "DOCK_MODEL"),
        ("Dock.Model.Mvvm", "DOCK_MODEL_MVVM"),
        ("ProDataGrid.FormulaEngine", "PRO_DATA_GRID_FORMULA"),
        ("ProDataGrid.FormulaEngine.Excel", "PRO_DATA_GRID_EXCEL"),
        ("ICSharpCode.Decompiler", "DECOMPILER"),
        ("Microsoft.CodeAnalysis.CSharp", "ROSLYN_CSHARP"),
        ("Microsoft.Diagnostics.Runtime", "CLRMD"),
    ];

    internal static int Run()
    {
        try
        {
            var (entryAssembly, productVersion) = VerifyEntryAssembly();
            VerifyPackagedAssembly(entryAssembly, "PhoenixInspect", "ENTRY_ASSEMBLY");
            VerifyDependencyClosure();
            VerifyDesktopDependencies();
            VerifyApplicationResources();

            Console.Out.WriteLine(
                $"PHOENIXINSPECT_DESKTOP_SMOKE_OK version={productVersion} mode=non-ui");
            return 0;
        }
        catch (SmokeTestFailureException failure)
        {
            Console.Error.WriteLine($"{FailurePrefix} {failure.Code}");
            return FailureExitCode;
        }
        catch
        {
            Console.Error.WriteLine($"{FailurePrefix} UNEXPECTED_ERROR");
            return FailureExitCode;
        }
    }

    /// <summary>Verifies the packaged executable identity and returns its normalized product version.</summary>
    /// <returns>The product version without build metadata.</returns>
    internal static string VerifyProductVersion() => VerifyEntryAssembly().ProductVersion;

    private static void VerifyDependencyClosure()
    {
        var payloadRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(AppContext.BaseDirectory));
        var depsPath = Path.Combine(payloadRoot, "PhoenixInspect.deps.json");
        Require(File.Exists(depsPath), "DEPS_FILE_MISSING");

        try
        {
            using var stream = File.OpenRead(depsPath);
            using var document = JsonDocument.Parse(stream);
            var root = document.RootElement;
            Require(root.TryGetProperty("runtimeTarget", out var runtimeTarget), "DEPS_RUNTIME_TARGET_MISSING");
            Require(runtimeTarget.TryGetProperty("name", out var nameElement), "DEPS_RUNTIME_TARGET_NAME_MISSING");
            var targetName = nameElement.GetString();
            Require(!string.IsNullOrWhiteSpace(targetName), "DEPS_RUNTIME_TARGET_NAME_INVALID");
            Require(root.TryGetProperty("targets", out var targets), "DEPS_TARGETS_MISSING");
            Require(targets.TryGetProperty(targetName, out var target), "DEPS_SELECTED_TARGET_MISSING");

            var targetSeparator = targetName.LastIndexOf('/');
            var runtimeIdentifier = targetSeparator >= 0 ? targetName[(targetSeparator + 1)..] : null;
            var selectedAssetCount = 0;
            var selectedNativeAssetCount = 0;

            foreach (var library in target.EnumerateObject())
            {
                selectedAssetCount += VerifyAssetGroup(payloadRoot, library.Value, "runtime", null);
                var nativeAssetCount = VerifyAssetGroup(payloadRoot, library.Value, "native", null);
                selectedAssetCount += nativeAssetCount;
                selectedNativeAssetCount += nativeAssetCount;
                selectedAssetCount += VerifyAssetGroup(payloadRoot, library.Value, "resources", "locale");

                if (runtimeIdentifier is null ||
                    !library.Value.TryGetProperty("runtimeTargets", out var runtimeTargets))
                {
                    continue;
                }

                foreach (var asset in runtimeTargets.EnumerateObject())
                {
                    Require(asset.Value.TryGetProperty("rid", out var ridElement), "DEPS_RUNTIME_ASSET_RID_MISSING");
                    if (!string.Equals(ridElement.GetString(), runtimeIdentifier, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    Require(
                        asset.Value.TryGetProperty("assetType", out var assetTypeElement),
                        "DEPS_RUNTIME_ASSET_TYPE_MISSING");
                    var assetType = assetTypeElement.GetString();
                    Require(
                        assetType is "runtime" or "native" or "resources",
                        "DEPS_RUNTIME_ASSET_TYPE_INVALID");

                    var locale = assetType == "resources" &&
                        asset.Value.TryGetProperty("locale", out var localeElement)
                            ? localeElement.GetString()
                            : null;
                    VerifyPayloadAsset(payloadRoot, asset.Name, locale, assetType);
                    selectedAssetCount++;
                    if (assetType == "native")
                    {
                        selectedNativeAssetCount++;
                    }
                }
            }

            Require(selectedAssetCount > 0, "DEPS_SELECTED_TARGET_EMPTY");
            if (runtimeIdentifier is not null)
            {
                Require(selectedNativeAssetCount > 0, "DEPS_SELECTED_NATIVE_TARGET_EMPTY");
            }
        }
        catch (SmokeTestFailureException)
        {
            throw;
        }
        catch
        {
            throw new SmokeTestFailureException("DEPS_FILE_INVALID");
        }
    }

    private static int VerifyAssetGroup(
        string payloadRoot,
        JsonElement library,
        string groupName,
        string? localeProperty)
    {
        if (!library.TryGetProperty(groupName, out var group))
        {
            return 0;
        }

        var count = 0;
        foreach (var asset in group.EnumerateObject())
        {
            string? locale = null;
            if (localeProperty is not null)
            {
                Require(
                    asset.Value.TryGetProperty(localeProperty, out var localeElement),
                    "DEPS_RESOURCE_LOCALE_MISSING");
                locale = localeElement.GetString();
                Require(!string.IsNullOrWhiteSpace(locale), "DEPS_RESOURCE_LOCALE_INVALID");
            }

            VerifyPayloadAsset(payloadRoot, asset.Name, locale, groupName);
            count++;
        }

        return count;
    }

    private static void VerifyPayloadAsset(
        string payloadRoot,
        string assetPath,
        string? locale,
        string assetType)
    {
        Require(!string.IsNullOrWhiteSpace(assetPath), "DEPS_ASSET_PATH_INVALID");
        var fileName = Path.GetFileName(assetPath.Replace('/', Path.DirectorySeparatorChar));
        Require(!string.IsNullOrWhiteSpace(fileName), "DEPS_ASSET_FILE_NAME_INVALID");
        Require(fileName.IndexOfAny(Path.GetInvalidFileNameChars()) < 0, "DEPS_ASSET_FILE_NAME_INVALID");

        var relativePath = fileName;
        if (locale is not null)
        {
            Require(
                locale.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 &&
                !locale.Contains(Path.DirectorySeparatorChar) &&
                !locale.Contains(Path.AltDirectorySeparatorChar),
                "DEPS_RESOURCE_LOCALE_INVALID");
            relativePath = Path.Combine(locale, fileName);
        }

        var fullPath = Path.GetFullPath(Path.Combine(payloadRoot, relativePath));
        Require(
            fullPath.StartsWith(payloadRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase),
            "DEPS_ASSET_OUTSIDE_PAYLOAD");
        Require(File.Exists(fullPath), "DEPS_ASSET_MISSING");
        Require(new FileInfo(fullPath).Length > 0, "DEPS_ASSET_EMPTY");
        if (assetType == "runtime" &&
            string.Equals(Path.GetExtension(fullPath), ".dll", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                Require(AssemblyName.GetAssemblyName(fullPath).Name is { Length: > 0 }, "DEPS_MANAGED_ASSET_INVALID");
            }
            catch (SmokeTestFailureException)
            {
                throw;
            }
            catch
            {
                throw new SmokeTestFailureException("DEPS_MANAGED_ASSET_INVALID");
            }
        }
    }

    private static (Assembly Assembly, string ProductVersion) VerifyEntryAssembly()
    {
        var entryAssembly = Assembly.GetEntryAssembly();
        Require(entryAssembly is not null, "ENTRY_ASSEMBLY_UNAVAILABLE");
        Require(entryAssembly.GetName().Name == "PhoenixInspect", "ENTRY_ASSEMBLY_IDENTITY");

        var product = entryAssembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product;
        Require(product == "PhoenixInspect", "ENTRY_PRODUCT_IDENTITY");

        var informationalVersion = entryAssembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;
        Require(!string.IsNullOrWhiteSpace(informationalVersion), "ENTRY_PRODUCT_VERSION_UNAVAILABLE");

        var assemblyPath = entryAssembly.Location;
        Require(!string.IsNullOrWhiteSpace(assemblyPath), "ENTRY_ASSEMBLY_LOCATION_UNAVAILABLE");
        var fileProductVersion = FileVersionInfo.GetVersionInfo(assemblyPath).ProductVersion;
        Require(!string.IsNullOrWhiteSpace(fileProductVersion), "ENTRY_FILE_PRODUCT_VERSION_UNAVAILABLE");

        var productVersion = NormalizeProductVersion(informationalVersion);
        Require(
            productVersion == NormalizeProductVersion(fileProductVersion),
            "ENTRY_PRODUCT_VERSION_MISMATCH");

        return (entryAssembly, productVersion);
    }

    private static void VerifyDesktopDependencies()
    {
        // Constructing the builder exercises the Avalonia.Desktop configuration surface but does not call Setup,
        // Initialize, or StartWithClassicDesktopLifetime, so this cannot create a native window.
        Require(Program.BuildAvaloniaApp() is not null, "AVALONIA_BUILDER_UNAVAILABLE");

        foreach (var (assemblyName, failureCodePrefix) in RequiredUiAssemblies)
        {
            VerifyPackagedAssembly(
                LoadAssembly(assemblyName, $"{failureCodePrefix}_LOAD_FAILED"),
                assemblyName,
                failureCodePrefix);
        }

        Require(typeof(App).IsAssignableTo(typeof(Application)), "APP_TYPE_INVALID");
        VerifyPackagedAssembly(typeof(App).Assembly, "PhoenixInspect", "APP_ASSEMBLY");
        VerifyPackagedAssembly(
            typeof(DumpInspectionService).Assembly,
            "PhoenixInspect.Inspection",
            "INSPECTION_ASSEMBLY");
    }

    private static void VerifyApplicationResources()
    {
        var loader = new StandardAssetLoader(typeof(App).Assembly);
        var indexUri = new Uri(
            "avares://PhoenixInspect/!AvaloniaResourceXamlInfo",
            UriKind.Absolute);
        try
        {
            Require(loader.Exists(indexUri), "APP_RESOURCE_INDEX_MISSING");
            using var stream = loader.Open(indexUri);
            var document = XDocument.Load(stream, LoadOptions.None);
            var indexedPaths = document.Descendants()
                .Where(static element => element.Name.LocalName == "Value")
                .Select(static element => element.Value)
                .ToArray();
            Require(
                indexedPaths.Distinct(StringComparer.Ordinal).Count() == indexedPaths.Length,
                "APP_RESOURCE_INDEX_DUPLICATE");
            Require(
                indexedPaths.SequenceEqual(ResourcePaths, StringComparer.Ordinal),
                "APP_RESOURCE_INDEX_MISMATCH");
        }
        catch (SmokeTestFailureException)
        {
            throw;
        }
        catch
        {
            throw new SmokeTestFailureException("APP_RESOURCE_INDEX_INVALID");
        }
    }

    private static Assembly LoadAssembly(string simpleName, string failureCode)
    {
        try
        {
            return Assembly.Load(new AssemblyName(simpleName));
        }
        catch
        {
            throw new SmokeTestFailureException(failureCode);
        }
    }

    private static void VerifyPackagedAssembly(Assembly assembly, string expectedName, string failureCodePrefix)
    {
        Require(assembly.GetName().Name == expectedName, $"{failureCodePrefix}_IDENTITY");
        Require(!string.IsNullOrWhiteSpace(assembly.Location), $"{failureCodePrefix}_LOCATION_UNAVAILABLE");

        var assemblyPath = Path.GetFullPath(assembly.Location);
        Require(File.Exists(assemblyPath), $"{failureCodePrefix}_FILE_MISSING");
        Require(
            string.Equals(
                Path.GetDirectoryName(assemblyPath),
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(AppContext.BaseDirectory)),
                StringComparison.OrdinalIgnoreCase),
            $"{failureCodePrefix}_OUTSIDE_PAYLOAD");
    }

    private static string NormalizeProductVersion(string productVersion)
    {
        var metadataSeparator = productVersion.IndexOf('+', StringComparison.Ordinal);
        return metadataSeparator >= 0 ? productVersion[..metadataSeparator] : productVersion;
    }

    private static void Require([DoesNotReturnIf(false)] bool condition, string failureCode)
    {
        if (!condition)
        {
            throw new SmokeTestFailureException(failureCode);
        }
    }

    private sealed class SmokeTestFailureException(string code) : Exception
    {
        internal string Code { get; } = code;
    }
}
