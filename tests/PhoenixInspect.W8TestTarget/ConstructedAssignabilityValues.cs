using System.Runtime.CompilerServices;

namespace PhoenixInspect.W8TestTarget;

/// <summary>Defines an invariant constructed interface for W8 runtime-identity comparisons.</summary>
/// <typeparam name="T">The exact invariant argument retained in emitted metadata.</typeparam>
/// <remarks>This fixture interface is physical evidence, not a product type contract.</remarks>
public interface IInvariantNode<T>
{
}

/// <summary>Defines a covariant constructed interface for W8 runtime-identity comparisons.</summary>
/// <typeparam name="T">The exact covariant result argument retained in emitted metadata.</typeparam>
/// <remarks>This fixture interface is physical evidence, not a product type contract.</remarks>
public interface ICovariantNode<out T>
{
}

/// <summary>Defines a contravariant constructed interface for W8 runtime-identity comparisons.</summary>
/// <typeparam name="T">The exact contravariant input argument retained in emitted metadata.</typeparam>
/// <remarks>This fixture interface is physical evidence, not a product type contract.</remarks>
public interface IContravariantNode<in T>
{
}

/// <summary>Supplies the base-class identity used by the W8 constructed-assignability fixture.</summary>
/// <remarks>This fixture class is physical evidence, not a product type contract.</remarks>
public class AssignabilityBaseNode
{
    /// <summary>Initializes the base node with its deterministic marker.</summary>
    /// <param name="marker">The marker retained by the target object.</param>
    public AssignabilityBaseNode(int marker) => Marker = marker;

    /// <summary>Gets the deterministic marker retained by this node.</summary>
    public int Marker { get; }
}

/// <summary>Supplies the derived-class identity used by the W8 constructed-assignability fixture.</summary>
/// <remarks>This fixture class is physical evidence, not a product type contract.</remarks>
public sealed class AssignabilityDerivedNode : AssignabilityBaseNode
{
    /// <summary>Initializes the derived node with its deterministic marker.</summary>
    /// <param name="marker">The marker retained by the target object.</param>
    public AssignabilityDerivedNode(int marker)
        : base(marker)
    {
    }
}

/// <summary>Implements the invariant and both variant interface definitions at one exact argument.</summary>
/// <typeparam name="T">The exact constructed argument represented by this carrier.</typeparam>
/// <remarks>This fixture class is physical evidence, not a product type contract.</remarks>
public sealed class AssignabilityCarrier<T> :
    IInvariantNode<T>,
    ICovariantNode<T>,
    IContravariantNode<T>
{
    /// <summary>Initializes the carrier with one exact retained value.</summary>
    /// <param name="value">The value that forces the closed argument and object to remain materialized.</param>
    public AssignabilityCarrier(T value) => Value = value;

    /// <summary>Gets the exact retained value.</summary>
    public T Value { get; }
}

/// <summary>
/// Roots one object through exact base, interface, variant-interface, and array views for dump-time comparison.
/// </summary>
/// <remarks>
/// Every member is a W8.1 physical witness. The field set intentionally exposes aliasing between slots so the
/// dump oracle can compare raw object addresses only after independently decoding each declared constructed type.
/// </remarks>
public static class ConstructedAssignabilityStorage
{
    /// <summary>Stores the exact derived object.</summary>
    public static readonly AssignabilityDerivedNode DirectDerived;

    /// <summary>Stores the same derived object through its base declaration.</summary>
    public static readonly AssignabilityBaseNode BaseView;

    /// <summary>Stores the exact request carrier.</summary>
    public static readonly AssignabilityCarrier<RequestContext> DirectRequestCarrier;

    /// <summary>Stores the request carrier through its invariant interface.</summary>
    public static readonly IInvariantNode<RequestContext> InvariantRequest;

    /// <summary>Stores a physically different batch carrier through the same invariant definition.</summary>
    public static readonly IInvariantNode<BatchContext> InvariantBatch;

