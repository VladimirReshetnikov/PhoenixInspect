using System.Runtime.CompilerServices;

namespace PhoenixInspect.W8TestTarget
{
    /// <summary>Supplies the local half of a same-simple-name, cross-assembly lookup pair.</summary>
    /// <remarks>This fixture type remains physically distinct from the external equally named candidate.</remarks>
    public sealed class SharedSpelling
    {
        /// <summary>Initializes the local candidate with its exact retained marker.</summary>
        /// <param name="marker">The stable value distinguishing this physical candidate.</param>
        public SharedSpelling(int marker) => Marker = marker;

        /// <summary>Gets the retained local-candidate marker.</summary>
        public int Marker { get; }
    }

    /// <summary>Supplies simultaneous per-thread field observations over one exact static declaration.</summary>
    /// <remarks>This is a physical runtime fixture and not a thread-storage product contract.</remarks>
    public static class ThreadRelativeProfile
    {
        /// <summary>Starts two retained workers, waits for both assignments, and pauses the coordinating thread.</summary>
        /// <param name="profile">The selected target profile retained in the coordinating frame.</param>
        /// <returns>A process exit code if the pause unexpectedly returns.</returns>
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public static int Run(string profile)
        {
            using var ready = new CountdownEvent(2);
            var firstState = new ThreadRelativeState(
                unchecked((int)0xE1017A01),
                unchecked((int)0xE1117A11),
                ready);
            var secondState = new ThreadRelativeState(
                unchecked((int)0xE2027A02),
                unchecked((int)0xE2127A12),
                ready);
            var first = new Thread(ThreadRelativeWorker)
            {
                IsBackground = true,
                Name = "w8-thread-relative-first",
            };
            var second = new Thread(ThreadRelativeWorker)
            {
                IsBackground = true,
                Name = "w8-thread-relative-second",
            };

            first.Start(firstState);
            second.Start(secondState);
            ready.Wait();

            Console.WriteLine("READY");
            Console.Out.Flush();
            Thread.Sleep(Timeout.Infinite);

            GC.KeepAlive(profile);
            GC.KeepAlive(first);
            GC.KeepAlive(second);
            GC.KeepAlive(firstState);
            GC.KeepAlive(secondState);
            return 0;
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        private static void ThreadRelativeWorker(object? stateObject)
        {
            var state = (ThreadRelativeState)stateObject!;
            GenericSlot<RequestContext>.ThreadSentinel = state.RequestSentinel;
            GenericSlot<BatchContext>.ThreadSentinel = state.BatchSentinel;
            var retainedRequestSentinel = GenericSlot<RequestContext>.ThreadSentinel;
            var retainedBatchSentinel = GenericSlot<BatchContext>.ThreadSentinel;
            state.Ready.Signal();
            Thread.Sleep(Timeout.Infinite);
            GC.KeepAlive(retainedRequestSentinel);
            GC.KeepAlive(retainedBatchSentinel);
            GC.KeepAlive(state);
        }

        private sealed record ThreadRelativeState(
            int RequestSentinel,
            int BatchSentinel,
            CountdownEvent Ready);
    }

    /// <summary>Supplies one attributable context-relative field observation.</summary>
    /// <remarks>This is a physical runtime fixture and not a context-storage product contract.</remarks>
    public static class ContextRelativeProfile
    {
        /// <summary>Assigns the active-context value and pauses with the exact result retained.</summary>
        /// <param name="profile">The selected target profile retained in the frame.</param>
        /// <returns>A process exit code if the pause unexpectedly returns.</returns>
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public static int Run(string profile)
        {
            ContextRelativeStorage.ContextSentinel = unchecked((int)0xE3037A03);
            var retainedSentinel = ContextRelativeStorage.ContextSentinel;

            Console.WriteLine("READY");
            Console.Out.Flush();
            Thread.Sleep(Timeout.Infinite);

            GC.KeepAlive(profile);
            GC.KeepAlive(retainedSentinel);
            return 0;
        }
    }

    /// <summary>Supplies an optimized selected frame beside the explicit non-optimized controls.</summary>
    /// <remarks>This is a JIT/PDB evidence fixture and not a frame-value product contract.</remarks>
    public static class OptimizedFrameProfile
    {
        /// <summary>Pauses in a no-inline method whose body remains eligible for Release optimization.</summary>
        /// <param name="profile">The selected target profile retained in the frame.</param>
        /// <param name="request">The reference parameter participating in the optimized value graph.</param>
        /// <param name="number">The value parameter participating in the optimized value graph.</param>
        /// <returns>A process exit code if the pause unexpectedly returns.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static int Run(string profile, RequestContext request, int number)
        {
            var foldedNumber = (number ^ request.Name.Length) + PrimitiveStorage.Int16;
            var selectedReference = foldedNumber < 0 ? request : PrimitiveStorage.NullReference;
            var projectedNumber = selectedReference is null ? foldedNumber : foldedNumber ^ selectedReference.Name.Length;

            Console.WriteLine("READY");
            Console.Out.Flush();
            Thread.Sleep(Timeout.Infinite);

            GC.KeepAlive(profile);
            GC.KeepAlive(request);
            GC.KeepAlive(foldedNumber);
            GC.KeepAlive(selectedReference);
            GC.KeepAlive(projectedNumber);
            return 0;
        }
    }

