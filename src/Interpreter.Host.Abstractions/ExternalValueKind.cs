using Interpreter.Core.Abstractions;
using Interpreter.Metadata.Abstractions;
using Interpreter.Types;

namespace Interpreter.Host.Abstractions;

/// <summary>
/// Classifies primitive and reference payload shapes used by host value snapshots.
/// </summary>
public enum ExternalValueKind
{
    Unavailable,
    Int32,
    Int64,
    Float64,
    Boolean,
    ObjectRef,
    StringRef,
    RawBytes,
}
