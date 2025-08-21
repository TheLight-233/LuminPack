using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;

namespace LuminPack.Option
{
    public static class LuminPackWriterOptionalStatePool
    {
        private static readonly ConcurrentQueue<LuminPackWriterOptionalState> _queue = new ();

        public static LuminPackWriterOptionalState Rent(LuminPackSerializerOption? options)
        {
            if (!_queue.TryDequeue(out var state))
            {
                state = new LuminPackWriterOptionalState();
            }

            state.Init(options);
            return state;
        }

        internal static void Return(LuminPackWriterOptionalState state)
        {
            state.Reset();
            _queue.Enqueue(state);
        }
    }
    
    public sealed class LuminPackWriterOptionalState : IDisposable
    {
        internal static readonly LuminPackWriterOptionalState NullOption = new LuminPackWriterOptionalState(true);

        private uint _nextId;
        private Dictionary<object, uint> ObjectToRef { get; set; }
        
        public LuminPackSerializerOption Option { get; private set; }
        
        public LuminPackWriterOptionalState(LuminPackSerializerOption? option = null)
        {
            ObjectToRef = new Dictionary<object, uint>(ReferenceEqualityComparer.Instance);
            _nextId = 0;
            Option = option ?? LuminPackSerializerOption.Default;
            
        }

        private LuminPackWriterOptionalState(bool _)
        {
            ObjectToRef = new Dictionary<object, uint>(ReferenceEqualityComparer.Instance);
            _nextId = 0;
            Option = LuminPackSerializerOption.Default;
        }
        
        public void Init(LuminPackSerializerOption? options)
        {
            Option = options ?? LuminPackSerializerOption.Default;
        }
        
        public void Reset()
        {
            ObjectToRef.Clear();
            Option = null!;
            _nextId = 0;
        }
        
        public (bool existsReference, uint id) GetOrAddReference(object value)
        {
#if NET8_0_OR_GREATER
            ref var id = ref CollectionsMarshal.GetValueRefOrAddDefault(ObjectToRef, value, out var exists);
            if (exists)
            {
                return (true, id);
            }
            else
            {
                id = _nextId++;
                return (false, id);
            }
#else
            if (ObjectToRef.TryGetValue(value, out var id))
            {
                return (true, id);
            }
            else
            {
                id = _nextId++;
                ObjectToRef.Add(value, id);
                return (false, id);
            }
#endif
        }
        
        void IDisposable.Dispose()
        {
            Option = null;
        }
    }
}