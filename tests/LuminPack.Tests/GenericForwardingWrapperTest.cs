#if LUMINPACK_NEXT_GENERATOR
using System.Collections.Generic;
using LuminPack;
using LuminPack.Attribute;
using LuminPack.Option;
using LuminPack.Core;
using Xunit;

namespace LuminPackUnitTest
{
    /// <summary>Verifies registered dispatch from source methods that forward an open payload to the serializer facade.</summary>
    public class GenericForwardingWrapperTest
    {
        /// <summary>Verifies a generic method pair uses the automatically registered closed payload.</summary>
        [Fact]
        public void ForwardedGenericPayloadRoundTrips()
        {
            var value = new ForwardedBox<double> { Value = 1.5, Count = 2 };
            byte[] bytes = ForwardingCodec.Write(value);
            Assert.Equal(LuminPackSerializer.Serialize(new ForwardedBox<double> { Value = 1.5, Count = 2 }), bytes);
            ForwardedBox<double> copy = ForwardingCodec.Read<ForwardedBox<double>>(bytes);
            Assert.Equal(1.5, copy.Value);
            Assert.Equal(2, copy.Count);
        }

        /// <summary>Verifies forwarding through a generic class into generic methods, and a ref read.</summary>
        [Fact]
        public void ForwardingThroughGenericTypesAndRefReads()
        {
            LuminPackSerializer.Register<ForwardedBox<string>>(
                static (ref LuminPackWriter writer, in ForwardedBox<string> item) => LuminPackSerializer.Serialize(ref writer, in item),
                static (ref LuminPackReader reader, ref ForwardedBox<string> item) => LuminPackSerializer.Deserialize(ref reader, ref item));
            LuminPackSerializer.Register<ForwardedBox<long>>(
                static (ref LuminPackWriter writer, in ForwardedBox<long> item) => LuminPackSerializer.Serialize(ref writer, in item),
                static (ref LuminPackReader reader, ref ForwardedBox<long> item) => LuminPackSerializer.Deserialize(ref reader, ref item));
            ForwardedBox<string> copy = ForwardingStore<string>.Load(ForwardingStore<string>.Save(new ForwardedBox<string> { Value = "s", Count = 3 }));
            Assert.Equal("s", copy.Value);
            Assert.Equal(3, copy.Count);

            ForwardedBox<long>? target = null;
            int consumed = ForwardingCodec.ReadInto(ForwardingCodec.Write(new ForwardedBox<long> { Value = 9 }), ref target);
            Assert.True(consumed > 0);
            Assert.Equal(9, target!.Value);
        }

        /// <summary>Verifies a declared profile and JSON forwarded through generic methods.</summary>
        [Fact]
        public void ForwardedProfileAndJsonRoundTrip()
        {
            LuminPackSerializer.Register<ForwardedBox<string>, LuminPackUtf8LengthOptions>(
                static (ref LuminPackWriter writer, in ForwardedBox<string> item) =>
                    LuminPackSerializer.Serialize<ForwardedBox<string>, LuminPackUtf8LengthOptions>(ref writer, in item),
                static (ref LuminPackReader reader, ref ForwardedBox<string> item) =>
                    LuminPackSerializer.Deserialize<ForwardedBox<string>, LuminPackUtf8LengthOptions>(ref reader, ref item));
            LuminPackSerializer.Register<ForwardedBox<int>>(
                static (ref LuminPackWriter writer, in ForwardedBox<int> item) => LuminPackSerializer.Serialize(ref writer, in item),
                static (ref LuminPackReader reader, ref ForwardedBox<int> item) => LuminPackSerializer.Deserialize(ref reader, ref item),
                static (ref LuminPackJsonWriter writer, in ForwardedBox<int> item) => LuminPackSerializer.SerializeJson(ref writer, in item),
                static (ref LuminPackJsonReader reader, ref ForwardedBox<int> item) => LuminPackSerializer.DeserializeJson(ref reader, ref item));
            var value = new ForwardedBox<string> { Value = "u", Count = 4 };
            Assert.Equal(LuminPackSerializer.Serialize<ForwardedBox<string>, LuminPackUtf8LengthOptions>(value), ForwardingCodec.WriteUtf8(value));

            ForwardedBox<int> json = ForwardingCodec.FromJson<ForwardedBox<int>>(ForwardingCodec.Json(new ForwardedBox<int> { Value = 5, Count = 6 }));
            Assert.Equal(5, json.Value);
            Assert.Equal(6, json.Count);
        }

