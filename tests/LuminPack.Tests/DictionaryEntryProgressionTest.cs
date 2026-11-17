using LuminPack;
using LuminPack.Attribute;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Preserves dense dictionary entry roots across caller code and collection replacement.</summary>
public class DictionaryEntryProgressionTest
{
    /// <summary>Compacting collection retains the active entry array, and resizing during a callback still fails.</summary>
    [Fact]
    public void DenseEntriesSurviveCollectionAndRejectCallbackMutation()
    {
        var source = Enumerable.Range(0, 17).ToDictionary(index => index,
            index => new ProgressionValue { Value = index * 7 });
        int calls = 0;
        try
        {
            ProgressionValue.BeforeWrite = () =>
            {
                if (++calls == 8) GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
            };
            byte[] payload = LuminPackSerializer.Serialize(source);
            Assert.Equal(source.Count, calls);
            var restored = global::LuminPackUnitTest.ConcreteReads.Read<Dictionary<int, ProgressionValue>>(payload, static (global::System.ReadOnlySpan<byte> concreteReadInput, ref Dictionary<int, ProgressionValue> concreteReadTarget) => LuminPackSerializer.Deserialize(concreteReadInput, ref concreteReadTarget));
            Assert.Equal(source.Keys, restored.Keys);
            foreach (var item in source) Assert.Equal(item.Value.Value, restored[item.Key].Value);

            calls = 0;
            ProgressionValue.BeforeWrite = () =>
            {
                if (++calls != 8) return;
                source.Add(17, new ProgressionValue { Value = 119 });
                GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
            };
            Assert.Throws<InvalidOperationException>(() => LuminPackSerializer.Serialize(source));
            ProgressionValue.BeforeWrite = null;
            restored = global::LuminPackUnitTest.ConcreteReads.Read<Dictionary<int, ProgressionValue>>(LuminPackSerializer.Serialize(source), static (global::System.ReadOnlySpan<byte> concreteReadInput, ref Dictionary<int, ProgressionValue> concreteReadTarget) => LuminPackSerializer.Deserialize(concreteReadInput, ref concreteReadTarget));
            Assert.Equal(source.Count, restored.Count);
        }
        finally { ProgressionValue.BeforeWrite = null; }
    }

    /// <summary>Each checked traversal rejects an insertion made by its last element formatter.</summary>
    [Fact]
    public void CollectionWritersCheckTheFinalCallback()
    {
        var item = new ProgressionValue();
        var queue = new Queue<ProgressionValue>();
        var heap = new PriorityQueue<ProgressionValue, int>();
        var set = new HashSet<ProgressionValue>();
        queue.Enqueue(item);
        heap.Enqueue(item, 7);
        set.Add(item);
        try
        {
            Check(() => queue.Enqueue(item), () => LuminPackSerializer.Serialize(queue));
            Check(() => heap.Enqueue(item, 4), () => LuminPackSerializer.Serialize(heap));
            Check(() => set.Add(new ProgressionValue()), () => LuminPackSerializer.Serialize(set));
        }
        finally { ProgressionValue.BeforeWrite = null; }

        static void Check(Action mutate, Action write)
        {
            ProgressionValue.BeforeWrite = () =>
            {
                ProgressionValue.BeforeWrite = null;
                mutate();
            };
            Assert.Throws<InvalidOperationException>(write);
        }
    }

    /// <summary>A removal allowed by the set enumerator must also shorten the emitted header.</summary>
    [Fact]
    public void SetRemovalDuringWriteUsesTheEnumeratedCount()
    {
        var first = new ProgressionValue { Value = 3 };
        var second = new ProgressionValue { Value = 7 };
        var set = new HashSet<ProgressionValue> { first, second };
        byte[] expected = LuminPackSerializer.Serialize(new HashSet<ProgressionValue> { first });
        try
        {
            ProgressionValue.BeforeWrite = () => set.Remove(second);
            Assert.Equal(expected, LuminPackSerializer.Serialize(set));
        }
        finally { ProgressionValue.BeforeWrite = null; }
    }

    /// <summary>Runs caller code while the containing dictionary's entry storage is live.</summary>
    [LuminPackable]
    public sealed partial class ProgressionValue
    {
        /// <summary>Stores the serialized scalar.</summary>
        public int Value;

        /// <summary>Gets or sets the isolated test callback.</summary>
        public static Action? BeforeWrite { get; set; }

        /// <summary>Invokes caller code at the established pre-serialization boundary.</summary>
        [LuminPackOnSerializing]
        public void OnSerializing() => BeforeWrite?.Invoke();
    }
}
