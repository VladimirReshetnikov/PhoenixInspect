using UnrelatedValues = System.Environment;

namespace Interpreter.W7PdbConflictFixture;

internal static class ConflictingImports
{
    internal static string Read() => UnrelatedValues.MachineName;
}