        /// <summary>Verifies a generic method that builds a two-parameter payload with an object initializer before forwarding it.</summary>
        [Fact]
        public void ForwardedConstructionWithTwoTypeParameters()
        {
            LuminPackSerializer.Register<ForwardedPair<string, ForwardedBox<short>>>(
                static (ref LuminPackWriter writer, in ForwardedPair<string, ForwardedBox<short>> item) => LuminPackSerializer.Serialize(ref writer, in item),
                static (ref LuminPackReader reader, ref ForwardedPair<string, ForwardedBox<short>> item) => LuminPackSerializer.Deserialize(ref reader, ref item));
            byte[] bytes = ForwardingCodec.WritePair("k", new ForwardedBox<short> { Value = 7 });
            ForwardedPair<string, ForwardedBox<short>> pair = ForwardingCodec.Read<ForwardedPair<string, ForwardedBox<short>>>(bytes);
            Assert.Equal("k", pair.Key);
            Assert.Equal((short)7, pair.Value.Value);
        }

        /// <summary>Verifies a generic reference-tracked payload over a value type keeps its cycle through forwarding.</summary>
        [Fact]
        public void ForwardedReferenceTrackedPayloadKeepsIdentity()
        {
            LuminPackSerializer.Register<ForwardedNode<decimal>>(
                static (ref LuminPackWriter writer, in ForwardedNode<decimal> item) => LuminPackSerializer.Serialize(ref writer, in item),
                static (ref LuminPackReader reader, ref ForwardedNode<decimal> item) => LuminPackSerializer.Deserialize(ref reader, ref item));
            var node = new ForwardedNode<decimal> { Value = 7.5m };
            node.Next = node;
            node.Children = new List<ForwardedNode<decimal>> { node };
            ForwardedNode<decimal> copy = ForwardingCodec.Read<ForwardedNode<decimal>>(ForwardingCodec.Write(node));
            Assert.Equal(7.5m, copy.Value);
            Assert.Same(copy, copy.Next);
            Assert.Same(copy, copy.Children![0]);
        }
    }

    /// <summary>A generic payload reached through forwarding methods.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    [LuminPackable]
    public partial class ForwardedBox<T>
    {
        /// <summary>The value.</summary>
        public T Value = default!;
        /// <summary>The count.</summary>
        public int Count;
    }

    /// <summary>A two-parameter generic payload reached through forwarding methods.</summary>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <typeparam name="TValue">The value type.</typeparam>
    [LuminPackable]
    public partial class ForwardedPair<TKey, TValue>
    {
        /// <summary>The key.</summary>
        public TKey Key = default!;
        /// <summary>The value.</summary>
        public TValue Value = default!;
    }

    /// <summary>A generic reference-tracked payload reached through forwarding methods.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    [LuminPackable, LuminPackOption(GeneratorType = GeneratorType.Version, TrackReferences = true)]
    public partial class ForwardedNode<T>
    {
        /// <summary>The value.</summary>
        [LuminPackOrder(0)] public T Value = default!;
        /// <summary>The next node.</summary>
        [LuminPackOrder(1)] public ForwardedNode<T>? Next;
        /// <summary>The child nodes.</summary>
        [LuminPackOrder(2)] public List<ForwardedNode<T>>? Children;
    }

    /// <summary>Generic methods that forward their type parameters to the facade.</summary>
    public static class ForwardingCodec
    {
        /// <summary>Writes a value.</summary>
        public static byte[] Write<TValue>(TValue value) => LuminPackSerializer.Serialize(value);

        /// <summary>Reads a value.</summary>
        public static TValue Read<TValue>(byte[] bytes) => LuminPackSerializer.Deserialize<TValue>(bytes);

        /// <summary>Reads into an existing value.</summary>
        public static int ReadInto<TValue>(byte[] bytes, ref TValue value) => LuminPackSerializer.Deserialize(bytes, ref value);

        /// <summary>Writes a value with UTF-8 string lengths.</summary>
        public static byte[] WriteUtf8<TValue>(TValue value) => LuminPackSerializer.Serialize<TValue, LuminPackUtf8LengthOptions>(value);

        /// <summary>Writes JSON text.</summary>
        public static string Json<TValue>(TValue value) => LuminPackSerializer.SerializeJson(value);

        /// <summary>Reads JSON text.</summary>
        public static TValue FromJson<TValue>(string json) => LuminPackSerializer.DeserializeJson<TValue>(json);

        /// <summary>Builds and writes a pair.</summary>
        public static byte[] WritePair<TKey, TValue>(TKey key, TValue value) => Write(new ForwardedPair<TKey, TValue> { Key = key, Value = value });
    }

    /// <summary>A generic type whose static methods forward to the generic codec.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    public static class ForwardingStore<T>
    {
        /// <summary>Saves a box.</summary>
        public static byte[] Save(ForwardedBox<T> box) => ForwardingCodec.Write(box);

        /// <summary>Loads a box.</summary>
        public static ForwardedBox<T> Load(byte[] bytes) => ForwardingCodec.Read<ForwardedBox<T>>(bytes);
    }
}
#endif