    /// <summary>Supplies disjoint lexical locals that test whether the pinned compiler reuses one named slot.</summary>
    /// <remarks>
    /// The current compiler emits distinct slots, which is an evidence-backed result of this Portable-PDB fixture
    /// rather than a frame-value product contract.
    /// </remarks>
    public static class SlotReuseProfile
    {
        /// <summary>Ends one addressed local scope before pausing inside a second addressed local scope.</summary>
        /// <param name="profile">The selected target profile retained in the active scope.</param>
        /// <param name="number">The deterministic input used to distinguish the two local values.</param>
        /// <returns>A process exit code if the pause unexpectedly returns.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static int Run(string profile, int number)
        {
            {
                var inactiveSlot = number ^ unchecked((int)0xE4047A04);
                ObserveAddressedValue(ref inactiveSlot);
            }

            {
                var activeSlot = number ^ unchecked((int)0xE5057A05);
                Console.WriteLine("READY");
                Console.Out.Flush();
                Thread.Sleep(Timeout.Infinite);

                ObserveAddressedValue(ref activeSlot);
                GC.KeepAlive(profile);
                return activeSlot;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        private static void ObserveAddressedValue(ref int value)
        {
            if (value == int.MinValue)
            {
                throw new InvalidOperationException("The disjoint-local sentinel reached its excluded value.");
            }
        }
    }

    /// <summary>Supplies selected-frame retention for the explicitly named RVA-backed fields.</summary>
    /// <remarks>This is a PE/runtime fixture and not a module-storage product contract.</remarks>
    public static class NamedRvaProfile
    {
        /// <summary>Pauses with both fixed-width values and their defining module retained.</summary>
        /// <param name="profile">The selected target profile retained in the frame.</param>
        /// <param name="sentinel">The exact four-byte field value read from the named PE location.</param>
        /// <param name="wideSentinel">The exact eight-byte field value read from the named PE location.</param>
        /// <returns>A process exit code if the pause unexpectedly returns.</returns>
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public static int Run(string profile, int sentinel, long wideSentinel)
        {
            if (sentinel != 0x21047A61 ||
                wideSentinel != unchecked((long)0xD3E5A71942087A92UL))
            {
                Console.WriteLine("RVA_VALUE_MISMATCH");
                Console.Out.Flush();
                return 95;
            }

            var combined = unchecked((ulong)(uint)sentinel) ^ unchecked((ulong)wideSentinel);
            Console.WriteLine("READY");
            Console.Out.Flush();
            Thread.Sleep(Timeout.Infinite);

            GC.KeepAlive(profile);
            GC.KeepAlive(sentinel);
            GC.KeepAlive(wideSentinel);
            GC.KeepAlive(combined);
            return 0;
        }
    }

    /// <summary>Supplies a query expression whose generated lambdas retain the source range-variable spelling.</summary>
    /// <remarks>This is a compiler/PDB fixture and not a lexical-binding product contract.</remarks>
    public static class QueryRangeProfile
    {
        /// <summary>Executes the query until its selected range element reaches the pause method.</summary>
        /// <param name="profile">The selected target profile captured by the generated selector.</param>
        /// <param name="request">The first exact query element.</param>
        /// <returns>A process exit code if the pause unexpectedly returns.</returns>
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public static int Run(string profile, RequestContext request)
        {
            var source = new[]
            {
                request,
                new RequestContext("query-nonmatching-element"),
                new RequestContext(request.Name),
            };
            var result =
                (from queryRangeVariable in source
                 where queryRangeVariable.Name == request.Name
                 select PauseFromRange(profile, queryRangeVariable))
                .First();

            GC.KeepAlive(result);
            return 94;
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        private static int PauseFromRange(string profile, RequestContext queryRangeVariable)
        {
            var retainedName = queryRangeVariable.Name;
            Console.WriteLine("READY");
            Console.Out.Flush();
            Thread.Sleep(Timeout.Infinite);

            GC.KeepAlive(profile);
            GC.KeepAlive(queryRangeVariable);
            GC.KeepAlive(retainedName);
            return retainedName.Length;
        }
    }
}

namespace PhoenixInspect.W8AmbiguityEvidence
{
    using PhoenixInspect.W8AliasTarget;
    using PhoenixInspect.W8TestTarget;

    /// <summary>Supplies two imported, same-simple-name candidates with distinct assembly identities.</summary>
    /// <remarks>This is a compiler/PDB fixture and not a name-resolution product contract.</remarks>
    public static class CrossAssemblyAmbiguityProfile
    {
        /// <summary>Materializes both physical candidates and pauses under the import scope that makes them peers.</summary>
        /// <param name="profile">The selected target profile retained in the frame.</param>
        /// <returns>A process exit code if the pause unexpectedly returns.</returns>
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public static int Run(string profile)
        {
            var localCandidate = new global::PhoenixInspect.W8TestTarget.SharedSpelling(0x1F017A01);
            var externalCandidate = new global::PhoenixInspect.W8AliasTarget.SharedSpelling(0x1F027A02);
            var distinctCandidates = localCandidate.Marker != externalCandidate.Marker;

            Console.WriteLine("READY");
            Console.Out.Flush();
            Thread.Sleep(Timeout.Infinite);

            GC.KeepAlive(profile);
            GC.KeepAlive(localCandidate);
            GC.KeepAlive(externalCandidate);
            GC.KeepAlive(distinctCandidates);
            return 0;
        }
    }
}
