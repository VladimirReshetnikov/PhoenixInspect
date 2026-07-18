using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.InteropServices;
using Interpreter.Core.Abstractions;
using Microsoft.Diagnostics.Runtime;

namespace Interpreter.IntegrationTests;

internal enum W8FrameProbeDisposition
{
    Exact,
    CanonicalSubstitution,
    LexicallyInactive,
    Unavailable,
    NotImplemented,
    NoInterface,
    Failed,
    Invalid,
    BoundReached,
}

internal enum W8FrameLocationKind
{
    NotReported,
    Memory,
    RegisterWithoutIdentity,
    ZeroLocations,
    SplitLocations,
    Unknown,
}

internal sealed record W8FrameTypeIdentity(
    W8FrameProbeDisposition Disposition,
    int HResult,
    Guid ModuleVersionId,
    int TypeDefinitionToken);

internal sealed record W8FrameLocationPart(uint Flags, ulong Argument);

internal sealed record W8FrameValueObservation(
    W8FrameProbeDisposition Disposition,
    int HResult,
    W8FrameLocationKind LocationKind,
    ulong Size,
    W8FrameTypeIdentity? Type,
    ImmutableArray<W8FrameLocationPart> Locations);

internal sealed record W8FrameVariableObservation(
    int Index,
    int AcquisitionHResult,
    W8FrameValueObservation Value);

internal sealed record W8FrameGenericArgumentObservation(
    int Index,
    W8FrameTypeIdentity Identity);

internal sealed record W8FrameLegacyObservation(
    W8FrameProbeDisposition SurfaceDisposition,
    int SurfaceHResult,
    int FrameHResult,
    ImmutableArray<byte> ContextBytes,
    string ContextSha256,
    int ArgumentCountHResult,
    ImmutableArray<W8FrameVariableObservation> Arguments,
    int LocalCountHResult,
    ImmutableArray<W8FrameVariableObservation> Locals,
    int FrameTypeArgumentCountHResult,
    ImmutableArray<W8FrameGenericArgumentObservation> FrameTypeArguments,
    int MethodHResult,
    int MethodTokenHResult,
    int MethodDefinitionToken,
    Guid MethodModuleVersionId,
    int MethodTypeArgumentCountHResult,
    ImmutableArray<W8FrameGenericArgumentObservation> MethodTypeArguments,
    int ExactGenericTokenInterfaceHResult,
    int ExactGenericTokenHResult,
    W8FrameValueObservation? ExactGenericToken,
    ImmutableArray<string> CanonicalLines)
{
    internal static W8FrameLegacyObservation SurfaceUnavailable(
        W8FrameProbeDisposition disposition,
        int hresult,
        string code) =>
        new(
            disposition,
            hresult,
            hresult,
            ImmutableArray<byte>.Empty,
            string.Empty,
            hresult,
            ImmutableArray<W8FrameVariableObservation>.Empty,
            hresult,
            ImmutableArray<W8FrameVariableObservation>.Empty,
            hresult,
            ImmutableArray<W8FrameGenericArgumentObservation>.Empty,
            hresult,
            hresult,
            0,
            Guid.Empty,
            hresult,
            ImmutableArray<W8FrameGenericArgumentObservation>.Empty,
            hresult,
            hresult,
            null,
            [$"surface|{code}|{disposition}|{hresult:x8}"]);
}

internal static class W8FrameLegacyInteropProbe
{
    private const int S_OK = 0;
    private const int S_FALSE = 1;
    private const int E_NOTIMPL = unchecked((int)0x80004001);
    private const int E_NOINTERFACE = unchecked((int)0x80004002);
    private const int MaximumWalkFrames = 256;
    private const int MaximumArguments = 64;
    private const int MaximumLocals = 256;
    private const int MaximumTypeArguments = 64;
    private const int MaximumLocations = 16;
    private const uint StackWalkFlags = 0x0f;
    private static readonly Guid Frame2InterfaceId =
        new("1C4D9A4B-702D-4CF6-B290-1DB6F43050D0");

