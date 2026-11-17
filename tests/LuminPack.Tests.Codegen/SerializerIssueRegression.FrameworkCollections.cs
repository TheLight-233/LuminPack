using Microsoft.CodeAnalysis;

internal static partial class SerializerIssueRegression
{
    /// <summary>Checks framework scalar collection dependencies in both binary and JSON output.</summary>
    /// <param name="references">Platform and runtime references.</param>
    internal static void RunFrameworkCollections(MetadataReference[] references)
    {
        const string source = """
            using System;
            using System.Collections.Generic;
            using System.Numerics;
            using LuminPack;
            using LuminPack.Attribute;
            [assembly: LuminPackGeneratorOptions(LuminPackGenerationMode.MODE)]
            [LuminPackable] public partial class Holder { public Vector4[] Values; }
            public static class Entry
            {
                public static void Check()
                {
                    Vector4[] vectors = { new Vector4(1, 2, 3, 4), new Vector4(-5, 6, 7, 8) };
                    byte[] bytes = LuminPackSerializer.Serialize(vectors);
                    byte[] expected = { 2, 0, 0, 0, 0, 0, 128, 63, 0, 0, 0, 64, 0, 0, 64, 64, 0, 0, 128, 64,
                        0, 0, 160, 192, 0, 0, 192, 64, 0, 0, 224, 64, 0, 0, 0, 65 };
                    if (!bytes.AsSpan().SequenceEqual(expected)) throw new Exception("Vector4 binary wire");
                    Vector4[] copy = null;
                    LuminPackSerializer.Deserialize(bytes, ref copy);
                    if (!copy.AsSpan().SequenceEqual(vectors)) throw new Exception("Vector4 binary");
                    string json = LuminPackSerializer.SerializeJson(vectors);
                    if (json != "[[1,2,3,4],[-5,6,7,8]]") throw new Exception("Vector4 JSON wire: " + json);
                    copy = null;
                    LuminPackSerializer.DeserializeJson(json, ref copy);
                    if (!copy.AsSpan().SequenceEqual(vectors)) throw new Exception("Vector4 JSON");
                    Vector2[] pairs = { new Vector2(9, 10) };
                    string pairJson = LuminPackSerializer.SerializeJson(pairs);
                    Vector2[] pairCopy = null;
                    LuminPackSerializer.DeserializeJson(pairJson, ref pairCopy);
                    if (!pairCopy.AsSpan().SequenceEqual(pairs)) throw new Exception("Vector2 JSON");
                    var list = new List<Vector4>(vectors);
                    var listBytes = LuminPackSerializer.Serialize(list);
                    List<Vector4> listCopy = null;
                    LuminPackSerializer.Deserialize(listBytes, ref listCopy);
                    if (listCopy.Count != 2 || listCopy[1] != vectors[1]) throw new Exception("Vector list binary");
                    string listJson = LuminPackSerializer.SerializeJson(list);
                    listCopy = null;
                    LuminPackSerializer.DeserializeJson(listJson, ref listCopy);
                    if (listCopy.Count != 2 || listCopy[1] != vectors[1]) throw new Exception("Vector list JSON");
                    var holder = new Holder { Values = vectors };
                    string holderJson = LuminPackSerializer.SerializeJson(holder);
                    Holder holderCopy = null;
                    LuminPackSerializer.DeserializeJson(holderJson, ref holderCopy);
                    if (!holderCopy.Values.AsSpan().SequenceEqual(vectors)) throw new Exception("Nested vector JSON");
                }
            }
            """;
        foreach (string mode in new[] { "Full", "Medium", "Light", "Minimal" })
            Execute(references, source.Replace("MODE", mode, StringComparison.Ordinal), "Vectors" + mode);
        const string singleOperation = """
            using System;
            using System.Numerics;
            using LuminPack;
            using LuminPack.Attribute;
            [assembly: LuminPackGeneratorOptions(LuminPackGenerationMode.Minimal)]
            public static class Entry
            {
                public static void Check()
                {
                    Vector4[] value = { new Vector4(1, 2, 3, 4) };
                    OPERATION
                }
            }
            """;
        Execute(references, singleOperation.Replace("OPERATION", "if (LuminPackSerializer.Serialize(value).Length != 20) throw new Exception();", StringComparison.Ordinal), "VectorBinaryOnly");
        Execute(references, singleOperation.Replace("OPERATION", "if (LuminPackSerializer.SerializeJson(value) != \"[[1,2,3,4]]\") throw new Exception();", StringComparison.Ordinal), "VectorJsonOnly");
        Console.WriteLine("Passed framework scalar arrays, lists and nested collections across four generation modes.");
    }
}
