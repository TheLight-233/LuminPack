using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using LuminPack.Code;
using LuminPack.Utility;

namespace LuminPack.Option
{
    /// <summary>Provides lumin pack reader optional state pool functionality.</summary>
    public static class LuminPackReaderOptionalStatePool
    {
        private static readonly ConcurrentQueue<LuminPackReaderOptionalState> _queue = new();

        /// <summary>Obtains a reusable instance from the pool, creating one when needed.</summary>
        /// <returns>The result of the operation.</returns>
        public static LuminPackReaderOptionalState Rent()
        {
            if (!_queue.TryDequeue(out var state))
            {
                state = new LuminPackReaderOptionalState();
            }

            return state;
        }

        /// <summary>Returns an instance to the pool for reuse.</summary>
        /// <param name="state">The reusable state associated with the operation.</param>
        internal static void Return(LuminPackReaderOptionalState state)
        {
            state.Reset();
            _queue.Enqueue(state);
        }
    }

    /// <summary>Provides lumin pack reader optional state functionality.</summary>
    public sealed class LuminPackReaderOptionalState : IDisposable
    {
        internal static readonly LuminPackReaderOptionalState NullOption = new LuminPackReaderOptionalState(true);

        private readonly LuminCircleReferenceMap<uint, object> refToObject;
        private LuminCircleReferenceMap<object, uint>? reusedObjectToRef;
        private bool reuseIndexInitialized;

        /// <summary>Initializes a new instance of <c>LuminPackReaderOptionalState</c>.</summary>
        public LuminPackReaderOptionalState()
        {
            refToObject = new LuminCircleReferenceMap<uint, object>();

        }

        /// <summary>Initializes a new instance of <c>LuminPackReaderOptionalState</c>.</summary>
        /// <param name="_">The  used by this operation.</param>
        private LuminPackReaderOptionalState(bool _)
        {
            refToObject = new LuminCircleReferenceMap<uint, object>();

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
        public static LuminPackReaderOptionalState Lease(ref LuminPackReaderOptionalState? cache)
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
        private static LuminPackReaderOptionalState LeaseSlow(ref LuminPackReaderOptionalState? cache)
        {
            var state = new LuminPackReaderOptionalState();
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

        /// <summary>Clears transient reader reference tracking for reuse by the next operation.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ResetOperationState()
        {
            refToObject.Clear();
            reusedObjectToRef?.Clear();
            reuseIndexInitialized = false;
        }

        /// <summary>Obtains the object reference for the supplied input.</summary>
        /// <param name="id">The id used by this operation.</param>
        /// <returns>The result of the operation.</returns>
        public object GetObjectReference(uint id)
        {
            if (refToObject.TryGetValue(id, out var value))
            {
                return value;
            }
            LuminPackExceptionHelper.ThrowMessage("Object is not found in this reference id:" + id);
            return null!;
        }

        /// <summary>Performs the add object reference step for the current operation.</summary>
        /// <param name="id">The id used by this operation.</param>
        /// <param name="value">The value consumed or produced by this operation.</param>
        public void AddObjectReference(uint id, object value)
        {
            if (!refToObject.TryAdd(id, value))
            {
                LuminPackExceptionHelper.ThrowMessage("Object is already added, id:" + id);
            }
            if (reuseIndexInitialized) reusedObjectToRef!.TryAdd(value, id);
        }

        /// <summary>Checks whether an existing instance already represents another record in this read scope.</summary>
        /// <param name="value">The candidate instance for a new reference identifier.</param>
        /// <returns>Whether the instance has already been registered by identity.</returns>
        public bool IsObjectReferenceRegistered(object value)
        {
            if (refToObject.Count == 0) return false;
            if (!reuseIndexInitialized)
            {
                reusedObjectToRef ??= new LuminCircleReferenceMap<object, uint>(ReferenceEqualityComparer.Instance);
                foreach (var entry in refToObject) reusedObjectToRef.TryAdd(entry.Value, entry.Key);
                reuseIndexInitialized = true;
            }
            return reusedObjectToRef!.TryGetValue(value, out _);
        }

        /// <summary>Releases the resources or pooled state owned by this instance.</summary>
        void IDisposable.Dispose()
        {

        }
    }


}