    internal static W8FrameLegacyObservation Observe(
        ClrRuntime runtime,
        ClrThread selectedThread,
        ulong selectedInstructionPointer,
        ulong selectedStackPointer,
        Architecture architecture)
    {
        var assembly = typeof(ClrRuntime).Assembly;
        var servicesField = typeof(ClrRuntime).GetField(
            "_services",
            BindingFlags.Instance | BindingFlags.NonPublic);
        var dacLibraryType = assembly.GetType("Microsoft.Diagnostics.Runtime.DacLibrary");
        if (servicesField?.GetValue(runtime) is not IServiceProvider services || dacLibraryType is null)
        {
            return W8FrameLegacyObservation.SurfaceUnavailable(
                W8FrameProbeDisposition.Unavailable,
                E_NOINTERFACE,
                "legacy-service-absent");
        }

        var library = services.GetService(dacLibraryType);
        if (library is null)
        {
            return W8FrameLegacyObservation.SurfaceUnavailable(
                W8FrameProbeDisposition.Unavailable,
                E_NOINTERFACE,
                "legacy-library-absent");
        }

        object? process = null;
        object? stackWalk = null;
        try
        {
            process = dacLibraryType.GetMethod(
                    "CreateClrDataProcess",
                    BindingFlags.Instance | BindingFlags.Public)
                ?.Invoke(library, null);
            if (process is null)
            {
                return W8FrameLegacyObservation.SurfaceUnavailable(
                    W8FrameProbeDisposition.Unavailable,
                    E_NOINTERFACE,
                    "legacy-process-absent");
            }

            stackWalk = process.GetType().GetMethod(
                    "CreateStackWalk",
                    BindingFlags.Instance | BindingFlags.Public,
                    binder: null,
                    [typeof(uint), typeof(uint)],
                    modifiers: null)
                ?.Invoke(process, [selectedThread.OSThreadId, StackWalkFlags]);
            if (stackWalk is null)
            {
                return W8FrameLegacyObservation.SurfaceUnavailable(
                    W8FrameProbeDisposition.Unavailable,
                    E_NOINTERFACE,
                    "legacy-stackwalk-absent");
            }

            var stackWalkSelf = ReadSelf(stackWalk);
            if (stackWalkSelf == IntPtr.Zero)
            {
                return W8FrameLegacyObservation.SurfaceUnavailable(
                    W8FrameProbeDisposition.Invalid,
                    E_NOINTERFACE,
                    "legacy-stackwalk-self-zero");
            }

            return WalkToSelectedFrame(
                stackWalkSelf,
                selectedInstructionPointer,
                selectedStackPointer,
                architecture);
        }
        catch (Exception exception) when (
            exception is TargetInvocationException or InvalidOperationException or ArgumentException or
            MemberAccessException or MarshalDirectiveException)
        {
            return W8FrameLegacyObservation.SurfaceUnavailable(
                W8FrameProbeDisposition.Failed,
                Marshal.GetHRForException(exception),
                "legacy-reflection-call-failed");
        }
        finally
        {
            (stackWalk as IDisposable)?.Dispose();
            (process as IDisposable)?.Dispose();
        }
    }