    /// <summary>Stores the request carrier through the exact covariant source construction.</summary>
    public static readonly ICovariantNode<RequestContext> CovariantRequest;

    /// <summary>Stores the same request carrier through the covariant object construction.</summary>
    public static readonly ICovariantNode<object> CovariantObject;

    /// <summary>Stores the exact object carrier.</summary>
    public static readonly AssignabilityCarrier<object> DirectObjectCarrier;

    /// <summary>Stores the object carrier through the exact contravariant source construction.</summary>
    public static readonly IContravariantNode<object> ContravariantObject;

    /// <summary>Stores the same object carrier through the contravariant request construction.</summary>
    public static readonly IContravariantNode<RequestContext> ContravariantRequest;

    /// <summary>Stores the exact request vector.</summary>
    public static readonly RequestContext[] RequestVector;

    /// <summary>Stores the same request vector through its covariant object-vector declaration.</summary>
    public static readonly object[] ObjectVector;

    /// <summary>Stores the exact rank-two request array.</summary>
    public static readonly RequestContext[,] RequestMatrix;

    /// <summary>Stores the same rank-two request array through its covariant object-array declaration.</summary>
    public static readonly object[,] ObjectMatrix;

    /// <summary>Stores an exact vector whose value-type element blocks reference-array covariance.</summary>
    public static readonly ValueContext[] ValueVector;

    /// <summary>Stores the same value vector through the common array base class.</summary>
    public static readonly Array ValueArrayView;

    static ConstructedAssignabilityStorage()
    {
        DirectDerived = new AssignabilityDerivedNode(unchecked((int)0xA1017A01));
        BaseView = DirectDerived;

        var request = new RequestContext("assignability-request-83");
        DirectRequestCarrier = new AssignabilityCarrier<RequestContext>(request);
        InvariantRequest = DirectRequestCarrier;
        InvariantBatch = new AssignabilityCarrier<BatchContext>(new BatchContext("assignability-batch-89"));
        CovariantRequest = DirectRequestCarrier;
        CovariantObject = CovariantRequest;

        DirectObjectCarrier = new AssignabilityCarrier<object>(new object());
        ContravariantObject = DirectObjectCarrier;
        ContravariantRequest = ContravariantObject;

        RequestVector = [request, new RequestContext("assignability-vector-97")];
        ObjectVector = RequestVector;
        RequestMatrix = new RequestContext[1, 2];
        RequestMatrix[0, 0] = request;
        RequestMatrix[0, 1] = new RequestContext("assignability-matrix-101");
        ObjectMatrix = RequestMatrix;
        ValueVector = [new ValueContext(unchecked((int)0xA2027A02))];
        ValueArrayView = ValueVector;
    }

    /// <summary>Forces every assignability witness to be initialized before the full dump is written.</summary>
    /// <remarks>This fixture method performs target setup only; the evaluator never invokes it.</remarks>
    public static void Materialize()
    {
        GC.KeepAlive(DirectDerived);
        GC.KeepAlive(BaseView);
        GC.KeepAlive(DirectRequestCarrier);
        GC.KeepAlive(InvariantRequest);
        GC.KeepAlive(InvariantBatch);
        GC.KeepAlive(CovariantRequest);
        GC.KeepAlive(CovariantObject);
        GC.KeepAlive(DirectObjectCarrier);
        GC.KeepAlive(ContravariantObject);
        GC.KeepAlive(ContravariantRequest);
        GC.KeepAlive(RequestVector);
        GC.KeepAlive(ObjectVector);
        GC.KeepAlive(RequestMatrix);
        GC.KeepAlive(ObjectMatrix);
        GC.KeepAlive(ValueVector);
        GC.KeepAlive(ValueArrayView);
    }
}

internal static class ConstructedAssignabilityModuleInitializer
{
    [ModuleInitializer]
    internal static void Initialize() => ConstructedAssignabilityStorage.Materialize();
}
