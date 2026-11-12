#if LUMINPACK_NEXT_GENERATOR
using System.Collections.Generic;
using LuminPack;
using LuminPack.Attribute;
using Xunit;

namespace LuminPackUnitTest
{
    /// <summary>Verifies payloads declared inside generic types and payloads whose simple names repeat across namespaces.</summary>
    /// <remarks>The legacy generator supports neither shape, so these tests run only with the Next generator.</remarks>
    public class GenericContainerPayloadTest
    {
        /// <summary>Verifies a class nested in a generic type writes the same record as an equivalent top-level class.</summary>
        [Fact]
        public void NestedClassWritesTheFlatRecord()
        {
            var value = new GenericContainer<int>.Inner { Value = 5, Count = 2, Label = "a" };
            byte[] bytes = LuminPackSerializer.Serialize(value);
            Assert.Equal(LuminPackSerializer.Serialize(new GenericContainerFlatInt { Value = 5, Count = 2, Label = "a" }), bytes);

            var copy = LuminPackSerializer.Deserialize<GenericContainer<int>.Inner>(bytes);
            Assert.Equal(5, copy.Value);
            Assert.Equal(2, copy.Count);
            Assert.Equal("a", copy.Label);
        }

        /// <summary>Verifies two closed constructions of one nested definition keep their own element types.</summary>
        [Fact]
        public void ClosedConstructionsKeepTheirTypeArguments()
        {
            var text = new GenericContainer<string>.Inner { Value = "x", Count = 3 };
            var copy = LuminPackSerializer.Deserialize<GenericContainer<string>.Inner>(LuminPackSerializer.Serialize(text));
            Assert.Equal("x", copy.Value);
            Assert.Equal(3, copy.Count);
            Assert.Null(copy.Label);

            var number = new GenericContainer<double>.Inner { Value = 1.5, Count = 4 };
            var numberCopy = LuminPackSerializer.Deserialize<GenericContainer<double>.Inner>(LuminPackSerializer.Serialize(number));
            Assert.Equal(1.5, numberCopy.Value);
            Assert.Equal(4, numberCopy.Count);
        }

        /// <summary>Verifies a payload nested two levels inside generic types, and a nested struct, as members of a record.</summary>
        [Fact]
        public void NestedPayloadsRoundTripAsMembers()
        {
            var holder = new GenericContainerHolder
            {
                Named = new GenericContainer<string>.Inner { Value = "n", Count = 1 },
                Leaf = new GenericContainer<long>.Middle<string>.Leaf { First = 9, Second = "s", Items = new List<string> { "p", "q" } },
                Pair = new GenericContainer<int>.Pair { Key = 4, Weight = 6 },
                Many = new List<GenericContainer<double>.Inner?> { new GenericContainer<double>.Inner { Value = 2.5, Count = 7 }, null },
            };
            byte[] bytes = LuminPackSerializer.Serialize(holder);
            var copy = LuminPackSerializer.Deserialize<GenericContainerHolder>(bytes);
            Assert.Equal("n", copy.Named!.Value);
            Assert.Equal(9, copy.Leaf!.First);
            Assert.Equal("s", copy.Leaf.Second);
            Assert.Equal(new[] { "p", "q" }, copy.Leaf.Items);
            Assert.Equal(4, copy.Pair.Key);
            Assert.Equal(6, copy.Pair.Weight);
            Assert.Equal(2.5, copy.Many![0]!.Value);
            Assert.Null(copy.Many[1]);
            Assert.Equal(bytes, LuminPackSerializer.Serialize(copy));

            string json = LuminPackSerializer.SerializeJson(holder);
            var jsonCopy = LuminPackSerializer.DeserializeJson<GenericContainerHolder>(json);
            Assert.Equal(json, LuminPackSerializer.SerializeJson(jsonCopy));
            Assert.Equal(7, jsonCopy.Many![0]!.Count);
        }

