using UnrelatedValues = System.Environment;

namespace PhoenixInspect.W7PdbConflictFixture;

internal static class ConflictingImports
{
    internal static string Read() => UnrelatedValues.MachineName;
}
