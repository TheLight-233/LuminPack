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
        /// <param name="options">The configuration to apply to this operation.</param>
        /// <returns>The result of the operation.</returns>
        public static LuminPackReaderOptionalState Rent(LuminPackSerializerOption? options)
        {
            if (!_queue.TryDequeue(out var state))
            {
                state = new LuminPackReaderOptionalState();
            }

            state.Init(options);
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

        /// <summary>Gets or sets the option.</summary>
        public LuminPackSerializerOption Option { get; private set; }

        /// <summary>Initializes a new instance of <c>LuminPackReaderOptionalState</c>.</summary>
        /// <param name="option">The configuration to apply to this operation.</param>
        public LuminPackReaderOptionalState(LuminPackSerializerOption? option = null)
        {
            refToObject = new LuminCircleReferenceMap<uint, object>();
            Option = option ?? LuminPackSerializerOption.InternalDefault;
        }

        /// <summary>Initializes a new instance of <c>LuminPackReaderOptionalState</c>.</summary>
        /// <param name="_">The  used by this operation.</param>
        private LuminPackReaderOptionalState(bool _)
        {
            refToObject = new LuminCircleReferenceMap<uint, object>();
            Option = LuminPackSerializerOption.InternalDefault;
        }

        /// <summary>Initializes operation state with the supplied serialization options.</summary>
        /// <param name="options">The configuration to apply to this operation.</param>
        public void Init(LuminPackSerializerOption? options)
        {
            Option = options ?? LuminPackSerializerOption.InternalDefault;
        }

        /// <summary>Clears state retained by the previous operation so it can be reused.</summary>
        public void Reset()
        {
            ResetOperationState();
            Option = LuminPackSerializerOption.InternalDefault;
        }

        /// <summary>Clears transient reader operation state while preserving the configured option.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ResetOperationState()
        {
            refToObject.Clear();
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
        }

        /// <summary>Releases the resources or pooled state owned by this instance.</summary>
        void IDisposable.Dispose()
        {
            Option = null;
        }
    }


}