    private static W8FrameLegacyObservation WalkToSelectedFrame(
        IntPtr stackWalk,
        ulong selectedInstructionPointer,
        ulong selectedStackPointer,
        Architecture architecture)
    {
        var (contextSize, contextFlags, instructionPointerOffset, stackPointerOffset) =
            ContextLayout(architecture);
        if (contextSize == 0)
        {
            return W8FrameLegacyObservation.SurfaceUnavailable(
                W8FrameProbeDisposition.NotImplemented,
                E_NOTIMPL,
                "context-layout-not-admitted");
        }

        var getContext = GetVtableDelegate<GetContextDelegate>(stackWalk, 0);
        var next = GetVtableDelegate<NoArgumentDelegate>(stackWalk, 2);
        var getFrame = GetVtableDelegate<GetInterfaceDelegate>(stackWalk, 5);
        var context = new byte[contextSize];
        for (var frameIndex = 0; frameIndex < MaximumWalkFrames; frameIndex++)
        {
            Array.Clear(context);
            var contextHResult = getContext(
                stackWalk,
                contextFlags,
                context.Length,
                out var actualContextSize,
                context);
            if (contextHResult != S_OK || actualContextSize < contextSize)
            {
                return W8FrameLegacyObservation.SurfaceUnavailable(
                    ToDisposition(contextHResult),
                    contextHResult,
                    "legacy-context-unavailable");
            }

            var instructionPointer = ReadPointer(context, instructionPointerOffset, architecture);
            var stackPointer = ReadPointer(context, stackPointerOffset, architecture);
            if (instructionPointer == selectedInstructionPointer && stackPointer == selectedStackPointer)
            {
                var frameHResult = getFrame(stackWalk, out var frame);
                if (frameHResult != S_OK || frame == IntPtr.Zero)
                {
                    return W8FrameLegacyObservation.SurfaceUnavailable(
                        ToDisposition(frameHResult),
                        frameHResult,
                        "legacy-frame-unavailable");
                }

                try
                {
                    return ObserveFrame(
                        frame,
                        frameHResult,
                        ImmutableArray.CreateRange(context),
                        CanonicalReplayEncoding.ComputeSha256(context));
                }
                finally
                {
                    Release(frame);
                }
            }

            var nextHResult = next(stackWalk);
            if (nextHResult == S_FALSE)
            {
                return W8FrameLegacyObservation.SurfaceUnavailable(
                    W8FrameProbeDisposition.Unavailable,
                    nextHResult,
                    "legacy-selected-frame-absent");
            }

            if (nextHResult != S_OK)
            {
                return W8FrameLegacyObservation.SurfaceUnavailable(
                    ToDisposition(nextHResult),
                    nextHResult,
                    "legacy-stackwalk-failed");
            }
        }

        return W8FrameLegacyObservation.SurfaceUnavailable(
            W8FrameProbeDisposition.BoundReached,
            S_FALSE,
            "legacy-frame-bound-reached");
    }

    private static W8FrameLegacyObservation ObserveFrame(
        IntPtr frame,
        int frameHResult,
        ImmutableArray<byte> contextBytes,
        string contextSha256)
    {
        var argumentCountHResult = ReadCount(frame, 3, MaximumArguments, out var argumentCount);
        var arguments = ReadVariables(frame, 4, argumentCountHResult, argumentCount, MaximumArguments);
        var localCountHResult = ReadCount(frame, 5, MaximumLocals, out var localCount);
        var locals = ReadVariables(frame, 6, localCountHResult, localCount, MaximumLocals);

        var frameTypeArgumentCountHResult = ReadCount(
            frame,
            10,
            MaximumTypeArguments,
            out var frameTypeArgumentCount);
        var frameTypeArguments = ReadTypeArguments(
            frame,
            11,
            frameTypeArgumentCountHResult,
            frameTypeArgumentCount);

        var getMethod = GetVtableDelegate<GetInterfaceDelegate>(frame, 8);
        var methodHResult = getMethod(frame, out var method);
        var methodTokenHResult = methodHResult;
        var methodToken = 0;
        var methodModuleVersionId = Guid.Empty;
        var methodTypeArgumentCountHResult = methodHResult;
        var methodTypeArguments = ImmutableArray<W8FrameGenericArgumentObservation>.Empty;
        if (methodHResult == S_OK && method != IntPtr.Zero)
        {
            try
            {
                methodTokenHResult = ReadMethodIdentity(
                    method,
                    out methodToken,
                    out methodModuleVersionId);
                methodTypeArgumentCountHResult = ReadCount(
                    method,
                    7,
                    MaximumTypeArguments,
                    out var methodTypeArgumentCount);
                methodTypeArguments = ReadTypeArguments(
                    method,
                    8,
                    methodTypeArgumentCountHResult,
                    methodTypeArgumentCount);
            }
            finally
            {
                Release(method);
            }
        }

        var exactGenericTokenInterfaceHResult = QueryInterface(
            frame,
            Frame2InterfaceId,
            out var frame2);
        var exactGenericTokenHResult = exactGenericTokenInterfaceHResult;
        W8FrameValueObservation? exactGenericToken = null;
        if (exactGenericTokenInterfaceHResult == S_OK && frame2 != IntPtr.Zero)
        {
            try
            {
                var getToken = GetVtableDelegate<GetInterfaceDelegate>(frame2, 0);
                exactGenericTokenHResult = getToken(frame2, out var token);
                if (exactGenericTokenHResult == S_OK && token != IntPtr.Zero)
                {
                    try
                    {
                        exactGenericToken = ObserveValue(token);
                    }
                    finally
                    {
                        Release(token);
                    }
                }
            }
            finally
            {
                Release(frame2);
            }
        }

        var lines = ImmutableArray.CreateBuilder<string>();
        lines.Add($"surface|exact|{frameHResult:x8}");
        lines.Add($"context|{contextBytes.Length}|{contextSha256}");
        lines.Add($"arguments|{argumentCountHResult:x8}|{arguments.Length}");
        foreach (var argument in arguments)
        {
            AppendVariable(lines, "argument", argument);
        }

        lines.Add($"locals|{localCountHResult:x8}|{locals.Length}");
        foreach (var local in locals)
        {
            AppendVariable(lines, "local", local);
        }

        lines.Add(
            $"frame-type-arguments|{frameTypeArgumentCountHResult:x8}|{frameTypeArguments.Length}");
        AppendTypeArguments(lines, "frame-type-argument", frameTypeArguments);
        lines.Add(
            $"method|{methodHResult:x8}|{methodTokenHResult:x8}|{methodToken:x8}|" +
            $"{methodModuleVersionId:D}");
        lines.Add(
            $"method-type-arguments|{methodTypeArgumentCountHResult:x8}|{methodTypeArguments.Length}");
        AppendTypeArguments(lines, "method-type-argument", methodTypeArguments);
        lines.Add(
            $"exact-generic-token|{exactGenericTokenInterfaceHResult:x8}|" +
            $"{exactGenericTokenHResult:x8}|{FormatValue(exactGenericToken)}");

        return new W8FrameLegacyObservation(
            W8FrameProbeDisposition.Exact,
            S_OK,
            frameHResult,
            contextBytes,
            contextSha256,
            argumentCountHResult,
            arguments,
            localCountHResult,
            locals,
            frameTypeArgumentCountHResult,
            frameTypeArguments,
            methodHResult,
            methodTokenHResult,
            methodToken,
            methodModuleVersionId,
            methodTypeArgumentCountHResult,
            methodTypeArguments,
            exactGenericTokenInterfaceHResult,
            exactGenericTokenHResult,
            exactGenericToken,
            lines.ToImmutable());
    }

