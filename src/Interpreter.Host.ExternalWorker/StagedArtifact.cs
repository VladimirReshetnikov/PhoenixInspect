using Microsoft.Win32.SafeHandles;

namespace Interpreter.Host.ExternalWorker;

internal sealed class StagedArtifact : IDisposable
{
    private const long MaximumBytes = 8L * 1024 * 1024 * 1024;
    private readonly string _directory;
    private readonly FileStream _stream;
    private int _disposed;

    private StagedArtifact(string directory, FileStream stream)
    {
        _directory = directory;
        _stream = stream;
    }

    internal SafeFileHandle Handle => _stream.SafeFileHandle;

    internal static StagedArtifact Create(string sourcePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        var directory = Path.Combine(Path.GetTempPath(), $"interpreter-worker-stage-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var stagedPath = Path.Combine(directory, "artifact.bin");
        try
        {
            using (var source = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var destination = new FileStream(stagedPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                if (source.Length > MaximumBytes)
                {
                    throw new ExternalArtifactLimitException();
                }

                var buffer = new byte[1024 * 1024];
                long total = 0;
                while (true)
                {
                    var count = source.Read(buffer);
                    if (count == 0)
                    {
                        break;
                    }

                    total = checked(total + count);
                    if (total > MaximumBytes)
                    {
                        throw new ExternalArtifactLimitException();
                    }

                    destination.Write(buffer, 0, count);
                }

                destination.Flush(flushToDisk: true);
            }

            var stream = new FileStream(stagedPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (!WindowsNative.SetHandleInformation(stream.SafeFileHandle, WindowsNative.HandleFlagInherit, WindowsNative.HandleFlagInherit))
            {
                stream.Dispose();
                throw new System.ComponentModel.Win32Exception(System.Runtime.InteropServices.Marshal.GetLastWin32Error());
            }

            return new StagedArtifact(directory, stream);
        }
        catch
        {
            try
            {
                Directory.Delete(directory, recursive: true);
            }
            catch
            {
            }

            throw;
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _stream.Dispose();
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new ExternalWorkerCleanupException();
        }

        if (Directory.Exists(_directory))
        {
            throw new ExternalWorkerCleanupException();
        }
    }
}

internal sealed class ExternalArtifactLimitException : IOException
{
    internal ExternalArtifactLimitException()
        : base("The external artifact exceeds the broker admission bound.")
    {
    }
}