        /// <summary>Verifies payloads that share a simple name with a type of another namespace keep separate operations, including
        /// the collection owners whose element names coincide.</summary>
        [Fact]
        public void SameSimpleNamesInOtherNamespacesStayDistinct()
        {
            var value = new SharedNameRecord
            {
                Local = new SharedNamePoint { X = 1 },
                Remote = new SharedNames.Remote.SharedNamePoint { X = 2, Y = 3 },
                Locals = new List<SharedNamePoint> { new SharedNamePoint { X = 4 } },
                Remotes = new List<SharedNames.Remote.SharedNamePoint> { new SharedNames.Remote.SharedNamePoint { Y = 5 } },
            };
            byte[] bytes = LuminPackSerializer.Serialize(value);
            var copy = LuminPackSerializer.Deserialize<SharedNameRecord>(bytes);
            Assert.Equal(1, copy.Local.X);
            Assert.Equal(2, copy.Remote.X);
            Assert.Equal(3, copy.Remote.Y);
            Assert.Equal(4, copy.Locals![0].X);
            Assert.Equal(5, copy.Remotes![0].Y);

            var json = LuminPackSerializer.DeserializeJson<SharedNameRecord>(LuminPackSerializer.SerializeJson(value));
            Assert.Equal(5, json.Remotes![0].Y);
        }
    }

    /// <summary>A generic type that declares packable payloads.</summary>
    /// <typeparam name="T">The value type the nested payloads store.</typeparam>
    public partial class GenericContainer<T>
    {
        /// <summary>A class payload generic through its containing type.</summary>
        [LuminPackable]
        public partial class Inner
        {
            /// <summary>The stored value.</summary>
            public T Value = default!;
            /// <summary>The count.</summary>
            public int Count;
            /// <summary>The optional label.</summary>
            public string? Label { get; set; }
        }

        /// <summary>A second generic level.</summary>
        /// <typeparam name="U">The second value type.</typeparam>
        public partial class Middle<U>
        {
            /// <summary>A payload generic over both containing levels.</summary>
            [LuminPackable]
            public partial class Leaf
            {
                /// <summary>The outer value.</summary>
                public T First = default!;
                /// <summary>The inner value.</summary>
                public U Second = default!;
                /// <summary>The inner values.</summary>
                public List<U>? Items;
            }
        }

        /// <summary>A struct payload generic through its containing type.</summary>
        [LuminPackable]
        public partial struct Pair
        {
            /// <summary>The key.</summary>
            public T Key;
            /// <summary>The weight.</summary>
            public int Weight;
        }
    }

    /// <summary>The top-level equivalent of <see cref="GenericContainer{T}.Inner"/> closed over <see cref="int"/>.</summary>
    [LuminPackable]
    public partial class GenericContainerFlatInt
    {
        /// <summary>The stored value.</summary>
        public int Value;
        /// <summary>The count.</summary>
        public int Count;
        /// <summary>The optional label.</summary>
        public string? Label { get; set; }
    }

    /// <summary>Holds payloads nested in generic types as members.</summary>
    [LuminPackable]
    public partial class GenericContainerHolder
    {
        /// <summary>A nested class closed over string.</summary>
        public GenericContainer<string>.Inner? Named;
        /// <summary>A payload nested two generic levels deep.</summary>
        public GenericContainer<long>.Middle<string>.Leaf? Leaf;
        /// <summary>A nested struct.</summary>
        public GenericContainer<int>.Pair Pair;
        /// <summary>A list of nested classes with a null element.</summary>
        public List<GenericContainer<double>.Inner?>? Many;
    }

    /// <summary>A struct whose simple name is also declared in <c>SharedNames.Remote</c>.</summary>
    [LuminPackable]
    public partial struct SharedNamePoint
    {
        /// <summary>The coordinate.</summary>
        public long X;
    }

    /// <summary>A record that uses both same-named structs directly and as list elements.</summary>
    [LuminPackable]
    public partial class SharedNameRecord
    {
        /// <summary>The local struct.</summary>
        public SharedNamePoint Local;
        /// <summary>The struct of the other namespace.</summary>
        public SharedNames.Remote.SharedNamePoint Remote;
        /// <summary>Local structs.</summary>
        public List<SharedNamePoint>? Locals;
        /// <summary>Structs of the other namespace.</summary>
        public List<SharedNames.Remote.SharedNamePoint>? Remotes;
    }
}

namespace SharedNames.Remote
{
    /// <summary>A struct whose simple name is also declared in <c>LuminPackUnitTest</c>.</summary>
    [LuminPackable]
    public partial struct SharedNamePoint
    {
        /// <summary>The first coordinate.</summary>
        public int X;
        /// <summary>The second coordinate.</summary>
        public int Y;
    }
}
#endif