    private static ImmutableArray<W8FrameVariableObservation> ReadVariables(
        IntPtr frame,
        int methodIndex,
        int countHResult,
        int count,
        int maximum)
    {
        if (countHResult != S_OK || count < 0 || count > maximum)
        {
            return ImmutableArray<W8FrameVariableObservation>.Empty;
        }

        var getVariable = GetVtableDelegate<GetVariableDelegate>(frame, methodIndex);
        var builder = ImmutableArray.CreateBuilder<W8FrameVariableObservation>(count);
        var nameBuffer = Marshal.AllocHGlobal(512 * sizeof(char));
        try
        {
            for (var index = 0; index < count; index++)
            {
                var hresult = getVariable(
                    frame,
                    checked((uint)index),
                    out var value,
                    512,
                    out _,
                    nameBuffer);
                if (hresult == S_OK && value != IntPtr.Zero)
                {
                    try
                    {
                        builder.Add(new W8FrameVariableObservation(index, hresult, ObserveValue(value)));
                    }
                    finally
                    {
                        Release(value);
                    }
                }
                else
                {
                    builder.Add(new W8FrameVariableObservation(
                        index,
                        hresult,
                        EmptyValue(ToDisposition(hresult), hresult)));
                }
            }
        }
        finally
        {
            Marshal.FreeHGlobal(nameBuffer);
        }

        return builder.ToImmutable();
    }

