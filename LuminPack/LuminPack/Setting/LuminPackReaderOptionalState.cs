using System.Collections.Concurrent;
using LuminPack.Code;

namespace LuminPack.Option
{
    public static class LuminPackReaderOptionalStatePool
    {
        private static readonly ConcurrentQueue<LuminPackReaderOptionalState> _queue = new ();

        public static LuminPackReaderOptionalState Rent(LuminPackSerializerOption? options)
        {
            if (!_queue.TryDequeue(out var state))
            {
                state = new LuminPackReaderOptionalState();
            }

            state.Init(options);
            return state;
        }

        internal static void Return(LuminPackReaderOptionalState state)
        {
            state.Reset();
            _queue.Enqueue(state);
        }
    }
    
    public sealed class LuminPackReaderOptionalState : IDisposable
    {
        internal static readonly LuminPackReaderOptionalState NullOption = new LuminPackReaderOptionalState(true);
        
        private readonly Dictionary<uint, object> refToObject;
        
        public LuminPackSerializerOption Option { get; private set; }
        
        public LuminPackReaderOptionalState(LuminPackSerializerOption? option = null)
        {
            refToObject = new Dictionary<uint, object>();
            Option = option ?? LuminPackSerializerOption.Default;
        }

        private LuminPackReaderOptionalState(bool _)
        {
            refToObject = new Dictionary<uint, object>();
            Option = LuminPackSerializerOption.Default;
        }
        
        public void Init(LuminPackSerializerOption? options)
        {
            Option = options ?? LuminPackSerializerOption.Default;
        }
        
        public void Reset()
        {
            refToObject.Clear();
            Option = null!;
        }
        
        public object GetObjectReference(uint id)
        {
            if (refToObject.TryGetValue(id, out var value))
            {
                return value;
            }
            LuminPackExceptionHelper.ThrowMessage("Object is not found in this reference id:" + id);
            return null!;
        }

        public void AddObjectReference(uint id, object value)
        {
            if (!refToObject.TryAdd(id, value))
            {
                LuminPackExceptionHelper.ThrowMessage("Object is already added, id:" + id);
            }
        }
        
        void IDisposable.Dispose()
        {
            Option = null;
        }
    }
    
    
}