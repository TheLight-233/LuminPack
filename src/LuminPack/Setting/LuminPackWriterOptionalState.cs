using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using LuminPack.Utility;

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
        private LuminCircleReferenceMap<object, uint> ObjectToRef { get; set; }
        
        public LuminPackSerializerOption Option { get; private set; }
        
        public LuminPackWriterOptionalState(LuminPackSerializerOption? option = null)
        {
            ObjectToRef = new LuminCircleReferenceMap<object, uint>(ReferenceEqualityComparer.Instance);
            _nextId = 0;
            Option = option ?? LuminPackSerializerOption.Default;
            
        }

        private LuminPackWriterOptionalState(bool _)
        {
            ObjectToRef = new LuminCircleReferenceMap<object, uint>(ReferenceEqualityComparer.Instance);
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
            Option = LuminPackSerializerOption.Default;
            _nextId = 0;
        }
        
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
        
        void IDisposable.Dispose()
        {
            Option = null;
        }
    }
}