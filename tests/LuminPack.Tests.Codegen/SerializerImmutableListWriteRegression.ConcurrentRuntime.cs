using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using LuminPack.Code;

internal static partial class SerializerImmutableListWriteRegression
{
    /// <summary>Checks capture timing, frozen snapshots and wide values under concurrent mutation.</summary>
    private static void VerifyConcurrentRuntime()
    {
        VerifyConcurrentBags();
        foreach (int count in new[] { 0, 1, 2, 17, 257 })
        {
            var stack = new ConcurrentStack<int>(Enumerable.Range(0, count));
            var enumerator = LuminPackCollectionAccess.EnumerateConcurrentStack(stack);
            var expected = stack.ToArray();
            stack.Clear(); stack.Push(999);
            var actual = new List<int>();
            while (enumerator.MoveNext()) actual.Add(enumerator.Current);
            enumerator.Dispose();
            if (!actual.SequenceEqual(expected)) throw new Exception("Stack head capture changed");
            foreach (int action in new[] { 0, 1, 2, 3, 4 })
            {
                var reference = TraceConcurrent(count, action, false);
                var candidate = TraceConcurrent(count, action, true);
                if (!reference.SequenceEqual(candidate)) throw new Exception("Dictionary capture changed: " + count + "/" + action);
            }
        }
        CheckConcurrentValues<string, decimal>(new[] { "a", "b", "c" }, new[] { 1.25m, 2.75m, 3.5m });
        CheckConcurrentValues<Guid, string>(new[] { Guid.NewGuid(), Guid.NewGuid() }, new[] { "first", "second" });
        CheckConcurrentValues<int, ConcurrentWide>(new[] { 1, 2 }, new[] { new ConcurrentWide(1), new ConcurrentWide(2) });
        var map = new ConcurrentDictionary<int, ConcurrentWide>();
        map[0] = new ConcurrentWide(0);
        int stop = 0;
        var worker = new Thread(() => { long version = 1; while (Volatile.Read(ref stop) == 0) map[0] = new ConcurrentWide(version++); });
        worker.IsBackground = true;
        worker.Start();
        try
        {
            for (int i = 0; i < 4096; i++)
                foreach (var pair in LuminPackCollectionAccess.EnumerateConcurrentDictionary(map))
                    if (pair.Value.A != pair.Value.B || pair.Value.B != pair.Value.C) throw new Exception("Torn published value");
        }
        finally { Volatile.Write(ref stop, 1); if (!worker.Join(5000)) throw new Exception("Dictionary worker did not stop"); }
    }

    /// <summary>Checks foreign queues, ring wrap, reference contents and freeze cleanup under contention.</summary>
    private static void VerifyConcurrentBags()
    {
        var bag = new ConcurrentBag<int>();
        for (int round = 0; round < 12; round++)
        {
            var worker = new Thread(() => { for (int i = 0; i < 97; i++) bag.Add(round * 1000 + i); for (int i = 0; i < 61; i++) bag.TryTake(out _); });
            worker.Start(); worker.Join();
            var expected = bag.ToArray();
            var snapshot = LuminPackCollectionAccess.RentConcurrentBagSnapshot(bag, out int count, out bool pooled);
            try { if (!snapshot.Take(count).SequenceEqual(expected)) throw new Exception("Frozen bag snapshot order changed"); }
            finally { if (pooled) System.Buffers.ArrayPool<int>.Shared.Return(snapshot); }
        }
        var strings = new ConcurrentBag<string?>(new[] { "a", null, "c" });
        var text = LuminPackCollectionAccess.RentConcurrentBagSnapshot(strings, out int size, out bool rented);
        try { if (!text.Take(size).SequenceEqual(strings)) throw new Exception("Reference bag changed"); }
        finally { if (rented) System.Buffers.ArrayPool<string?>.Shared.Return(text, true); }
        var active = new ConcurrentBag<int>();
        int stop = 0, seed = 0;
        var workers = new Thread[3];
        for (int i = 0; i < workers.Length; i++)
        {
            workers[i] = new Thread(() => { while (Volatile.Read(ref stop) == 0) { active.Add(Interlocked.Increment(ref seed)); active.TryPeek(out _); active.TryTake(out _); } });
            workers[i].IsBackground = true;
            workers[i].Start();
        }
        try
        {
            for (int round = 0; round < 2048; round++)
            {
                var snapshot = LuminPackCollectionAccess.RentConcurrentBagSnapshot(active, out int count, out bool pooled);
                try { if (snapshot.Take(count).Any(value => value <= 0) || snapshot.Take(count).Distinct().Count() != count) throw new Exception("Concurrent bag snapshot tore"); }
                finally { if (pooled) System.Buffers.ArrayPool<int>.Shared.Return(snapshot); }
            }
        }
        finally { Volatile.Write(ref stop, 1); foreach (var worker in workers) if (!worker.Join(5000)) throw new Exception("Bag freeze retained a lock"); }
        using var output = new LuminPack.Utility.LuminBufferWriter(true);
        var writer = new LuminPack.Core.LuminPackWriter(output);
        var wide = new ConcurrentBag<ConcurrentWide>(new[] { new ConcurrentWide(1), new ConcurrentWide(2), new ConcurrentWide(3) });
        if (LuminPackCollectionAccess.TryWriteConcurrentBagRaw(ref writer, wide))
        {
            var bytes = writer.GetSpan().ToArray();
            var expected = wide.ToArray();
            if (bytes.Length != 4 + expected.Length * 24 || BitConverter.ToInt32(bytes, 0) != expected.Length) throw new Exception("Raw bag frame changed");
            for (int i = 0; i < expected.Length; i++) for (int offset = 0; offset < 24; offset += 8)
                if (BitConverter.ToInt64(bytes, 4 + i * 24 + offset) != expected[i].A) throw new Exception("Raw bag value changed");
        }
    }

