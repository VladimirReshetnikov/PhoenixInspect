namespace PhoenixInspect.W8CoordinatorShapeTarget;

/// <summary>Names the first of four coexisting coordinator region arguments.</summary>
/// <remarks>This emitted fixture type is a physical oracle, not a product contract.</remarks>
public sealed class NorthRegion
{
    /// <summary>Initializes the region witness.</summary>
    public NorthRegion()
    {
    }
}

/// <summary>Names the second of four coexisting coordinator region arguments.</summary>
/// <remarks>This emitted fixture type is a physical oracle, not a product contract.</remarks>
public sealed class SouthRegion
{
    /// <summary>Initializes the region witness.</summary>
    public SouthRegion()
    {
    }
}

/// <summary>Names the third of four coexisting coordinator region arguments.</summary>
/// <remarks>This emitted fixture type is a physical oracle, not a product contract.</remarks>
public sealed class EastRegion
{
    /// <summary>Initializes the region witness.</summary>
    public EastRegion()
    {
    }
}

/// <summary>Names the fourth of four coexisting coordinator region arguments.</summary>
/// <remarks>This emitted fixture type is a physical oracle, not a product contract.</remarks>
public sealed class WestRegion
{
    /// <summary>Initializes the region witness.</summary>
    public WestRegion()
    {
    }
}

/// <summary>Owns the coordinator registry whose four closed constructions coexist in one snapshot.</summary>
/// <typeparam name="TRegion">The closed region argument that selects one construction.</typeparam>
/// <remarks>This emitted fixture type is a physical oracle, not a product contract.</remarks>
public static class Registry<TRegion>
{
    /// <summary>Stores the per-construction coordinator sentinel.</summary>
    public static int Sentinel;
}

/// <summary>Owns the constructed generic base that declares the field a derived owner exposes.</summary>
/// <typeparam name="TRegion">The closed region argument of the base construction.</typeparam>
/// <remarks>This emitted fixture type is a physical oracle, not a product contract.</remarks>
public class RegistryBase<TRegion>
{
    /// <summary>Stores the sentinel declared on the constructed generic base.</summary>
    public static int BaseSentinel;

    /// <summary>Initializes the base owner.</summary>
    public RegistryBase()
    {
    }
}

/// <summary>Names a qualified derived owner over one closed generic base construction.</summary>
/// <remarks>This emitted fixture type is a physical oracle, not a product contract.</remarks>
public sealed class DerivedRegistry : RegistryBase<WestRegion>
{
    /// <summary>Initializes the derived owner.</summary>
    public DerivedRegistry()
    {
    }
}

/// <summary>Owns the coordinator slot whose field signature is <c>VAR 0</c> over a closed nullable argument.</summary>
/// <typeparam name="TValue">The closed argument, including closed nullable forms.</typeparam>
/// <remarks>This emitted fixture type is a physical oracle, not a product contract.</remarks>
public static class NullableSlot<TValue>
{
    /// <summary>Stores the closed nullable value projected by the corpus.</summary>
    public static TValue? Current;
}

/// <summary>Supplies the coordinator surface where a property shares a bare field spelling.</summary>
/// <remarks>This emitted fixture type is a physical oracle, not a product contract.</remarks>
public static class CoordinatorTotals
{
    /// <summary>Stores the declared static field the bare property spelling does not name.</summary>
    public static int DeclaredRegionTotal;

    /// <summary>Gets the valid but unsupported member that owns the bare spelling.</summary>
    public static int RegionTotal => DeclaredRegionTotal;
}
