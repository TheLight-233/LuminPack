using System.Reflection;

/// <summary>Executes original fixture operations once to produce JIT diagnostics without throughput measurement.</summary>
internal static class SerializerCodegenProbe
{
    /// <summary>Compiles equivalent checked and guaranteed entries once for native-code inspection.</summary>
    /// <param name="directory">The complete benchmark image with the capacity-tier fixture.</param>
    internal static void RunCapacityTiers(string directory)
    {
        string file = File.Exists(Path.Combine(directory, "LuminPack.Benchmark.Capacity.dll")) ? "LuminPack.Benchmark.Capacity.dll" : "LuminPack.Benchmark.dll";
        var type = BenchmarkImage.Load(directory, file).GetType("LuminPackBenchmark.CapacityTierBenchmark", true)!;
        object fixture = Activator.CreateInstance(type)!;
        try { type.GetMethod("SetUp")!.Invoke(fixture, null); }
        finally { type.GetMethod("CleanUp")!.Invoke(fixture, null); }
        Console.WriteLine("Executed equivalent capacity-tier entries once and checked wire parity; no latency was measured.");
    }

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
        var optionsField = type.GetField("luminPackSerializerOption", BindingFlags.Instance | BindingFlags.NonPublic);
        if (character && utf8 && optionsField != null)
        {
            object options = optionsField.GetValue(fixture)!;
            var encoding = options.GetType().GetProperty("StringEncoding")!;
            encoding.SetValue(options, Enum.Parse(encoding.PropertyType, "UTF8"));
        }
        type.GetMethod("SetUp")!.Invoke(fixture, null);
        try
        {
            if (character && utf8 && optionsField is null) RunCompiledUtf8(assembly, fixture);
            else
            {
                type.GetMethod("LuminPackSerialize")!.Invoke(fixture, null);
                GC.KeepAlive(type.GetMethod("LuminPackDeserialize")!.Invoke(fixture, null));
            }
            Console.WriteLine($"Executed the {fixtureName} read/write entries once{(utf8 ? " with supplemental UTF-8 options" : " with original options")} for code-generation inspection; no latency was measured.");
        }
        finally { type.GetMethod("CleanUp")!.Invoke(fixture, null); }
    }

    /// <summary>Closes the same generated character buffer operations with the supplemental UTF-8 profile.</summary>
    /// <param name="assembly">The retained benchmark assembly.</param>
    /// <param name="fixture">The already initialized original character fixture.</param>
    private static void RunCompiledUtf8(Assembly assembly, object fixture)
    {
        const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
        object value = fixture.GetType().GetField("_data", fields)!.GetValue(fixture)!;
        object writer = fixture.GetType().GetField("_luminWriter", fields)!.GetValue(fixture)!;
        Type profile = writer.GetType().Assembly.GetType("LuminPack.Option.LuminPackDefaultOptions", true)!;
        Type serializer = assembly.GetType("LuminPack.LuminPackSerializer", true)!;
        MethodInfo write = serializer.GetMethods().Single(method => method.Name == "Serialize" && method.IsGenericMethodDefinition &&
            method.GetGenericArguments().Length == 1 && method.GetParameters() is [var payload, var buffer] &&
            (payload.ParameterType.IsByRef ? payload.ParameterType.GetElementType() : payload.ParameterType) == value.GetType() && buffer.ParameterType == writer.GetType());
        write.MakeGenericMethod(profile).Invoke(null, [value, writer]);
        MethodInfo read = serializer.GetMethods().Single(method => method.Name == "Deserialize" && method.IsGenericMethodDefinition &&
            method.GetGenericArguments().Length == 2 && method.GetParameters() is [var buffer] && buffer.ParameterType == writer.GetType());
        GC.KeepAlive(read.MakeGenericMethod(value.GetType(), profile).Invoke(null, [writer]));
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
