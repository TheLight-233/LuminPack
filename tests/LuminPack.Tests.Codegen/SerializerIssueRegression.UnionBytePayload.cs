using Microsoft.CodeAnalysis;

internal static partial class SerializerIssueRegression
{
    /// <summary>Checks that checked Union reads consume lone byte fields as payload data and keep tracked identities.</summary>
    /// <param name="references">Platform and runtime references.</param>
    internal static void RunUnionBytePayload(MetadataReference[] references)
    {
        const string source = """
            using System;
            using System.Collections.Generic;
            using LuminPack;
            using LuminPack.Attribute;

            [LuminPackable(GeneratorType.CircleReference)]
            public sealed partial class Card
            {
                [LuminPackOrder(0)] public int Id;
            }

            [LuminPackable] public abstract partial class Action { public short Turn; public byte Actor; }
            [LuminPackable] public sealed partial class Play : Action { public Card Card; public byte Slot; }

            [LuminPackable] public abstract partial class Message { }
            [LuminPackable] public sealed partial class TextMessage : Message { public byte Code; public string Text; }

            [LuminPackable]
            public sealed partial class Replay
            {
                public List<Card> Cards;
                public List<Action> Actions;
            }

            public static class Entry
            {
                public static void Check()
                {
                    var card = new Card { Id = 42 };
                    var replay = new Replay
                    {
                        Cards = new() { card },
                        Actions = new() { new Play { Turn = 8, Actor = 2, Card = card, Slot = 1 } }
                    };
                    var bytes = LuminPackSerializer.Serialize(replay);
                    Replay copy = null;
                    LuminPackSerializer.Deserialize(bytes, ref copy);
                    CheckReplay(copy);
                    var existing = copy.Actions[0];
                    LuminPackSerializer.Deserialize(bytes, ref copy);
                    CheckReplay(copy);
                    if (!ReferenceEquals(existing, copy.Actions[0])) throw new Exception("Union case was replaced on reuse.");

                    Action root = new Play { Card = card, Slot = byte.MaxValue };
                    var rootBytes = LuminPackSerializer.Serialize(root);
                    root = null;
                    LuminPackSerializer.Deserialize(rootBytes, ref root);
                    if (root is not Play { Slot: byte.MaxValue, Card.Id: 42 }) throw new Exception("Union root lost its byte or card.");
                    try
                    {
                        LuminPackSerializer.Deserialize(rootBytes.AsSpan(0, rootBytes.Length - 1), ref root);
                        throw new Exception("Union root accepted a truncated byte.");
                    }
                    catch (Exception error) when (error.Message.StartsWith("Span out of range", StringComparison.Ordinal)) { }

                    Message message = new TextMessage { Code = 3, Text = "payload" };
                    var messageBytes = LuminPackSerializer.Serialize(message);
                    message = null;
                    LuminPackSerializer.Deserialize(messageBytes, ref message);
                    if (message is not TextMessage { Code: 3, Text: "payload" }) throw new Exception("Stream Union lost its byte or text.");
                }

                private static void CheckReplay(Replay replay)
                {
                    if (replay.Actions[0] is not Play { Turn: 8, Actor: 2, Slot: 1, Card.Id: 42 } play ||
                        !ReferenceEquals(replay.Cards[0], play.Card)) throw new Exception("Union collection lost its byte or card identity.");
                }
            }
            """;
        Execute(references, source, "UnionBytePayload");
        Console.WriteLine("Passed checked Union root and list byte payloads with reference identity and reuse.");
    }
}
