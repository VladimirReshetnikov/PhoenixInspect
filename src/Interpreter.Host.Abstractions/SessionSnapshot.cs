using Interpreter.Core.Abstractions;
using Interpreter.Metadata.Abstractions;
using Interpreter.Types;

namespace Interpreter.Host.Abstractions;

/// <summary>
/// Captures session-wide environment metadata for deterministic modeling decisions.
/// </summary>
/// <param name="DumpCaptureTimeUtc">Dump capture timestamp in UTC when known.</param>
/// <param name="TargetLocalOffset">Target machine local offset when known.</param>
/// <param name="TargetMachineName">Target machine name when known.</param>
/// <param name="TargetProcessId">Target process identifier when known.</param>
/// <param name="EnvironmentVariables">Optional environment variable snapshot.</param>
public sealed record SessionSnapshot(
    DateTimeOffset? DumpCaptureTimeUtc,
    TimeSpan? TargetLocalOffset,
    string? TargetMachineName,
    int? TargetProcessId,
    IReadOnlyDictionary<string, string>? EnvironmentVariables);
