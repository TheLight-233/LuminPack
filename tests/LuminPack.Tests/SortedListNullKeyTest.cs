using LuminPack;
using LuminPack.Attribute;
using LuminPack.Option;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Rejects null sorted-list keys before publishing invalid bulk storage.</summary>
[Collection(nameof(RuntimeStateCollection))]
public sealed class SortedListNullKeyTest
{
    /// <summary>Rejects a null key in a fresh multi-pair record after reading its value and before the next pair.</summary>
    [Fact]
    public void FreshBulkNullKeyPreservesValueCallbackTiming()
    {
        Check<LuminPackUtf8LengthOptions>();
        Check<LuminPackUtf16LengthOptions>();
    }

    /// <summary>Nullable value keys reject an absent value just as the framework Add operation does.</summary>
    [Fact]
    public void NullableValueKeysRejectAbsentValues()
    {
        byte[] absent = LuminPackSerializer.Serialize<LuminPackUtf16LengthOptions>((int?)null);
        byte[] present = LuminPackSerializer.Serialize<LuminPackUtf16LengthOptions>((int?)137);
        byte[] value = LuminPackSerializer.Serialize<LuminPackUtf16LengthOptions>("value");
        byte[] bytes = BitConverter.GetBytes(2).Concat(absent).Concat(value).Concat(present).Concat(value).ToArray();
        SortedList<int?, string>? target = null;
        var failure = Assert.Throws<ArgumentNullException>(() => LuminPackSerializer.Deserialize<LuminPackUtf16LengthOptions>(bytes, ref target));
        Assert.Equal("key", failure.ParamName);
        Assert.Null(target);
    }

    /// <summary>Compares fresh rejection with the established Add key boundary.</summary>
    /// <typeparam name="TOptions">A length profile that distinguishes null from empty strings.</typeparam>
    private static void Check<TOptions>() where TOptions : struct, ILuminPackOptions
    {
        byte[] key = LuminPackSerializer.Serialize<TOptions>((string?)null);
        byte[] first = LuminPackSerializer.Serialize<TOptions>(new NullKeyObservedValue { Number = 137 });
        byte[] nextKey = LuminPackSerializer.Serialize<TOptions>("second");
        byte[] second = LuminPackSerializer.Serialize<TOptions>(new NullKeyObservedValue { Number = 271 });
        byte[] bytes = BitConverter.GetBytes(2).Concat(key).Concat(first).Concat(nextKey).Concat(second).ToArray();
        var callbacks = new List<int>();
        NullKeyObservedValue.OnRead = item => callbacks.Add(item.Number);
        try
        {
            SortedList<string, NullKeyObservedValue>? fresh = null;
            var failure = Assert.Throws<ArgumentNullException>(() => LuminPackSerializer.Deserialize<TOptions>(bytes, ref fresh));
            Assert.Equal("key", failure.ParamName);
            Assert.Null(fresh);
            Assert.Equal(new[] { 137 }, callbacks);
            callbacks.Clear();
            var reused = new SortedList<string, NullKeyObservedValue> { ["old"] = new() { Number = 719 } };
            failure = Assert.Throws<ArgumentNullException>(() => LuminPackSerializer.Deserialize<TOptions>(bytes, ref reused));
            Assert.Equal("key", failure.ParamName);
            Assert.Empty(reused);
            Assert.Equal(new[] { 137 }, callbacks);
        }
        finally { NullKeyObservedValue.OnRead = null; }
    }
}

/// <summary>Observes value-read completion before the sorted-list key validation boundary.</summary>
[LuminPackable]
public sealed class NullKeyObservedValue
{
    public int Number;
    public static Action<NullKeyObservedValue>? OnRead;

    /// <summary>Records the completed value before pair publication.</summary>
    [LuminPackOnDeserialized]
    public void CompleteRead() => OnRead?.Invoke(this);
}
