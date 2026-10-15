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
        /// <param name="options">The configuration to apply to this operation.</param>
        /// <returns>The result of the operation.</returns>
        public static LuminPackWriterOptionalState Rent(LuminPackSerializerOption? options)
        {
            if (!_queue.TryDequeue(out var state))
            {
                state = new LuminPackWriterOptionalState();
            }

            state.Init(options);
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

        /// <summary>Gets or sets the option.</summary>
        public LuminPackSerializerOption Option { get; private set; }

        /// <summary>Initializes a new instance of <c>LuminPackWriterOptionalState</c>.</summary>
        /// <param name="option">The configuration to apply to this operation.</param>
        public LuminPackWriterOptionalState(LuminPackSerializerOption? option = null)
        {
            ObjectToRef = new LuminCircleReferenceMap<object, uint>(ReferenceEqualityComparer.Instance);
            _nextId = 0;
            Option = option ?? LuminPackSerializerOption.InternalDefault;

        }

        /// <summary>Initializes a new instance of <c>LuminPackWriterOptionalState</c>.</summary>
        /// <param name="_">The  used by this operation.</param>
        private LuminPackWriterOptionalState(bool _)
        {
            ObjectToRef = new LuminCircleReferenceMap<object, uint>(ReferenceEqualityComparer.Instance);
            _nextId = 0;
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

        /// <summary>Clears transient writer operation state while preserving the configured option.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ResetOperationState()
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
            Option = null;
        }
    }
}
