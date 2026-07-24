namespace PhoenixInspect.W8WorkflowShapeTarget;

/// <summary>Names the closed reference argument that substitutes <c>VAR 0</c> in the stage slot.</summary>
/// <remarks>This emitted fixture type is a physical oracle, not a product contract.</remarks>
public sealed class StepContext
{
    /// <summary>Initializes a step context with a stable fixture label.</summary>
    /// <param name="label">The explicitly retained fixture label.</param>
    public StepContext(string label) => Label = label;

    /// <summary>Stores the fixture label read by the conditional-chain suffix.</summary>
    public readonly string Label;
}

/// <summary>Names the workflow marker projected from a metadata Constant row.</summary>
/// <remarks>This emitted fixture enum is a physical oracle, not a product contract.</remarks>
public enum StageMarker
{
    /// <summary>The workflow has not started.</summary>
    Pending = 0,

    /// <summary>The workflow is running.</summary>
    Running = 1,

    /// <summary>The workflow completed.</summary>
    Completed = 2,
}

/// <summary>Owns the workflow stage slot whose declared field signature is <c>VAR 0</c>.</summary>
/// <typeparam name="TStep">The closed argument, including array and nested constructed forms.</typeparam>
/// <remarks>This emitted fixture type is a physical oracle, not a product contract.</remarks>
public static class Stage<TStep>
    where TStep : class
{
    /// <summary>Stores the substituted per-construction current step.</summary>
    public static TStep? Current;
}

/// <summary>Owns the base static field a derived same-name member hides.</summary>
/// <remarks>This emitted fixture type is a physical oracle, not a product contract.</remarks>
public class BaseStage
{
    /// <summary>Stores the base sentinel the derived member hides.</summary>
    public static int Sentinel;

    /// <summary>Initializes the base owner.</summary>
    public BaseStage()
    {
    }
}

/// <summary>Declares a valid but unsupported same-name member that hides the base static field.</summary>
/// <remarks>This emitted fixture type is a physical oracle, not a product contract.</remarks>
public sealed class HidingStage : BaseStage
{
    /// <summary>Gets the derived member that owns the spelling the base field declares.</summary>
    public static new int Sentinel => BaseStage.Sentinel ^ 0x0F_0F_0F_0F;

    /// <summary>Initializes the derived owner.</summary>
    public HidingStage()
    {
    }
}

/// <summary>Owns the qualified spelling a second load context duplicates.</summary>
/// <remarks>This emitted fixture type is a physical oracle, not a product contract.</remarks>
public static class DuplicatedStageStatics
{
    /// <summary>Stores the sentinel both loaded definitions declare.</summary>
    public static int Sentinel;
}

/// <summary>Owns the first competing declaration of the ambiguous workflow alias spelling.</summary>
/// <remarks>This emitted fixture type is a physical oracle, not a product contract.</remarks>
public static class FirstAmbiguousStage
{
    /// <summary>Stores the first competing sentinel.</summary>
    public static int Sentinel;
}

/// <summary>Owns the second competing declaration of the ambiguous workflow alias spelling.</summary>
/// <remarks>This emitted fixture type is a physical oracle, not a product contract.</remarks>
public static class SecondAmbiguousStage
{
    /// <summary>Stores the second competing sentinel.</summary>
    public static int Sentinel;
}

/// <summary>Owns the ground enum literal reached through a fully ground TypeSpec alias.</summary>
/// <typeparam name="TStep">The ground closed argument of the literal owner.</typeparam>
/// <remarks>This emitted fixture type is a physical oracle, not a product contract.</remarks>
public static class StageMarkers<TStep>
    where TStep : class
{
    /// <summary>Stores the metadata-only completed marker.</summary>
    public const StageMarker CompletedMarker = StageMarker.Completed;
}
