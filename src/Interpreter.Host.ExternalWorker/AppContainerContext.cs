using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;

namespace Interpreter.Host.ExternalWorker;

internal sealed class AppContainerContext : IDisposable
{
    private const string ProfileName = "VladimirReshetnikov.Interpreter.ExternalWorker";
    private const int AlreadyExistsHResult = unchecked((int)0x800700B7);
    private const int MaximumDeploymentFiles = 128;
    private const long MaximumDeploymentBytes = 64L * 1024 * 1024;
    private readonly string _requestDirectory;
    private int _disposed;

    private AppContainerContext(
        IntPtr sid,
        string sidString,
        string localAppDataDirectory,
        string requestDirectory,
        string scratchDirectory)
    {
        Sid = sid;
        SidString = sidString;
        LocalAppDataDirectory = localAppDataDirectory;
        _requestDirectory = requestDirectory;
        ScratchDirectory = scratchDirectory;
    }

    internal IntPtr Sid { get; }

    internal string SidString { get; }

    internal string ScratchDirectory { get; }

    internal string LocalAppDataDirectory { get; }

    internal static AppContainerContext Create()
    {
        var result = WindowsNative.CreateAppContainerProfile(
            ProfileName,
            "Interpreter external artifact worker",
            "Constrained one-shot dump evidence worker",
            IntPtr.Zero,
            0,
            out var sid);
        if (result == AlreadyExistsHResult)
        {
            result = WindowsNative.DeriveAppContainerSidFromAppContainerName(ProfileName, out sid);
        }

        if (result < 0 || sid == IntPtr.Zero)
        {
            Marshal.ThrowExceptionForHR(result);
        }

        IntPtr sidText = IntPtr.Zero;
        IntPtr folderText = IntPtr.Zero;
        try
        {
            if (!WindowsNative.ConvertSidToStringSidW(sid, out sidText))
            {
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            }

            var sidString = Marshal.PtrToStringUni(sidText)
                ?? throw new InvalidOperationException("The AppContainer SID could not be represented.");
            var folderResult = WindowsNative.GetAppContainerFolderPath(sidString, out folderText);
            if (folderResult < 0 || folderText == IntPtr.Zero)
            {
                Marshal.ThrowExceptionForHR(folderResult);
            }

            var profileFolder = Marshal.PtrToStringUni(folderText)
                ?? throw new InvalidOperationException("The AppContainer profile path is unavailable.");
            var requestDirectory = Path.Combine(profileFolder, "Temp", $"request-{Guid.NewGuid():N}");
            var scratchDirectory = Path.Combine(requestDirectory, "work");
            Directory.CreateDirectory(scratchDirectory);
            return new AppContainerContext(
                sid,
                sidString,
                profileFolder,
                requestDirectory,
                scratchDirectory);
        }
        catch
        {
            _ = WindowsNative.FreeSid(sid);
            throw;
        }
        finally
        {
            if (sidText != IntPtr.Zero)
            {
                _ = WindowsNative.LocalFree(sidText);
            }

            if (folderText != IntPtr.Zero)
            {
                Marshal.FreeCoTaskMem(folderText);
            }
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        Exception? cleanupFailure = null;
        try
        {
            if (Directory.Exists(_requestDirectory))
            {
                Directory.Delete(_requestDirectory, recursive: true);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            cleanupFailure = exception;
        }

        _ = WindowsNative.FreeSid(Sid);
        if (cleanupFailure is not null || Directory.Exists(_requestDirectory))
        {
            throw new ExternalWorkerCleanupException();
        }
    }

    internal string DeployRunner(string trustedRunnerExecutablePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(trustedRunnerExecutablePath);
        if (!Path.IsPathFullyQualified(trustedRunnerExecutablePath))
        {
            throw new ArgumentException("The trusted runner path must be fully qualified.", nameof(trustedRunnerExecutablePath));
        }

        var sourceExecutable = new FileInfo(Path.GetFullPath(trustedRunnerExecutablePath));
        if (!sourceExecutable.Exists)
        {
            throw new FileNotFoundException("The trusted runner executable is unavailable.");
        }

        var sourceDirectory = sourceExecutable.Directory
            ?? throw new InvalidOperationException("The trusted runner directory is unavailable.");
        var deploymentDirectory = Path.Combine(_requestDirectory, "runner");
        Directory.CreateDirectory(deploymentDirectory);
        ProtectDeploymentDirectory(deploymentDirectory, new SecurityIdentifier(SidString));

        var files = sourceDirectory
            .EnumerateFiles("*", SearchOption.TopDirectoryOnly)
            .Where(static file => IsDeploymentFile(file.Extension))
            .OrderBy(static file => file.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (files.Length == 0 || files.Length > MaximumDeploymentFiles)
        {
            throw new InvalidDataException("The trusted runner deployment violates its file-count bound.");
        }

        long totalBytes = 0;
        var executableCopied = false;
        foreach (var file in files)
        {
            if ((file.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidDataException("The trusted runner deployment cannot contain reparse points.");
            }

            var copiedLength = CopyDeploymentFile(
                file,
                Path.Combine(deploymentDirectory, file.Name),
                MaximumDeploymentBytes - totalBytes);
            totalBytes = checked(totalBytes + copiedLength);
            if (totalBytes > MaximumDeploymentBytes)
            {
                throw new InvalidDataException("The trusted runner deployment exceeds its byte bound.");
            }

            executableCopied |= string.Equals(
                file.FullName,
                sourceExecutable.FullName,
                StringComparison.OrdinalIgnoreCase);
        }

        if (!executableCopied)
        {
            throw new InvalidDataException("The trusted runner executable is outside the admitted deployment set.");
        }

        return Path.Combine(deploymentDirectory, sourceExecutable.Name);
    }

    private static bool IsDeploymentFile(string extension) =>
        extension.Equals(".exe", StringComparison.OrdinalIgnoreCase) ||
        extension.Equals(".dll", StringComparison.OrdinalIgnoreCase) ||
        extension.Equals(".json", StringComparison.OrdinalIgnoreCase);

    private static long CopyDeploymentFile(FileInfo sourceFile, string destinationPath, long remainingByteBudget)
    {
        using var source = new FileStream(sourceFile.FullName, FileMode.Open, FileAccess.Read, FileShare.Read);
        var expectedLength = source.Length;
        if (expectedLength < 0 || expectedLength > remainingByteBudget)
        {
            throw new InvalidDataException("The trusted runner deployment exceeds its byte bound.");
        }

        using var destination = new FileStream(destinationPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        source.CopyTo(destination);
        destination.Flush(flushToDisk: true);
        if (source.Length != expectedLength || destination.Length != expectedLength)
        {
            throw new InvalidDataException("The trusted runner deployment changed while it was staged.");
        }

        return expectedLength;
    }

    private static void ProtectDeploymentDirectory(
        string deploymentDirectory,
        SecurityIdentifier appContainerSid)
    {
        var currentUser = WindowsIdentity.GetCurrent().User
            ?? throw new InvalidOperationException("The broker user SID is unavailable.");
        var security = new DirectorySecurity();
        security.SetOwner(currentUser);
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        AddDeploymentRule(security, currentUser, FileSystemRights.FullControl);
        AddDeploymentRule(security, new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null), FileSystemRights.FullControl);
        AddDeploymentRule(
            security,
            new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null),
            FileSystemRights.FullControl);
        AddDeploymentRule(
            security,
            appContainerSid,
            FileSystemRights.ReadAndExecute | FileSystemRights.Synchronize);
        new DirectoryInfo(deploymentDirectory).SetAccessControl(security);
    }

    private static void AddDeploymentRule(
        DirectorySecurity security,
        SecurityIdentifier principal,
        FileSystemRights rights)
    {
        security.AddAccessRule(new FileSystemAccessRule(
            principal,
            rights,
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
            PropagationFlags.None,
            AccessControlType.Allow));
    }
}

internal sealed class ExternalWorkerCleanupException : IOException
{
    internal ExternalWorkerCleanupException()
        : base("The external worker could not remove its private request data.")
    {
    }
}
