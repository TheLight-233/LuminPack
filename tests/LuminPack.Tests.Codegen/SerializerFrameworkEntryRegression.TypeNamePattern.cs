using System.Reflection;
using System.Text.RegularExpressions;

internal static partial class SerializerFrameworkEntryRegression
{
    /// <summary>Finds one existing qualifier match in the actual loaded runtime.</summary>
    private delegate bool FindTypeNameRemoval(ReadOnlySpan<char> source, out int start, out int end);

    /// <summary>Compacts an actual runtime name from its first known qualifier match.</summary>
    private delegate int CompactTypeName(ReadOnlySpan<char> source, Span<char> destination, int start, int end);

    /// <summary>Compares the replacement-free matcher with the original regex over Unicode and ambiguous version runs.</summary>
    internal static void VerifyTypeNamePattern()
    {
        var type = typeof(LuminPack.Core.LuminPackWriter).Assembly.GetType("LuminPack.Code.LuminPackTypeNameText")!;
        var find = type.GetMethod("FindRemoval", BindingFlags.NonPublic | BindingFlags.Static)!.CreateDelegate<FindTypeNameRemoval>();
        var compact = type.GetMethod("Compact", BindingFlags.NonPublic | BindingFlags.Static)!.CreateDelegate<CompactTypeName>();
        var regex = new Regex(@", Version=\d+.\d+.\d+.\d+, Culture=[\w-]+, PublicKeyToken=(?:null|[a-f0-9]{16})");
        int count = 0;
        void Check(string source)
        {
            string expected = regex.Replace(source, "");
            string actual = source;
            if (find(source.AsSpan(), out int start, out int end))
            {
                var scratch = new char[source.Length];
                actual = new string(scratch, 0, compact(source.AsSpan(), scratch, start, end));
            }
            Require(actual == expected, "Qualifier matching diverged from the original regex.");
            count++;
        }
        string[] versions = ["1.2.3.4", "1234567", "123456", "1a2b3c4", "1\r2\t3\04", "1\n2.3.4", "\u0661.\u0662.\u0663.\u0664", "1.2.3.4.5", "1.2.3.", "", new('1', 128)];
        string[] cultures = ["neutral", "en-US", "a_b", "-", "\u0301", "\u200C", "\u0661", "a b", "", "\uD800"];
        string[] tokens = ["null", "nullx", "NULL", "0123456789abcdef", "0123456789abcdefg", "0123456789ABCDEF", "0123456789abcde", ""];
        foreach (string version in versions)
            foreach (string culture in cultures)
                foreach (string token in tokens)
                    Check("Foo, Version=" + version + ", Culture=" + culture + ", PublicKeyToken=" + token + "]");
        for (int value = 0; value <= char.MaxValue; value++)
        {
            Check("Foo, Version=1.2.3.4, Culture=" + (char)value + ", PublicKeyToken=null");
            Check("Foo, Version=" + (char)value + ".2.3.4, Culture=neutral, PublicKeyToken=null");
        }
        var random = new Random(4021);
        const string alphabet = "019ab.,\n\r\u0661\uD800";
        for (int i = 0; i < 100000; i++)
        {
            var characters = new char[random.Next(0, 26)];
            for (int j = 0; j < characters.Length; j++) characters[j] = alphabet[random.Next(alphabet.Length)];
            Check("A, Version=" + new string(characters) + ", Culture=neutral, PublicKeyToken=null[X, Version=1.2.3.4, Culture=en-US, PublicKeyToken=0123456789abcdef]");
        }
        foreach (var item in new[] { typeof(int), typeof(Dictionary<string, List<int[]>>), typeof(List<>), typeof(int).MakeByRefType() })
            Check(item.AssemblyQualifiedName!);
        Console.WriteLine("Matched " + count + " qualifier inputs against the original regex.");
    }
}
