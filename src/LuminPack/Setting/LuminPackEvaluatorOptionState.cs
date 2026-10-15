using System.Collections.Concurrent;
using System.Runtime.InteropServices;

namespace LuminPack.Option;

/// <summary>Provides lumin pack evaluator optional state pool functionality.</summary>
public static class LuminPackEvaluatorOptionalStatePool
{
    private static readonly ConcurrentQueue<LuminPackEvaluatorOptionState> _queue = new();

    /// <summary>Obtains a reusable instance from the pool, creating one when needed.</summary>
    /// <param name="options">The configuration to apply to this operation.</param>
    /// <returns>The result of the operation.</returns>
    public static LuminPackEvaluatorOptionState Rent(LuminPackSerializerOption? options)
    {
        if (!_queue.TryDequeue(out var state))
        {
            state = new LuminPackEvaluatorOptionState();
        }

        state.Init(options);
        return state;
    }

    /// <summary>Returns an instance to the pool for reuse.</summary>
    /// <param name="state">The reusable state associated with the operation.</param>
    internal static void Return(LuminPackEvaluatorOptionState state)
    {
        state.Reset();
        _queue.Enqueue(state);
    }
}

/// <summary>Provides lumin pack evaluator option state functionality.</summary>
public sealed class LuminPackEvaluatorOptionState : IDisposable
{
    internal static readonly LuminPackEvaluatorOptionState NullOption = new LuminPackEvaluatorOptionState(true);

    private uint _nextId;
    /// <summary>Gets or sets the object to ref.</summary>
    private Dictionary<object, uint> ObjectToRef { get; set; }

    /// <summary>Gets or sets the option.</summary>
    public LuminPackSerializerOption Option { get; private set; }

    /// <summary>Initializes a new instance of <c>LuminPackEvaluatorOptionState</c>.</summary>
    /// <param name="option">The configuration to apply to this operation.</param>
    public LuminPackEvaluatorOptionState(LuminPackSerializerOption? option = null)
    {
        ObjectToRef = new Dictionary<object, uint>(ReferenceEqualityComparer.Instance);
        _nextId = 0;
        Option = option ?? LuminPackSerializerOption.InternalDefault;
    }

    /// <summary>Initializes a new instance of <c>LuminPackEvaluatorOptionState</c>.</summary>
    /// <param name="_">The  used by this operation.</param>
    private LuminPackEvaluatorOptionState(bool _)
    {
        ObjectToRef = new Dictionary<object, uint>(ReferenceEqualityComparer.Instance);
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
        ObjectToRef.Clear();
        Option = null!;
        _nextId = 0;
    }

    /// <summary>Obtains the or add reference for the supplied input.</summary>
    /// <param name="value">The value consumed or produced by this operation.</param>
    /// <returns>The result of the operation.</returns>
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

    /// <summary>Releases the resources or pooled state owned by this instance.</summary>
    void IDisposable.Dispose()
    {
        Option = null;
    }
}
