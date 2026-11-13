using LuminPack;
using LuminPack.Attribute;
using LuminPack.Code;
using LuminPack.Core;
using LuminPack.Option;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Checks concrete declaration profiles and explicit registration after missing generic lookups.</summary>
[Collection(nameof(RuntimeStateCollection))]
public class LazyFormatterRegressionTest
{
    private readonly OpaqueSerializer _fallback = new();

    /// <summary>Closed calls preserve declaration profiles and Next can round-trip through opaque generic calls.</summary>
    [Fact]
    public void GenericEntriesPreserveDeclaredProfiles()
    {
        var value = new LazyProfileNode { Text = "\u00E9\u4E2D\uD83D\uDE00", Next = new LazyProfileNode { Text = "tail" } };
#if LUMINPACK_NEXT_GENERATOR
        Assert.Equal(value.Text, _fallback.Deserialize<LazyProfileNode>(_fallback.Serialize(value)).Text);
        Assert.Equal(value.Text, _fallback.DeserializeJson<LazyProfileNode>(_fallback.SerializeJson(value)).Text);
#else
        Assert.ThrowsAny<Exception>(() => _fallback.Serialize(value));
        Assert.ThrowsAny<Exception>(() => _fallback.SerializeJson(value));
#endif
        byte[] bytes = LuminPackSerializer.Serialize(value);
        Assert.Equal(bytes, LuminPackSerializer.Serialize<LazyProfileNode>(value));
        var copy = global::LuminPackUnitTest.ConcreteReads.Read<LazyProfileNode>(bytes, static (global::System.ReadOnlySpan<byte> concreteReadInput, ref LazyProfileNode concreteReadTarget) => LuminPackSerializer.Deserialize(concreteReadInput, ref concreteReadTarget));
        Assert.Equal(value.Text, copy.Text);
        Assert.Equal(value.Next.Text, copy.Next!.Text);
        string json = LuminPackSerializer.SerializeJson(value);
        Assert.Equal(json, LuminPackSerializer.SerializeJson<LazyProfileNode>(value));
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

    /// <summary>Manual registration takes priority after the first generic lookup and leaves concrete roots unchanged.</summary>
    [Fact]
    public unsafe void ManualRegistrationOverridesFallbackAndLeavesStaticEntriesUnchanged()
    {
        var value = new LazyOverridePayload { Value = 17 };
        byte[] direct = LuminPackSerializer.Serialize(value);
#if LUMINPACK_NEXT_GENERATOR
        Assert.Equal(direct, _fallback.Serialize(value));
#else
        Assert.ThrowsAny<Exception>(() => _fallback.Serialize(value));
#endif
        nint key = LuminPackMarshal.GetMethodTable(typeof(LazyOverridePayload));
        Assert.False(LuminPackFormatterRegistry.TryGetValue(in key, out _));
        LuminPackFormatterRegistry.Register(typeof(LazyOverridePayload),
            (LuminPack.Code.LuminPackWriteDelegate<LazyOverridePayload>)WriteOverride,
            (LuminPack.Code.LuminPackReadDelegate<LazyOverridePayload>)ReadOverride);
        byte[] bytes = _fallback.Serialize(value);
        Assert.Equal(BitConverter.GetBytes(99), bytes);
        Assert.Equal(99, _fallback.Deserialize<LazyOverridePayload>(bytes).Value);
        Assert.Equal(direct, LuminPackSerializer.Serialize(value));
        Assert.Equal(direct, LuminPackSerializer.Serialize<LazyOverridePayload>(value));
#if LUMINPACK_NEXT_GENERATOR
        Assert.Equal(LuminPackSerializer.SerializeJson(value), _fallback.SerializeJson(value));
#else
        Assert.ThrowsAny<Exception>(() => _fallback.SerializeJson(value));
#endif
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

    /// <summary>Provides instance boundaries that deliberately retain registered generic dispatch.</summary>
    private sealed class OpaqueSerializer
    {
        /// <summary>Writes through the registered binary operation.</summary>
        /// <typeparam name="T">The runtime-selected payload type.</typeparam>
        /// <param name="value">The payload.</param>
        /// <returns>The registered representation.</returns>
        internal byte[] Serialize<T>(T value) => LuminPackSerializer.Serialize(value);

        /// <summary>Writes through the registered JSON operation.</summary>
        /// <typeparam name="T">The runtime-selected payload type.</typeparam>
        /// <param name="value">The payload.</param>
        /// <returns>The registered JSON representation.</returns>
        internal string SerializeJson<T>(T value) => LuminPackSerializer.SerializeJson(value);

        /// <summary>Reads through the registered binary operation.</summary>
        /// <typeparam name="T">The runtime-selected payload type.</typeparam>
        /// <param name="bytes">The registered representation.</param>
        /// <returns>The decoded payload.</returns>
        internal T Deserialize<T>(ReadOnlySpan<byte> bytes) => LuminPackSerializer.Deserialize<T>(bytes);

#if LUMINPACK_NEXT_GENERATOR
        /// <summary>Reads a JSON document through the registered operation.</summary>
        /// <typeparam name="T">The runtime-selected payload type.</typeparam>
        /// <param name="json">The registered representation.</param>
        /// <returns>The decoded payload.</returns>
        internal T DeserializeJson<T>(string json) => LuminPackSerializer.DeserializeJson<T>(json);
#endif
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
