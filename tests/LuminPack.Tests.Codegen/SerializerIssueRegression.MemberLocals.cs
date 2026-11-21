using Microsoft.CodeAnalysis;

internal static partial class SerializerIssueRegression
{
    /// <summary>Checks that case-distinct fields have independent constructor and read locals.</summary>
    /// <param name="references">The compiler references used by the consumer.</param>
    internal static void RunMemberLocals(MetadataReference[] references)
    {
        const string source = """
            using System;
            using LuminPack;
            using LuminPack.Attribute;

            [LuminPackable]
            public sealed class Counts
            {
                public int Count;
                public int count;
                public int count2;
                public int Identifier;
                [LuminPackConstructor]
                public Counts(int identifier) => Identifier = identifier;
            }

            [LuminPackable]
            public struct Labels
            {
                public string Label;
                public string label;
                public int Identifier;
                [LuminPackConstructor]
                public Labels(int identifier) => Identifier = identifier;
            }

            public static class Entry
            {
                public static void Check()
                {
                    var counts = new Counts(5) { Count = 11, count = 22, count2 = 33 };
                    var binary = LuminPackSerializer.Deserialize<Counts>(LuminPackSerializer.Serialize(counts));
                    var json = LuminPackSerializer.DeserializeJson<Counts>(LuminPackSerializer.SerializeJson(counts));
                    if (binary is not { Count: 11, count: 22, count2: 33, Identifier: 5 } ||
                        json is not { Count: 11, count: 22, count2: 33, Identifier: 5 })
                        throw new InvalidOperationException("Case-distinct record fields did not round trip.");

                    var labels = new Labels(9) { Label = "upper", label = "lower" };
                    var binaryLabels = LuminPackSerializer.Deserialize<Labels>(LuminPackSerializer.Serialize(labels));
                    var jsonLabels = LuminPackSerializer.DeserializeJson<Labels>(LuminPackSerializer.SerializeJson(labels));
                    if (binaryLabels is not { Label: "upper", label: "lower", Identifier: 9 } ||
                        jsonLabels is not { Label: "upper", label: "lower", Identifier: 9 })
                        throw new InvalidOperationException("Case-distinct struct fields did not round trip.");
                }
            }
            """;
        Execute(references, source, "CaseDistinctFields");
        Console.WriteLine("Passed binary and JSON round trips for case-distinct record and struct fields.");
    }
}
