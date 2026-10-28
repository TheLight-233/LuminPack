using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using LuminPack.Utility;

namespace LuminPack.Option
{
    /// <summary>Provides lumin pack writer optional state pool functionality.</summary>
    public static class LuminPackWriterOptionalStatePool
    {
        private static readonly ConcurrentQueue<LuminPackWriterOptionalState> _queue = new();

        /// <summary>Obtains a reusable instance from the pool, creating one when needed.</summary>
        /// <returns>The result of the operation.</returns>
        public static LuminPackWriterOptionalState Rent()
        {
            if (!_queue.TryDequeue(out var state))
            {
                state = new LuminPackWriterOptionalState();
            }

            return state;
        }

        /// <summary>Returns an instance to the pool for reuse.</summary>
        /// <param name="state">The reusable state associated with the operation.</param>
        internal static void Return(LuminPackWriterOptionalState state)
        {
            state.Reset();
            _queue.Enqueue(state);
        }
    }

    /// <summary>Provides lumin pack writer optional state functionality.</summary>
    public sealed class LuminPackWriterOptionalState : IDisposable
    {
        internal static readonly LuminPackWriterOptionalState NullOption = new LuminPackWriterOptionalState(true);

        private uint _nextId;
        /// <summary>Gets or sets the object to ref.</summary>
        private LuminCircleReferenceMap<object, uint> ObjectToRef { get; set; }

        /// <summary>Initializes a new instance of <c>LuminPackWriterOptionalState</c>.</summary>
        public LuminPackWriterOptionalState()
        {
            ObjectToRef = new LuminCircleReferenceMap<object, uint>(ReferenceEqualityComparer.Instance);
            _nextId = 0;

        }

        /// <summary>Initializes a new instance of <c>LuminPackWriterOptionalState</c>.</summary>
        /// <param name="_">The  used by this operation.</param>
        private LuminPackWriterOptionalState(bool _)
        {
            ObjectToRef = new LuminCircleReferenceMap<object, uint>(ReferenceEqualityComparer.Instance);
            _nextId = 0;

        }

        /// <summary>Whether a generated convenience operation currently leases this state from its thread's cache.</summary>
        private bool _leased;

        /// <summary>Leases the calling thread's cached state, or installs a new cached state when it is absent or active.</summary>
        /// <param name="cache">The calling thread's idle-state slot owned by the generated serializer.</param>
        /// <returns>A reset state owned by the caller until <see cref="Release"/>.</returns>
        /// <remarks>The cached instance stays in its slot while leased, so leasing and releasing store no reference. A nested
        /// or concurrent operation that finds the lease receives an independent state, which then becomes the cached one.</remarks>
        [global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static LuminPackWriterOptionalState Lease(ref LuminPackWriterOptionalState? cache)
        {
            var state = cache;
            // Acquire: a release on another thread published its reset before clearing the flag.
            if (state is not null && !Volatile.Read(ref state._leased))
            {
                state._leased = true;
                return state;
            }

            return LeaseSlow(ref cache);
        }

        /// <summary>Creates a leased state and makes it the calling thread's cached state.</summary>
        /// <param name="cache">The calling thread's idle-state slot.</param>
        /// <returns>The new leased state.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static LuminPackWriterOptionalState LeaseSlow(ref LuminPackWriterOptionalState? cache)
        {
            var state = new LuminPackWriterOptionalState();
            state._leased = true;
            cache = state;
            return state;
        }

        /// <summary>Clears the operation's references and ends the lease obtained from <see cref="Lease"/>.</summary>
        [global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Release()
        {
            ResetOperationState();
            Volatile.Write(ref _leased, false);
        }

        /// <summary>Clears state retained by the previous operation so it can be reused.</summary>
        public void Reset()
        {
            ResetOperationState();

        }

        /// <summary>Clears transient writer reference tracking for reuse by the next operation.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ResetOperationState()
        {
            // No reference can be retained before the first id is assigned.
            if (_nextId == 0) return;
            ResetReferences();
        }

        /// <summary>Clears a nonempty reference table without expanding its cleanup into each entry point.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void ResetReferences()
        {
            ObjectToRef.Clear();
            _nextId = 0;
        }

        /// <summary>Obtains the or add reference for the supplied input.</summary>
        /// <param name="value">The value consumed or produced by this operation.</param>
        /// <returns>The result of the operation.</returns>
        public (bool existsReference, uint id) GetOrAddReference(object value)
        {

            ref var id = ref ObjectToRef.GetValueRefOrAddDefault(value, out var exists);
            if (exists)
            {
                return (true, id);
            }
            else
            {
                id = _nextId++;
                return (false, id);
            }
        }

        /// <summary>Releases the resources or pooled state owned by this instance.</summary>
        void IDisposable.Dispose()
        {

        }
    }
}
