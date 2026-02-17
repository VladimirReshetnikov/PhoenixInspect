using Interpreter.Core.Abstractions;
using Interpreter.Metadata.Abstractions;
using Interpreter.Types;

namespace Interpreter.Host.Abstractions;

/// <summary>
/// Optional low-level process-memory reader contract for advanced host scenarios.
/// </summary>
public interface IProcessMemoryReader
{
    /// <summary>
    /// Tries to copy memory bytes from a process address into a destination span.
    /// </summary>
    /// <param name="address">Starting address to read.</param>
    /// <param name="destination">Destination span receiving copied bytes.</param>
    /// <returns><see langword="true"/> when read succeeds; otherwise <see langword="false"/>.</returns>
    bool TryRead(ulong address, Span<byte> destination);
}
