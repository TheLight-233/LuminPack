using LuminPack;
using LuminPack.Attribute;

namespace LuminPackPortableFixtures;

/// <summary>Exchanges closed primitive roots and a positional record with inherited list storage.</summary>
internal static class PrimitiveRootFixtures
{
    /// <summary>Writes managed Binary inputs or validates the portable Binary outputs.</summary>
    /// <param name="directory">The exchange directory.</param>
    /// <param name="write">Whether to produce managed inputs.</param>
    internal static void Exchange(string directory, bool write)
    {
        Directory.CreateDirectory(directory);
        Check("scalar", LuminPackSerializer.Serialize<int>(int.MinValue));
        Check("float", LuminPackSerializer.Serialize<float>(1.5f));
        Check("bools", LuminPackSerializer.Serialize<bool[]>([true, false, true]));
        Check("nested", LuminPackSerializer.Serialize<Dictionary<int, List<float>>>(new() { [-1] = [1.25f] }));
        Check("empty", LuminPackSerializer.Serialize<int?[]>([]));
        Check("null", LuminPackSerializer.Serialize<int?[]>(null!));
        Check("array", LuminPackSerializer.Serialize<int?[]>([1, null, -7]));
        Check("list", LuminPackSerializer.Serialize<List<float?>>([1.5f, null, -2.25f]));
        Check("dict", LuminPackSerializer.Serialize<Dictionary<int, int?>>(new() { [3] = null }));
        var derived = new PrimitiveList { 11, 22 };
        derived.Label = 42;
        Check("derived", LuminPackSerializer.Serialize(derived));
        if (write)
        {
            foreach (var (name, json) in new[]
            {
                ("scalar", "-2147483648"), ("float", "1.5"), ("bools", "[true,false,true]"),
                ("nested", "[[-1,[1.25]]]"), ("empty", "[]"), ("null", "null"),
                ("array", "[1,null,-7]"), ("list", "[1.5,null,-2.25]"), ("dict", "[[3,null]]"),
                ("derived", """{"Label":42,"$items":[11,22]}""")
            })
            {
                File.WriteAllBytes(Path.Combine(directory, name + ".utf8.json"), System.Text.Encoding.UTF8.GetBytes(json));
                File.WriteAllBytes(Path.Combine(directory, name + ".utf16.json"), System.Text.Encoding.Unicode.GetBytes(json));
            }
            File.WriteAllBytes(Path.Combine(directory, "derived-null.bin"), [1, 42, 0, 0, 0, 255, 255, 255, 255]);
            File.WriteAllText(Path.Combine(directory, "derived-null.json"), """{"Label":42,"$items":null}""");
        }

        void Check(string name, byte[] expected)
        {
            string path = Path.Combine(directory, name + ".bin");
            if (write)
                File.WriteAllBytes(path, expected);
            else if (!File.ReadAllBytes(path).AsSpan().SequenceEqual(expected))
                throw new InvalidDataException("Unexpected primitive root bytes for " + name);
        }
    }
}

/// <summary>Verifies the ordinary member count independently of the inherited collection body.</summary>
[LuminPackable, LuminPackOption(IncludeCollectionBase = true)]
public sealed partial class PrimitiveList : List<int>
{
    public int Label;
}
