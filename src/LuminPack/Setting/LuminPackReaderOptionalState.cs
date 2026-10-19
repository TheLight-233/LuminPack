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
