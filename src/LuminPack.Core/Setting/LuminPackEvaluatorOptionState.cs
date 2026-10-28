using System.Runtime.CompilerServices;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;

namespace LuminPack.Option;

/// <summary>Provides lumin pack evaluator optional state pool functionality.</summary>
public static class LuminPackEvaluatorOptionalStatePool
{
    private static readonly ConcurrentQueue<LuminPackEvaluatorOptionState> _queue = new();

    /// <summary>Obtains a reusable instance from the pool, creating one when needed.</summary>
    /// <returns>The result of the operation.</returns>
    public static LuminPackEvaluatorOptionState Rent()
    {
        if (!_queue.TryDequeue(out var state))
        {
            state = new LuminPackEvaluatorOptionState();
        }

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

    /// <summary>Initializes a new instance of <c>LuminPackEvaluatorOptionState</c>.</summary>
    public LuminPackEvaluatorOptionState()
    {
        ObjectToRef = new Dictionary<object, uint>(ReferenceEqualityComparer.Instance);
        _nextId = 0;

    }

    /// <summary>Initializes a new instance of <c>LuminPackEvaluatorOptionState</c>.</summary>
    /// <param name="_">The  used by this operation.</param>
    private LuminPackEvaluatorOptionState(bool _)
    {
        ObjectToRef = new Dictionary<object, uint>(ReferenceEqualityComparer.Instance);
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
    public static LuminPackEvaluatorOptionState Lease(ref LuminPackEvaluatorOptionState? cache)
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
    private static LuminPackEvaluatorOptionState LeaseSlow(ref LuminPackEvaluatorOptionState? cache)
    {
        var state = new LuminPackEvaluatorOptionState();
        state._leased = true;
        cache = state;
        return state;
    }

    /// <summary>Clears the operation's references and ends the lease obtained from <see cref="Lease"/>.</summary>
    [global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Release()
    {
        Reset();
        Volatile.Write(ref _leased, false);
    }

    /// <summary>Clears state retained by the previous operation so it can be reused.</summary>
    public void Reset()
    {
        ObjectToRef.Clear();

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

    }
}
