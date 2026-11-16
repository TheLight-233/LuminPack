using System.Collections.Concurrent;
using System.ComponentModel;

namespace LuminPack.Code;

public static partial class LuminPackCollectionAccess
{
    /// <summary>Empties a reused concurrent queue for an ordered refill while keeping its current tail segment.</summary>
    /// <typeparam name="T">The queue element type.</typeparam>
    /// <param name="queue">The existing queue.</param>
    /// <remarks><c>Clear</c> freezes the tail and publishes a new 32-slot segment, so refilling a large queue allocates the
    /// whole growth chain again. Dequeuing every item leaves the last segment open for enqueues, and a refill that fits
    /// its capacity allocates no segment. Each removal is an ordinary framework dequeue, so concurrent operations keep
    /// their framework guarantees.</remarks>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void DrainConcurrentQueue<T>(ConcurrentQueue<T> queue)
    {
        while (queue.TryDequeue(out _))
        {
        }
    }
}
