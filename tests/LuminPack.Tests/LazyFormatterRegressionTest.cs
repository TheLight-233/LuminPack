using LuminPack;
using LuminPack.Attribute;
using LuminPack.Code;
using LuminPack.Core;
using LuminPack.Option;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Checks concrete declaration profiles and explicit registration after missing generic lookups.</summary>
public class LazyFormatterRegressionTest
{
    /// <summary>Concrete entries preserve declaration profiles while generic payload APIs require registration.</summary>
    [Fact]
    public void GenericEntriesPreserveDeclaredProfiles()
    {
        var value = new LazyProfileNode { Text = "\u00E9\u4E2D\uD83D\uDE00", Next = new LazyProfileNode { Text = "tail" } };
        Assert.ThrowsAny<Exception>(() => LuminPackSerializer.Serialize<LazyProfileNode>(value));
        Assert.ThrowsAny<Exception>(() => LuminPackSerializer.SerializeJson<LazyProfileNode>(value));
        byte[] bytes = LuminPackSerializer.Serialize(value);
        var copy = global::LuminPackUnitTest.ConcreteReads.Read<LazyProfileNode>(bytes, static (global::System.ReadOnlySpan<byte> concreteReadInput, ref LazyProfileNode concreteReadTarget) => LuminPackSerializer.Deserialize(concreteReadInput, ref concreteReadTarget));
        Assert.Equal(value.Text, copy.Text);
        Assert.Equal(value.Next.Text, copy.Next!.Text);
        string json = LuminPackSerializer.SerializeJson(value);
        Assert.Equal(value.Text, global::LuminPackUnitTest.ConcreteReads.ReadJson<LazyProfileNode>(json, static (string concreteReadInput, ref LazyProfileNode concreteReadTarget) => LuminPackSerializer.DeserializeJson(concreteReadInput, ref concreteReadTarget)).Text);
        Assert.Equal(json, LuminPackSerializer.SerializeJson(value));
    }

    /// <summary>Strict validation also checks UTF-8 inside an ignored property's string value.</summary>
    [Fact]
    public void StrictUnknownStringsAreValidated()
    {
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes("{\"Unknown\":\"?\",\"Text\":\"valid\"}");
        Assert.Equal("valid", global::LuminPackUnitTest.ConcreteReads.ReadJson<LazyProfileNode>(bytes, static (ReadOnlySpan<byte> concreteReadInput, ref LazyProfileNode concreteReadTarget) => LuminPackSerializer.DeserializeJson(concreteReadInput, ref concreteReadTarget)).Text);
        bytes[Array.IndexOf(bytes, (byte)'?')] = 255;
        Assert.Throws<FormatException>(() => global::LuminPackUnitTest.ConcreteReads.ReadJson<LazyProfileNode>(bytes, static (ReadOnlySpan<byte> concreteReadInput, ref LazyProfileNode concreteReadTarget) => LuminPackSerializer.DeserializeJson(concreteReadInput, ref concreteReadTarget)));
    }

    /// <summary>Manual registration invalidates an earlier missing lookup and leaves concrete roots unchanged.</summary>
    [Fact]
    public unsafe void ManualRegistrationOverridesAWarmedGeneratedEntry()
    {
        var value = new LazyOverridePayload { Value = 17 };
        byte[] direct = LuminPackSerializer.Serialize(value);
        Assert.ThrowsAny<Exception>(() => LuminPackSerializer.Serialize<LazyOverridePayload>(value));
        nint key = LuminPackMarshal.GetMethodTable(typeof(LazyOverridePayload));
        Assert.False(LuminPackFormatterRegistry.TryGetValue(in key, out _));
        LuminPackFormatterRegistry.Register(typeof(LazyOverridePayload),
            (nint)(delegate*<ref LuminPackWriter, in LazyOverridePayload, void>)&WriteOverride,
            (nint)(delegate*<ref LuminPackReader, ref LazyOverridePayload, void>)&ReadOverride);
        byte[] bytes = LuminPackSerializer.Serialize<LazyOverridePayload>(value);
        Assert.Equal(BitConverter.GetBytes(99), bytes);
        Assert.Equal(99, LuminPackSerializer.Deserialize<LazyOverridePayload>(bytes).Value);
        Assert.Equal(direct, LuminPackSerializer.Serialize(value));
        Assert.ThrowsAny<Exception>(() => LuminPackSerializer.SerializeJson<LazyOverridePayload>(value));
    }

    /// <summary>Checks complete concurrent publication, revision invalidation and collectible payload lifetime.</summary>
    [Fact]
    public void ConcurrentRegistryRetainsCacheAndLifetimeSemantics() => FormatterRegistryCacheTest.Run();

    /// <summary>Writes the explicit override marker.</summary>
    /// <param name="writer">The current writer.</param>
    /// <param name="value">The ignored original payload.</param>
    private static void WriteOverride(ref LuminPackWriter writer, in LazyOverridePayload value) => writer.Write(99);

    /// <summary>Reads the explicit marker into a fresh payload.</summary>
    /// <param name="reader">The current reader.</param>
    /// <param name="value">The destination payload.</param>
    private static void ReadOverride(ref LuminPackReader reader, ref LazyOverridePayload value)
    {
        int marker = reader.Read<int>();
        value = new LazyOverridePayload { Value = marker };
    }

}

/// <summary>Uses a declaration-owned UTF-8 profile across recursive generic boundaries.</summary>
[LuminPackable, LuminPackOption(StringEncoding = LuminPackStringEncoding.UTF8, ValidateJsonSyntax = true)]
public sealed class LazyProfileNode
{
    public string Text = "";
    public LazyProfileNode? Next;
}

/// <summary>Receives a late explicit formatter after first generic use.</summary>
[LuminPackable]
public sealed class LazyOverridePayload
{
    public int Value;
}