    private static W8FrameValueObservation ObserveValue(IntPtr value)
    {
        var getSize = GetVtableDelegate<GetUInt64Delegate>(value, 2);
        var sizeHResult = getSize(value, out var size);
        if (sizeHResult != S_OK)
        {
            size = 0;
        }

        var getType = GetVtableDelegate<GetInterfaceDelegate>(value, 5);
        var typeHResult = getType(value, out var typeInstance);
        W8FrameTypeIdentity? type = null;
        if (typeHResult == S_OK && typeInstance != IntPtr.Zero)
        {
            try
            {
                type = ReadTypeIdentity(typeInstance);
            }
            finally
            {
                Release(typeInstance);
            }
        }

        var getNumLocations = GetVtableDelegate<GetUInt32Delegate>(value, 25);
        var locationsHResult = getNumLocations(value, out var locationCount);
        if (locationsHResult != S_OK)
        {
            return new W8FrameValueObservation(
                ToDisposition(locationsHResult),
                locationsHResult,
                W8FrameLocationKind.NotReported,
                size,
                type,
                ImmutableArray<W8FrameLocationPart>.Empty);
        }

        if (locationCount > MaximumLocations)
        {
            return new W8FrameValueObservation(
                W8FrameProbeDisposition.BoundReached,
                locationsHResult,
                W8FrameLocationKind.SplitLocations,
                size,
                type,
                ImmutableArray<W8FrameLocationPart>.Empty);
        }

        var getLocation = GetVtableDelegate<GetLocationDelegate>(value, 26);
        var locations = ImmutableArray.CreateBuilder<W8FrameLocationPart>(checked((int)locationCount));
        for (uint index = 0; index < locationCount; index++)
        {
            var hresult = getLocation(value, index, out var flags, out var argument);
            if (hresult != S_OK)
            {
                return new W8FrameValueObservation(
                    ToDisposition(hresult),
                    hresult,
                    W8FrameLocationKind.NotReported,
                    size,
                    type,
                    locations.ToImmutable());
            }

            locations.Add(new W8FrameLocationPart(flags, argument));
        }

        var frozenLocations = locations.ToImmutable();
        var locationKind = frozenLocations.Length switch
        {
            0 => W8FrameLocationKind.ZeroLocations,
            > 1 => W8FrameLocationKind.SplitLocations,
            _ when frozenLocations[0].Flags == 0 && frozenLocations[0].Argument != 0 =>
                W8FrameLocationKind.Memory,
            _ when frozenLocations[0].Flags == 1 => W8FrameLocationKind.RegisterWithoutIdentity,
            _ => W8FrameLocationKind.Unknown,
        };
        var disposition = locationKind == W8FrameLocationKind.Memory
            ? W8FrameProbeDisposition.Exact
            : W8FrameProbeDisposition.Unavailable;
        return new W8FrameValueObservation(
            disposition,
            locationsHResult,
            locationKind,
            size,
            type,
            frozenLocations);
    }

    private static ImmutableArray<W8FrameGenericArgumentObservation> ReadTypeArguments(
        IntPtr owner,
        int methodIndex,
        int countHResult,
        int count)
    {
        if (countHResult != S_OK || count < 0 || count > MaximumTypeArguments)
        {
            return ImmutableArray<W8FrameGenericArgumentObservation>.Empty;
        }

        var getTypeArgument = GetVtableDelegate<GetIndexedInterfaceDelegate>(owner, methodIndex);
        var builder = ImmutableArray.CreateBuilder<W8FrameGenericArgumentObservation>(count);
        for (var index = 0; index < count; index++)
        {
            var hresult = getTypeArgument(owner, checked((uint)index), out var typeInstance);
            if (hresult == S_OK && typeInstance != IntPtr.Zero)
            {
                try
                {
                    builder.Add(new W8FrameGenericArgumentObservation(
                        index,
                        ReadTypeIdentity(typeInstance)));
                }
                finally
                {
                    Release(typeInstance);
                }
            }
            else
            {
                builder.Add(new W8FrameGenericArgumentObservation(
                    index,
                    new W8FrameTypeIdentity(
                        ToDisposition(hresult),
                        hresult,
                        Guid.Empty,
                        0)));
            }
        }

        return builder.ToImmutable();
    }

