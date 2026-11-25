using Microsoft.CodeAnalysis;

internal static partial class SerializerIssueRegression
{
    /// <summary>Verifies the storage contracts of nullable annotations on closed generic members.</summary>
    /// <param name="references">Platform and runtime references.</param>
    private static void VerifyClosedGenericMemberAnnotations(MetadataReference[] references)
    {
        const string source = """
            #nullable enable
            using System;
            using LuminPack;
            using LuminPack.Attribute;

            [LuminPackable] public abstract partial class Root { public int Header; }
            [LuminPackable] public abstract partial class AnnotatedMiddle<T> : Root { public T? Item; }
            [LuminPackable] public sealed partial class AnnotatedLeaf : AnnotatedMiddle<int> { public int Tail; }
            [LuminPackable] public sealed partial class TextLeaf : AnnotatedMiddle<string> { public int Tail; }
            [LuminPackable] public abstract partial class IntMiddle : Root { public int Item; }
            [LuminPackable] public sealed partial class IntLeaf : IntMiddle { public int Tail; }

            [LuminPackable] public sealed class NullableValue<T> where T : struct { public T? Item; }
            [LuminPackable] public sealed class NullableInt { public int? Item; }

            public static class Entry
            {
                public static void Check()
                {
                    var value = new AnnotatedLeaf { Header = 11, Item = 12, Tail = 13 };
                    var plain = new IntLeaf { Header = 11, Item = 12, Tail = 13 };
                    byte[] bytes = LuminPackSerializer.Serialize(value);
                    if (!bytes.AsSpan().SequenceEqual(LuminPackSerializer.Serialize(plain)) ||
                        LuminPackSerializer.Deserialize<AnnotatedLeaf>(bytes) is not { Header: 11, Item: 12, Tail: 13 })
                        throw new Exception("The annotated generic member did not use the substituted integer contract.");

                    AnnotatedMiddle<int> union = value;
                    if (LuminPackSerializer.Deserialize<AnnotatedMiddle<int>>(LuminPackSerializer.Serialize(union)) is not AnnotatedLeaf { Item: 12 } ||
                        LuminPackSerializer.DeserializeJson<AnnotatedMiddle<int>>(LuminPackSerializer.SerializeJson(union)) is not AnnotatedLeaf { Item: 12 })
                        throw new Exception("The closed generic Union did not preserve the substituted member.");

                    var text = new TextLeaf { Header = 1, Tail = 2 };
                    var decodedText = LuminPackSerializer.DeserializeJson<TextLeaf>(LuminPackSerializer.SerializeJson(text));
                    if (decodedText is not { Header: 1, Item: null, Tail: 2 })
                        throw new Exception("The reference substitution lost its null value.");

                    byte[] nullable = LuminPackSerializer.Serialize(new NullableValue<int>());
                    if (!nullable.AsSpan().SequenceEqual(LuminPackSerializer.Serialize(new NullableInt())) ||
                        LuminPackSerializer.Deserialize<NullableValue<int>>(nullable) is not { Item: null })
                        throw new Exception("A struct-constrained parameter lost its nullable value framing.");

                }
            }
            """;
        Execute(references, source, "ClosedGenericMemberAnnotations");
        VerifyOpenGenericMemberDispatch(references);
    }

    /// <summary>Checks open members of a two-parameter payload through generated overloads without a static backend.</summary>
    /// <param name="references">Platform and runtime references.</param>
    private static void VerifyOpenGenericMemberDispatch(MetadataReference[] references)
    {
        const string source = """
            #nullable enable
            using System;
            using LuminPack;
            using LuminPack.Attribute;

            public class Outer<TValue> where TValue : unmanaged
            {
                [LuminPackable]
                public sealed class Inner<TText> where TText : class
                {
                    public TValue Value;
                    public TText? Text;
                }
            }

            public static class Entry
            {
                public static void Check()
                {
                    var value = new Outer<int>.Inner<string> { Value = 41, Text = "value" };
                    if (LuminPackSerializer.Deserialize<Outer<int>.Inner<string>>(LuminPackSerializer.Serialize(value)) is not { Value: 41, Text: "value" } ||
                        LuminPackSerializer.DeserializeJson<Outer<int>.Inner<string>>(LuminPackSerializer.SerializeJson(value)) is not { Value: 41, Text: "value" })
                        throw new Exception("The generic formatter did not dispatch each member through its own contract.");
                }
            }
            """;
        Execute(references, source, "OpenGenericMemberDispatch");
    }
}
