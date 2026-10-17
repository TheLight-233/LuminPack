using System.Reflection;

/// <summary>Executes original fixture operations once to produce JIT diagnostics without throughput measurement.</summary>
internal static class SerializerCodegenProbe
{
    /// <summary>Initializes the selected original fixture and invokes its LuminPack entries once.</summary>
    /// <param name="directory">A complete retained benchmark output directory.</param>
    /// <param name="character">Whether to inspect the character graph instead of the Simple fixture.</param>
    /// <param name="utf8">Whether the character fixture should use the supplemental UTF-8 encoding variant.</param>
    internal static void Run(string directory, bool character = false, bool utf8 = false)
    {
        var assembly = BenchmarkImage.Load(directory);
        string fixtureName = character ? "SerializeBenchmark" : "SimpleBenchmark";
        var type = assembly.GetType("LuminPackBenchmark." + fixtureName, throwOnError: true)!;
        object fixture = Activator.CreateInstance(type)!;
        if (character && utf8)
        {
            object options = type.GetField("luminPackSerializerOption", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(fixture)!;
            var encoding = options.GetType().GetProperty("StringEncoding")!;
            encoding.SetValue(options, Enum.Parse(encoding.PropertyType, "UTF8"));
        }
        type.GetMethod("SetUp")!.Invoke(fixture, null);
        try
        {
            type.GetMethod("LuminPackSerialize")!.Invoke(fixture, null);
            GC.KeepAlive(type.GetMethod("LuminPackDeserialize")!.Invoke(fixture, null));
            Console.WriteLine($"Executed the {fixtureName} read/write entries once{(utf8 ? " with supplemental UTF-8 options" : " with original options")} for code-generation inspection; no latency was measured.");
        }
        finally { type.GetMethod("CleanUp")!.Invoke(fixture, null); }
    }

    /// <summary>Executes a finite string boundary matrix through the retained fixture's original API methods.</summary>
    /// <param name="directory">A complete benchmark output directory.</param>
    internal static void RunStringCases(string directory)
    {
        var assembly = BenchmarkImage.Load(directory);
        var type = assembly.GetType("LuminPackBenchmark.BinaryStringRatioBenchmark", throwOnError: true)!;
        object fixture = Activator.CreateInstance(type)!;
        type.GetMethod("Setup")!.Invoke(fixture, null);
        object data = type.GetField("_data", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(fixture)!;
        string?[] expected = [null, "", "a", "bc", "def", new string('g', 4), new string('h', 8), new string('i', 16), new string('j', 32), new string('k', 33), "\u4e2d\u00e9\ud83c\udf1f"];
        var values = data.GetType().GetField("Values")!;
        values.SetValue(data, expected);
        byte[] bytes = (byte[])type.GetMethod("LuminPackWrite")!.Invoke(fixture, null)!;
        type.GetField("_lp", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(fixture, bytes);
        object result = type.GetMethod("LuminPackRead")!.Invoke(fixture, null)!;
        if (!((string?[])values.GetValue(result)!).SequenceEqual(expected)) throw new InvalidOperationException("String boundary code-generation fixture lost data.");
        Console.WriteLine("Executed string boundary cases once and verified values; no latency was measured.");
    }
}
