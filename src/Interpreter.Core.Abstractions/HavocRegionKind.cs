using Interpreter.IL;
using Interpreter.Types;

namespace Interpreter.Core.Abstractions;

/// <summary>
/// Defines havoc scope kinds for conservative memory invalidation.
/// </summary>
public enum HavocRegionKind
{
    Everything,
    ReachableFromArgs,
    SpecificObject,
    SpecificAddressable,
}
