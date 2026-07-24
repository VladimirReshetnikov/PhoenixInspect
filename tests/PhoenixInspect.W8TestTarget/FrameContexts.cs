using System.Runtime.CompilerServices;

namespace PhoenixInspect.W8TestTarget;

/// <summary>Supplies a selected frame with both declaring-type and method generic arguments.</summary>
/// <typeparam name="TType">The exact closed declaring-type argument retained in the frame.</typeparam>
/// <remarks>This is a physical frame fixture and not a frame-value product contract.</remarks>
public sealed class GenericFrameOwner<TType>
{
    private readonly TType ownerValue;

    /// <summary>Initializes the selected-frame owner with its closed declaring-type value.</summary>
    /// <param name="ownerValue">The exact value retained through the selected frame.</param>
    public GenericFrameOwner(TType ownerValue) => this.ownerValue = ownerValue;

    /// <summary>Pauses with <c>this</c>, reference/value parameters, locals, and both generic contexts live.</summary>
    /// <typeparam name="TMethod">The exact closed method argument retained in the frame.</typeparam>
    /// <param name="profile">The selected target profile.</param>
    /// <param name="methodValue">The method-generic reference parameter.</param>
    /// <param name="request">The ordinary reference parameter.</param>
    /// <param name="value">The ordinary value parameter.</param>
    /// <param name="number">The ordinary primitive parameter.</param>
    /// <returns>A process exit code if the pause unexpectedly returns.</returns>
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    public int Run<TMethod>(
        string profile,
        TMethod methodValue,
        RequestContext request,
        ValueContext value,
        int number)
    {
        var localThis = this;
        var localOwnerValue = ownerValue;
        var localMethodValue = methodValue;
        var localRequest = request;
        var localValue = value;
        var localNumber = number ^ value.Marker;
        var declaringTypeWitness = typeof(TType);
        var methodTypeWitness = typeof(TMethod);

        Console.WriteLine("READY");
        Console.Out.Flush();
        Thread.Sleep(Timeout.Infinite);

        GC.KeepAlive(localThis);
        GC.KeepAlive(localOwnerValue);
        GC.KeepAlive(localMethodValue);
        GC.KeepAlive(localRequest);
        GC.KeepAlive(localValue);
        GC.KeepAlive(localNumber);
        GC.KeepAlive(declaringTypeWitness);
        GC.KeepAlive(methodTypeWitness);
        GC.KeepAlive(profile);
        return 0;
    }
}