    /// <summary>Records runtime-specific dictionary visibility around the first yielded element.</summary>
    /// <param name="count">The initial key count.</param>
    /// <param name="mutation">The mutation scenario.</param>
    /// <param name="candidate">Whether to use the validated traversal.</param>
    /// <returns>The complete sequence observed by the selected enumerator.</returns>
    private static List<KeyValuePair<int, long>> TraceConcurrent(int count, int mutation, bool candidate)
    {
        var map = new ConcurrentDictionary<int, long>(1, 3, new ConcurrentCollisionComparer());
        for (int i = 0; i < count; i++) map[i] = i;
        var values = new List<KeyValuePair<int, long>>();
        var fast = candidate ? LuminPackCollectionAccess.EnumerateConcurrentDictionary(map) : default;
        using var slow = candidate ? null : map.GetEnumerator();
        if (mutation == 4) { map.Clear(); for (int i = 400; i < 527; i++) map[i] = i; }
        while (candidate ? fast.MoveNext() : slow!.MoveNext())
        {
            var pair = candidate ? fast.Current : slow!.Current;
            values.Add(pair);
            if (values.Count != 1) continue;
            if (mutation == 0) map.TryRemove(pair.Key - 1, out _);
            if (mutation == 1) { map.Clear(); map[999] = 999; }
            if (mutation == 2) for (int i = 300; i < 600; i++) map[i] = i;
            if (mutation == 3) for (int i = 0; i < count; i++) map[i] = i + 991;
        }
        fast.Dispose();
        return values;
    }

    /// <summary>Checks closed reference and wide value field offsets.</summary>
    /// <typeparam name="K">The key type.</typeparam>
    /// <typeparam name="V">The value type.</typeparam>
    /// <param name="keys">The source keys.</param>
    /// <param name="values">The source values.</param>
    private static void CheckConcurrentValues<K, V>(K[] keys, V[] values) where K : notnull
    {
        var map = new ConcurrentDictionary<K, V>();
        for (int i = 0; i < keys.Length; i++) map[keys[i]] = values[i];
        var actual = new List<KeyValuePair<K, V>>();
        foreach (var pair in LuminPackCollectionAccess.EnumerateConcurrentDictionary(map)) actual.Add(pair);
        if (!actual.SequenceEqual(map)) throw new Exception("Closed dictionary storage changed");
        var stack = new ConcurrentStack<V>(values);
        var items = new List<V>();
        foreach (var item in LuminPackCollectionAccess.EnumerateConcurrentStack(stack)) items.Add(item);
        if (!items.SequenceEqual(stack)) throw new Exception("Closed stack storage changed");
    }

    /// <summary>Forces dictionary mutation tests through one collision chain.</summary>
    private sealed class ConcurrentCollisionComparer : IEqualityComparer<int>
    {
        /// <summary>Compares probe keys.</summary>
        /// <param name="x">The first key.</param>
        /// <param name="y">The second key.</param>
        /// <returns>Whether the keys are equal.</returns>
        public bool Equals(int x, int y) => x == y;
        /// <summary>Places every probe key in one chain.</summary>
        /// <param name="value">The key.</param>
        /// <returns>The fixed collision hash.</returns>
        public int GetHashCode(int value) => 0;
    }

    /// <summary>Detects torn publication and exercises 32-bit aggregate copies.</summary>
    private struct ConcurrentWide
    {
        public long A, B, C;
        /// <summary>Copies one version into all independently observed words.</summary>
        /// <param name="value">The published version.</param>
        public ConcurrentWide(long value) { A = B = C = value; }
    }
}
