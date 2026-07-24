using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Diagnostics.Runtime;

namespace PhoenixInspect.IntegrationTests;

internal sealed record W8FrameDbiTypeArgument(
    int ElementType,
    int MetadataToken,
    ulong AssemblyAddress,
    ulong TypeHandle);

internal sealed record W8FrameDbiObservation(
    W8FrameProbeDisposition Disposition,
    int FactoryHResult,
    int ResolveHResult,
    ulong RawToken,
    ulong ResolvedToken,
    int EnumerateHResult,
    uint DeclaringTypeArgumentCount,
    ImmutableArray<W8FrameDbiTypeArgument> Arguments,
    string Code)
{
    internal static W8FrameDbiObservation NotAdmitted(
        W8FrameProbeDisposition disposition,
        int hresult,
        string code) =>
        new(
            disposition,
            hresult,
            hresult,
            0,
            0,
            hresult,
            0,
            ImmutableArray<W8FrameDbiTypeArgument>.Empty,
            code);
}

internal static class W8FrameDbiProbe
{
    private const int S_OK = 0;
    private const int E_FAIL = unchecked((int)0x80004005);
    private const int E_NOTIMPL = unchecked((int)0x80004001);
    private const int E_NOINTERFACE = unchecked((int)0x80004002);
    private const uint TypeContextIlNumber = unchecked((uint)-3);
    private const int ResolveExactGenericArgsTokenMethodIndex = 57;
    private const int EnumerateMethodDescParamsMethodIndex = 70;
    private const int MaximumTypeArguments = 64;
    private static readonly Guid DbiInterfaceId =
        new("DB505C1B-A327-4A46-8C32-AF55A56F8E09");

    internal static W8FrameDbiObservation Observe(
        ClrRuntime runtime,
        IMemoryReader memory,
        ulong methodDescriptor,
        W8FrameLegacyObservation legacy)
    {
        if (legacy.ExactGenericTokenHResult != S_OK)
        {
            return W8FrameDbiObservation.NotAdmitted(
                ToDisposition(legacy.ExactGenericTokenHResult),
                legacy.ExactGenericTokenHResult,
                "exact-token-call-unavailable");
        }

        if (legacy.ExactGenericToken is not
            {
                Disposition: W8FrameProbeDisposition.Exact,
                LocationKind: W8FrameLocationKind.Memory,
            } token ||
            token.Locations.Length != 1 ||
            token.Locations[0].Flags != 0 ||
            token.Locations[0].Argument == 0)
        {
            return W8FrameDbiObservation.NotAdmitted(
                W8FrameProbeDisposition.Unavailable,
                E_NOINTERFACE,
                "exact-token-location-unavailable");
        }

        var pointerBytes = new byte[memory.PointerSize];
        if (memory.Read(token.Locations[0].Argument, pointerBytes) != pointerBytes.Length)
        {
            return W8FrameDbiObservation.NotAdmitted(
                W8FrameProbeDisposition.Unavailable,
                E_FAIL,
                "exact-token-memory-unavailable");
        }

        var rawToken = memory.PointerSize switch
        {
            4 => (ulong)BinaryPrimitives.ReadUInt32LittleEndian(pointerBytes),
            8 => BinaryPrimitives.ReadUInt64LittleEndian(pointerBytes),
            _ => 0UL,
        };
        if (rawToken == 0)
        {
            return new W8FrameDbiObservation(
                W8FrameProbeDisposition.Invalid,
                S_OK,
                S_OK,
                0,
                0,
                E_FAIL,
                0,
                ImmutableArray<W8FrameDbiTypeArgument>.Empty,
                "exact-token-zero-rejected");
        }

        var factoryHResult = CreateInterface(runtime, out var dbi, out var factoryCode);
        if (factoryHResult != S_OK || dbi == IntPtr.Zero)
        {
            return new W8FrameDbiObservation(
                ToDisposition(factoryHResult),
                factoryHResult,
                factoryHResult,
                rawToken,
                0,
                factoryHResult,
                0,
                ImmutableArray<W8FrameDbiTypeArgument>.Empty,
                factoryCode);
        }

        try
        {
            var resolve = GetVtableDelegate<ResolveExactGenericArgsTokenDelegate>(
                dbi,
                ResolveExactGenericArgsTokenMethodIndex);
            var resolveHResult = resolve(
                dbi,
                TypeContextIlNumber,
                rawToken,
                out var resolvedToken);
            if (resolveHResult != S_OK || resolvedToken == 0)
            {
                return new W8FrameDbiObservation(
                    resolveHResult == S_OK
                        ? W8FrameProbeDisposition.Invalid
                        : ToDisposition(resolveHResult),
                    factoryHResult,
                    resolveHResult,
                    rawToken,
                    0,
                    resolveHResult,
                    0,
                    ImmutableArray<W8FrameDbiTypeArgument>.Empty,
                    resolveHResult == S_OK
                        ? "resolved-token-zero-rejected"
                        : "exact-token-resolution-unavailable");
            }

            var enumerate = GetVtableDelegate<EnumerateMethodDescParamsDelegate>(
                dbi,
                EnumerateMethodDescParamsMethodIndex);
            var state = new CallbackState();
            var stateHandle = GCHandle.Alloc(state, GCHandleType.Normal);
            var callback = new ExpandedTypeDataCallback(CaptureTypeArgument);
            int enumerateHResult;
            uint declaringTypeArgumentCount;
            try
            {
                enumerateHResult = enumerate(
                    dbi,
                    methodDescriptor,
                    resolvedToken,
                    out declaringTypeArgumentCount,
                    callback,
                    GCHandle.ToIntPtr(stateHandle));
            }
            finally
            {
                GC.KeepAlive(callback);
                stateHandle.Free();
            }

            var arguments = state.Arguments.ToImmutableArray();
            if (state.BoundReached)
            {
                return new W8FrameDbiObservation(
                    W8FrameProbeDisposition.BoundReached,
                    factoryHResult,
                    resolveHResult,
                    rawToken,
                    resolvedToken,
                    enumerateHResult,
                    declaringTypeArgumentCount,
                    arguments,
                    "dbi-type-argument-bound-reached");
            }

            return new W8FrameDbiObservation(
                enumerateHResult == S_OK
                    ? W8FrameProbeDisposition.Exact
                    : ToDisposition(enumerateHResult),
                factoryHResult,
                resolveHResult,
                rawToken,
                resolvedToken,
                enumerateHResult,
                declaringTypeArgumentCount,
                arguments,
                enumerateHResult == S_OK
                    ? "dbi-exact"
                    : "dbi-type-arguments-unavailable");
        }
        finally
        {
            _ = Marshal.Release(dbi);
        }
    }

