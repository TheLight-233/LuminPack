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
            var restored = LuminPackSerializer.Deserialize<Dictionary<int, ProgressionValue>>(payload);
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
            restored = LuminPackSerializer.Deserialize<Dictionary<int, ProgressionValue>>(LuminPackSerializer.Serialize(source));
            Assert.Equal(source.Count, restored.Count);
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