    private static W8FrameTypeIdentity ReadTypeIdentity(IntPtr typeInstance)
    {
        var getDefinition = GetVtableDelegate<GetInterfaceDelegate>(typeInstance, 15);
        var definitionHResult = getDefinition(typeInstance, out var definition);
        if (definitionHResult != S_OK || definition == IntPtr.Zero)
        {
            return new W8FrameTypeIdentity(
                ToDisposition(definitionHResult),
                definitionHResult,
                Guid.Empty,
                0);
        }

        try
        {
            var getToken = GetVtableDelegate<GetTokenAndScopeDelegate>(definition, 12);
            var tokenHResult = getToken(definition, out var token, out var module);
            if (tokenHResult != S_OK || module == IntPtr.Zero)
            {
                return new W8FrameTypeIdentity(
                    ToDisposition(tokenHResult),
                    tokenHResult,
                    Guid.Empty,
                    token);
            }

            try
            {
                var moduleHResult = ReadModuleVersionId(module, out var moduleVersionId);
                return new W8FrameTypeIdentity(
                    ToDisposition(moduleHResult),
                    moduleHResult,
                    moduleVersionId,
                    token);
            }
            finally
            {
                Release(module);
            }
        }
        finally
        {
            Release(definition);
        }
    }

    private static int ReadMethodIdentity(
        IntPtr method,
        out int token,
        out Guid moduleVersionId)
    {
        var getToken = GetVtableDelegate<GetTokenAndScopeDelegate>(method, 2);
        var tokenHResult = getToken(method, out token, out var module);
        moduleVersionId = Guid.Empty;
        if (tokenHResult != S_OK || module == IntPtr.Zero)
        {
            return tokenHResult;
        }

        try
        {
            return ReadModuleVersionId(module, out moduleVersionId);
        }
        finally
        {
            Release(module);
        }
    }

    private static int ReadModuleVersionId(IntPtr module, out Guid moduleVersionId)
    {
        var getVersionId = GetVtableDelegate<GetGuidDelegate>(module, 37);
        return getVersionId(module, out moduleVersionId);
    }

    private static int ReadCount(IntPtr owner, int methodIndex, int maximum, out int count)
    {
        var getCount = GetVtableDelegate<GetUInt32Delegate>(owner, methodIndex);
        var hresult = getCount(owner, out var unsignedCount);
        if (hresult != S_OK)
        {
            count = 0;
            return hresult;
        }

        if (unsignedCount > maximum)
        {
            count = checked((int)Math.Min(unsignedCount, int.MaxValue));
            return S_FALSE;
        }

        count = checked((int)unsignedCount);
        return hresult;
    }

    private static void AppendVariable(
        ImmutableArray<string>.Builder lines,
        string kind,
        W8FrameVariableObservation observation) =>
        lines.Add(
            $"{kind}|{observation.Index}|{observation.AcquisitionHResult:x8}|" +
            FormatValue(observation.Value));

    private static string FormatValue(W8FrameValueObservation? value)
    {
        if (value is null)
        {
            return "absent";
        }

        var type = value.Type is null
            ? "type-absent"
            : $"{value.Type.Disposition}:{value.Type.HResult:x8}:" +
              $"{value.Type.ModuleVersionId:D}:{value.Type.TypeDefinitionToken:x8}";
        var locations = string.Join(
            ",",
            value.Locations.Select(static item => $"{item.Flags:x8}@{item.Argument:x16}"));
        return $"{value.Disposition}|{value.HResult:x8}|{value.LocationKind}|{value.Size}|" +
            $"{type}|{locations}";
    }

    private static void AppendTypeArguments(
        ImmutableArray<string>.Builder lines,
        string kind,
        ImmutableArray<W8FrameGenericArgumentObservation> arguments)
    {
        foreach (var argument in arguments)
        {
            lines.Add(
                $"{kind}|{argument.Index}|{argument.Identity.Disposition}|" +
                $"{argument.Identity.HResult:x8}|{argument.Identity.ModuleVersionId:D}|" +
                $"{argument.Identity.TypeDefinitionToken:x8}");
        }
    }

    private static W8FrameValueObservation EmptyValue(
        W8FrameProbeDisposition disposition,
        int hresult) =>
        new(
            disposition,
            hresult,
            W8FrameLocationKind.NotReported,
            0,
            null,
            ImmutableArray<W8FrameLocationPart>.Empty);