    private static int CreateInterface(
        ClrRuntime runtime,
        out IntPtr dbi,
        out string code)
    {
        dbi = IntPtr.Zero;
        code = "dbi-interface-unavailable";
        try
        {
            var assembly = typeof(ClrRuntime).Assembly;
            var servicesField = typeof(ClrRuntime).GetField(
                "_services",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var dacLibraryType = assembly.GetType("Microsoft.Diagnostics.Runtime.DacLibrary");
            if (servicesField?.GetValue(runtime) is not IServiceProvider services)
            {
                code = "dbi-service-unavailable";
                return E_NOINTERFACE;
            }

            if (dacLibraryType is null)
            {
                code = "dbi-library-type-unavailable";
                return E_NOINTERFACE;
            }

            if (services.GetService(dacLibraryType) is not { } library)
            {
                code = "dbi-library-unavailable";
                return E_NOINTERFACE;
            }

            var owningLibrary = dacLibraryType.GetProperty(
                    "OwningLibrary",
                    BindingFlags.Instance | BindingFlags.Public)
                ?.GetValue(library);
            var libraryHandle = owningLibrary?.GetType().GetField(
                    "_library",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.GetValue(owningLibrary);
            var dacDataTarget = dacLibraryType.GetProperty(
                    "DacDataTarget",
                    BindingFlags.Instance | BindingFlags.Public)
                ?.GetValue(library);
            var dataTargetComType = assembly.GetType(
                "Microsoft.Diagnostics.Runtime.DacInterface.DacDataTargetCOM");
            var createDataTarget = dataTargetComType?.GetMethod(
                "CreateIDacDataTarget",
                BindingFlags.Static | BindingFlags.Public);
            if (libraryHandle is not IntPtr nativeLibrary || nativeLibrary == IntPtr.Zero)
            {
                code = "dbi-native-library-unavailable";
                return E_NOINTERFACE;
            }

            if (dacDataTarget is null)
            {
                code = "dbi-data-target-unavailable";
                return E_NOINTERFACE;
            }

            if (dataTargetComType is null || createDataTarget is null)
            {
                code = "dbi-data-target-adapter-unavailable";
                return E_NOINTERFACE;
            }

            if (createDataTarget.Invoke(null, [dacDataTarget]) is not IntPtr dataTargetInterface ||
                dataTargetInterface == IntPtr.Zero)
            {
                code = "dbi-data-target-interface-unavailable";
                return E_NOINTERFACE;
            }

            try
            {
                var export = NativeLibrary.GetExport(nativeLibrary, "CLRDataCreateInstance");
                var create = Marshal.GetDelegateForFunctionPointer<CreateInstanceDelegate>(export);
                var interfaceId = DbiInterfaceId;
                var hresult = create(ref interfaceId, dataTargetInterface, out dbi);
                code = hresult == S_OK && dbi != IntPtr.Zero
                    ? "dbi-interface-created"
                    : "dbi-factory-declined-interface";
                return hresult;
            }
            finally
            {
                _ = Marshal.Release(dataTargetInterface);
                GC.KeepAlive(dacDataTarget);
            }
        }
        catch (Exception exception) when (
            exception is TargetInvocationException or InvalidOperationException or ArgumentException or
            EntryPointNotFoundException or MarshalDirectiveException)
        {
            code = $"dbi-interface-exception-{exception.GetType().Name}";
            return Marshal.GetHRForException(exception);
        }
    }

    private static void CaptureTypeArgument(IntPtr typeData, IntPtr userData)
    {
        if (typeData == IntPtr.Zero || userData == IntPtr.Zero)
        {
            return;
        }

        var state = AssertState(userData);
        if (state.Arguments.Count >= MaximumTypeArguments)
        {
            state.BoundReached = true;
            return;
        }

        var data = Marshal.PtrToStructure<ExpandedTypeData>(typeData);
        state.Arguments.Add(new W8FrameDbiTypeArgument(
            data.ElementType,
            unchecked((int)data.MetadataToken),
            data.AssemblyAddress,
            data.TypeHandle));
    }

    private static CallbackState AssertState(IntPtr userData) =>
        (CallbackState)GCHandle.FromIntPtr(userData).Target!;

    private static W8FrameProbeDisposition ToDisposition(int hresult) => hresult switch
    {
        S_OK => W8FrameProbeDisposition.Exact,
        E_NOTIMPL => W8FrameProbeDisposition.NotImplemented,
        E_NOINTERFACE => W8FrameProbeDisposition.NoInterface,
        _ => W8FrameProbeDisposition.Failed,
    };

    private static T GetVtableDelegate<T>(IntPtr self, int interfaceMethodIndex)
        where T : Delegate
    {
        var vtable = Marshal.ReadIntPtr(self);
        var method = Marshal.ReadIntPtr(
            vtable,
            checked((3 + interfaceMethodIndex) * IntPtr.Size));
        return Marshal.GetDelegateForFunctionPointer<T>(method);
    }

    private sealed class CallbackState
    {
        internal List<W8FrameDbiTypeArgument> Arguments { get; } = [];

        internal bool BoundReached { get; set; }
    }

    [StructLayout(LayoutKind.Explicit, Size = 40)]
    private struct ExpandedTypeData
    {
        [FieldOffset(0)]
        internal int ElementType;

        [FieldOffset(8)]
        internal uint MetadataToken;

        [FieldOffset(16)]
        internal ulong AssemblyAddress;

        [FieldOffset(24)]
        internal ulong TypeHandle;
    }

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int CreateInstanceDelegate(
        ref Guid interfaceId,
        IntPtr dataTarget,
        out IntPtr instance);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int ResolveExactGenericArgsTokenDelegate(
        IntPtr self,
        uint exactTokenIndex,
        ulong rawToken,
        out ulong resolvedToken);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void ExpandedTypeDataCallback(IntPtr typeData, IntPtr userData);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int EnumerateMethodDescParamsDelegate(
        IntPtr self,
        ulong methodDescriptor,
        ulong genericToken,
        out uint declaringTypeArgumentCount,
        ExpandedTypeDataCallback callback,
        IntPtr userData);
}