    private static (int Size, uint Flags, int InstructionPointerOffset, int StackPointerOffset)
        ContextLayout(Architecture architecture) => architecture switch
        {
            Architecture.X64 => (1232, 0x0010_003f, 248, 152),
            Architecture.X86 => (716, 0x0001_003f, 184, 196),
            Architecture.Arm => (416, 0, 64, 56),
            Architecture.Arm64 => (912, 0, 264, 256),
            _ when (int)architecture == 9 => (544, 0, 264, 24),
            _ when (int)architecture == 6 => (1312, 0, 264, 32),
            _ => default,
        };

    private static ulong ReadPointer(byte[] context, int offset, Architecture architecture) =>
        architecture is Architecture.X86 or Architecture.Arm
            ? BitConverter.ToUInt32(context, offset)
            : BitConverter.ToUInt64(context, offset);

    private static IntPtr ReadSelf(object wrapper)
    {
        for (var type = wrapper.GetType(); type is not null; type = type.BaseType)
        {
            var property = type.GetProperty(
                "Self",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly);
            if (property?.GetValue(wrapper) is IntPtr value)
            {
                return value;
            }
        }

        return IntPtr.Zero;
    }

    private static T GetVtableDelegate<T>(IntPtr self, int interfaceMethodIndex)
        where T : Delegate
    {
        if (self == IntPtr.Zero)
        {
            throw new ArgumentException("The interface pointer must be nonzero.", nameof(self));
        }

        var vtable = Marshal.ReadIntPtr(self);
        var method = Marshal.ReadIntPtr(
            vtable,
            checked((3 + interfaceMethodIndex) * IntPtr.Size));
        if (method == IntPtr.Zero)
        {
            throw new InvalidOperationException("The selected interface method pointer was zero.");
        }

        return Marshal.GetDelegateForFunctionPointer<T>(method);
    }

    private static int QueryInterface(IntPtr self, Guid interfaceId, out IntPtr result)
    {
        var query = GetIUnknownDelegate<QueryInterfaceDelegate>(self, 0);
        return query(self, in interfaceId, out result);
    }

    private static void Release(IntPtr self)
    {
        if (self != IntPtr.Zero)
        {
            _ = GetIUnknownDelegate<NoArgumentDelegate>(self, 2)(self);
        }
    }

    private static T GetIUnknownDelegate<T>(IntPtr self, int methodIndex)
        where T : Delegate
    {
        var vtable = Marshal.ReadIntPtr(self);
        var method = Marshal.ReadIntPtr(vtable, checked(methodIndex * IntPtr.Size));
        return Marshal.GetDelegateForFunctionPointer<T>(method);
    }

    private static W8FrameProbeDisposition ToDisposition(int hresult) => hresult switch
    {
        S_OK => W8FrameProbeDisposition.Exact,
        S_FALSE => W8FrameProbeDisposition.Unavailable,
        E_NOTIMPL => W8FrameProbeDisposition.NotImplemented,
        E_NOINTERFACE => W8FrameProbeDisposition.NoInterface,
        _ when hresult < 0 => W8FrameProbeDisposition.Failed,
        _ => W8FrameProbeDisposition.Invalid,
    };

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate int GetContextDelegate(
        IntPtr self,
        uint flags,
        int bufferSize,
        out int actualSize,
        [Out] byte[] buffer);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate int NoArgumentDelegate(IntPtr self);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate int GetInterfaceDelegate(IntPtr self, out IntPtr value);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate int GetIndexedInterfaceDelegate(IntPtr self, uint index, out IntPtr value);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate int GetVariableDelegate(
        IntPtr self,
        uint index,
        out IntPtr value,
        uint bufferLength,
        out uint nameLength,
        IntPtr nameBuffer);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate int GetUInt32Delegate(IntPtr self, out uint value);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate int GetUInt64Delegate(IntPtr self, out ulong value);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate int GetLocationDelegate(
        IntPtr self,
        uint index,
        out uint flags,
        out ulong argument);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate int GetTokenAndScopeDelegate(
        IntPtr self,
        out int token,
        out IntPtr module);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate int GetGuidDelegate(IntPtr self, out Guid value);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate int QueryInterfaceDelegate(
        IntPtr self,
        in Guid interfaceId,
        out IntPtr result);
}
