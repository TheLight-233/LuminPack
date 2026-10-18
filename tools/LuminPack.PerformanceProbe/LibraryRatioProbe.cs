using System.Diagnostics;
using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;

/// <summary>Interleaves unchanged fixture operations with reference libraries to expose machine variability.</summary>
internal static class LibraryRatioProbe
{
    /// <summary>Measures equivalent checked and guaranteed records against the same reference serializer.</summary>
    /// <param name="directories">Complete images containing the same capacity-tier fixture.</param>
    internal static void RunCapacityTiers(params string[] directories)
    {
        Process.GetCurrentProcess().ProcessorAffinity = (nint)256;
        string[] names = ["LuminPackSerialize", "CheckedSerialize", "MemoryPackSerialize"];
        var fixtures = directories.Select(directory =>
        {
            string file = File.Exists(Path.Combine(directory, "LuminPackCapacityBenchmark.dll")) ? "LuminPackCapacityBenchmark.dll" : "LuminPackBenchmark.dll";
            var type = BenchmarkImage.Load(directory, file).GetType("LuminPackBenchmark.CapacityTierBenchmark", true)!;
            object fixture = Activator.CreateInstance(type)!;
            type.GetMethod("SetUp")!.Invoke(fixture, null);
            string prefix = Path.GetFileName(Path.TrimEndingDirectorySeparator(directory)) + ".";
            return (names.Select(name => prefix + name).ToArray(), names.Select(name => Bind(type.GetMethod(name)!, fixture)).ToArray(),
                type.GetMethod("CleanUp")!.CreateDelegate<Action>(fixture));
        }).ToArray();
        MeasureFixtures(fixtures, 128, 256, 256, names.Length, names.Length);
    }

    private static object? _sink;

    /// <summary>Measures unchanged collection fixtures against all original binary reference libraries.</summary>
    /// <param name="fixtureName">SimpleBenchmark, SerializeBenchmark, or PolymorphismBenchmark.</param>
    /// <param name="directories">Retained images; exclude unsafe polymorphism baselines.</param>
    internal static void RunCapacity(string fixtureName, params string[] directories) => RunCapacityCore(fixtureName, false, directories);

    /// <summary>Uses one reference-library fixture for all retained LuminPack images to avoid independent type-cache layouts.</summary>
    /// <param name="fixtureName">The unchanged benchmark fixture type name.</param>
    /// <param name="directories">Complete retained images built from the same fixture sources.</param>
    internal static void RunCapacityCommon(string fixtureName, params string[] directories) => RunCapacityCore(fixtureName, true, directories);

    /// <summary>Binds unchanged fixtures and selects independent or shared reference caches before measurement.</summary>
    /// <param name="fixtureName">The unchanged benchmark fixture type name.</param>
    /// <param name="commonReferences">Whether all LuminPack results share the same reference serializer instances.</param>
    /// <param name="directories">The complete retained benchmark images.</param>
    private static void RunCapacityCore(string fixtureName, bool commonReferences, string[] directories)
    {
        Process.GetCurrentProcess().ProcessorAffinity = (nint)256;
        string[] operations = ["LuminPackSerialize", "MemoryPackSerialize", "MessagePackSerialize", "NinoSerialize",
            "LuminPackDeserialize", "MemoryPackDeserialize", "MessagePackDeserialize", "NinoDeserialize"];
        var instances = new List<object>();
        var fixtures = directories.Select(directory =>
        {
            var type = BenchmarkImage.Load(directory).GetType("LuminPackBenchmark." + fixtureName, true)!;
            object instance = Activator.CreateInstance(type)!;
            type.GetMethod("SetUp")!.Invoke(instance, null);
            instances.Add(instance);
            string prefix = Path.GetFileName(Path.TrimEndingDirectorySeparator(directory)) + ".";
            return (Names: operations.Select(name => prefix + name).ToArray(),
                Actions: operations.Select(name => Bind(type.GetMethod(name)!, instance)).ToArray(),
                Cleanup: type.GetMethod("CleanUp")!.CreateDelegate<Action>(instance));
        }).ToArray();
        bool simple = fixtureName == "SimpleBenchmark";
        if (commonReferences)
        {
            SynchronizeCapacityInputs(fixtureName, instances);
            var shared = fixtures[0];
            int[] referenceIndices = [1, 5, 2, 6, 3, 7];
            fixtures = fixtures.Select(fixture => (Names: new[] { fixture.Names[0], fixture.Names[4] },
                Actions: new[] { fixture.Actions[0], fixture.Actions[4] }, fixture.Cleanup)).Append((
                Names: referenceIndices.Select(index => "reference." + operations[index]).ToArray(),
                Actions: referenceIndices.Select(index => shared.Actions[index]).ToArray(), Cleanup: (Action)(static () => { }))).ToArray();
        }
        MeasureFixtures(fixtures, simple ? 4096 : 128, simple ? 131072 : 1024,
            simple ? 16384 : 512, commonReferences ? 2 : operations.Length, commonReferences ? 1 : 4);
    }

    /// <summary>Clones one original payload across image-local model types without changing benchmark methods.</summary>
    /// <param name="fixtureName">The original fixture's declared model and buffer shape.</param>
    /// <param name="instances">Already initialized retained-image fixtures, with the canonical reference first.</param>
    private static void SynchronizeCapacityInputs(string fixtureName, List<object> instances)
    {
        const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
        bool character = fixtureName == "SerializeBenchmark";
        string bytesName = character ? "_luminBuffer" : "buffer1";
        string modelName = character ? "_data" : "myClass";
        string writerName = character ? "_luminWriter" : "luminBufferWriter";
        byte[] payload = (byte[])instances[0].GetType().GetField(bytesName, fields)!.GetValue(instances[0])!;
        foreach (object fixture in instances)
        {
            var type = fixture.GetType();
            object writer = type.GetField(writerName, fields)!.GetValue(fixture)!;
            writer.GetType().GetMethod("LoadSequence")!.Invoke(writer, [new System.Buffers.ReadOnlySequence<byte>(payload)]);
            var model = type.GetField(modelName, fields)!;
            var serializer = type.Assembly.GetType("LuminPack.LuminPackSerializer", true)!;
            bool compiledUtf16 = fixtureName != "SimpleBenchmark" && writer.GetType().Assembly.GetType("LuminPack.Option.LuminPackUtf16LengthOptions") != null;
            var read = serializer.GetMethods(BindingFlags.Public | BindingFlags.Static).Single(method =>
                method.Name == "Deserialize" && method.IsGenericMethodDefinition && method.GetGenericArguments().Length == (compiledUtf16 ? 2 : 1) &&
                method.GetParameters() is [var parameter] && parameter.ParameterType == writer.GetType());
            Type[] arguments = compiledUtf16 ? [model.FieldType, writer.GetType().Assembly.GetType("LuminPack.Option.LuminPackUtf16LengthOptions", true)!] : [model.FieldType];
            object restored = read.MakeGenericMethod(arguments).Invoke(null, [writer])!;
            model.SetValue(fixture, restored);
            type.GetField(bytesName, fields)!.SetValue(fixture, payload);
            type.GetMethod("LuminPackSerialize")!.Invoke(fixture, null);
            var copy = (Memory<byte>)writer.GetType().GetMethod("GetMemory")!.Invoke(writer, [0])!;
            if (!copy.Span.SequenceEqual(payload)) throw new InvalidOperationException("Canonical capacity fixture changed wire bytes across images.");
        }
    }

    /// <summary>Checks and measures identical fixed/managed Union distributions against common references.</summary>
    /// <param name="fixedPercent">The percentage of fixed payloads in the dictionary.</param>
    /// <param name="directories">Complete retained images with the same distribution fixture.</param>
    internal static void RunUnion(int fixedPercent, params string[] directories)
    {
        Process.GetCurrentProcess().ProcessorAffinity = (nint)256;
        string[] operations = ["LuminPackWrite", "MemoryPackWrite", "NinoWrite"];
        byte[]? expected = null;
        var fixtures = directories.Select(directory =>
        {
            var type = BenchmarkImage.Load(directory).GetType("LuminPackBenchmark.UnionCursorBenchmark", true)!;
            object fixture = Activator.CreateInstance(type)!;
            type.GetProperty("FixedPercent")!.SetValue(fixture, fixedPercent);
            type.GetMethod("Setup")!.Invoke(fixture, null);
            byte[] payload = (byte[])type.GetMethod("Snapshot")!.Invoke(fixture, null)!;
            if (expected != null && !payload.AsSpan().SequenceEqual(expected))
                throw new InvalidOperationException("The Union distribution changed wire bytes between images.");
            expected = payload;
            string prefix = Path.GetFileName(Path.TrimEndingDirectorySeparator(directory)) + ".";
            return (Names: operations.Select(name => prefix + name).ToArray(),
                Actions: operations.Select(name => Bind(type.GetMethod(name)!, fixture)).ToArray(),
                Cleanup: type.GetMethod("Cleanup")!.CreateDelegate<Action>(fixture));
        }).ToArray();
        MeasureFixtures(fixtures, 4096, 65536, 65536, operations.Length, operations.Length);
    }

    /// <summary>Compares unchanged JSON fixture operations with their original reference operations.</summary>
    /// <param name="directories">Complete retained benchmark images.</param>
    internal static void RunJson(params string[] directories)
    {
        Process.GetCurrentProcess().ProcessorAffinity = (nint)256;
        string[] operations = ["LuminPackSerializeToJson", "SystemTextJsonSerialize", "NewtonsoftJsonSerialize",
            "LuminPackDeserializeFromJson", "SystemTextJsonDeserialize", "NewtonsoftJsonDeserialize"];
        var fixtures = directories.Select(directory =>
        {
            var type = BenchmarkImage.Load(directory).GetType("LuminPackBenchmark.JsonBenchmark", true)!;
            object fixture = Activator.CreateInstance(type)!;
            type.GetMethod("SetUp")!.Invoke(fixture, null);
            string prefix = Path.GetFileName(Path.TrimEndingDirectorySeparator(directory)) + ".";
            return (Names: operations.Select(name => prefix + name).ToArray(),
                Actions: operations.Select(name => Bind(type.GetMethod(name)!, fixture)).ToArray(),
                Cleanup: type.GetMethod("CleanUp")!.CreateDelegate<Action>(fixture));
        }).ToArray();
        MeasureFixtures(fixtures, 32, 128, 128, operations.Length, 3);
    }
    /// <summary>Measures read/write reference ratios from one retained benchmark image.</summary>
    /// <param name="monomorphic">Whether the supplemental fixture uses the same first Union object in every dictionary entry.</param>
    /// <param name="character">Whether to use the original UTF-16 character graph instead of SimpleBenchmark.</param>
    /// <param name="writeOnly">Whether to measure only writes without interleaved allocating reads.</param>
    /// <param name="directories">Complete retained benchmark output directories, measured in the same rotating order.</param>
    internal static void Run(bool monomorphic, bool character, bool writeOnly, params string[] directories)
    {
        Process.GetCurrentProcess().ProcessorAffinity = (nint)256;
        var operations = new[] { "LuminPackSerialize", "MemoryPackSerialize", "NinoSerialize", "LuminPackDeserialize", "MemoryPackDeserialize", "NinoDeserialize" }
            .Take(writeOnly ? 3 : 6).ToArray();
        var fixtures = directories.Select(directory =>
        {
            var assembly = BenchmarkImage.Load(directory);
            var type = assembly.GetType("LuminPackBenchmark." + (character ? "SerializeBenchmark" : "SimpleBenchmark"), throwOnError: true)!;
            object instance = Activator.CreateInstance(type)!;
            if (monomorphic)
            {
                object value = type.GetField("myClass", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(instance)!;
                var dictionary = (IDictionary)value.GetType().GetField("Map1")!.GetValue(value)!;
                object first = dictionary.Values.Cast<object>().First();
                foreach (object key in dictionary.Keys.Cast<object>().ToArray()) dictionary[key] = first;
            }
            type.GetMethod("SetUp")!.Invoke(instance, null);
            string prefix = directories.Length == 1 ? string.Empty : Path.GetFileName(Path.TrimEndingDirectorySeparator(directory)) + ".";
            return (Names: operations.Select(name => prefix + name).ToArray(),
                Actions: operations.Select(name => Bind(type.GetMethod(name)!, instance)).ToArray(),
                Cleanup: type.GetMethod("CleanUp")!.CreateDelegate<Action>(instance));
        }).ToArray();
        MeasureFixtures(fixtures, character ? 1024 : 16384, character ? 8192 : 262144,
            character ? 4096 : 65536, operations.Length, writeOnly ? operations.Length : operations.Length / 2);
    }

    /// <summary>Compares string lengths through the retained fixture's existing read/write methods.</summary>
    /// <param name="shape">An original string shape, Tiny, or Mixed.</param>
    /// <param name="directories">Complete retained benchmark images.</param>
    internal static void RunStrings(string shape, params string[] directories)
    {
        Process.GetCurrentProcess().ProcessorAffinity = (nint)256;
        var operations = new[] { "LuminPackWrite", "MemoryPackWrite", "LuminPackRead", "MemoryPackRead" };
        var fixtures = directories.Select(directory =>
        {
            var type = BenchmarkImage.Load(directory).GetType("LuminPackBenchmark.BinaryStringRatioBenchmark", throwOnError: true)!;
            object instance = Activator.CreateInstance(type)!;
            type.GetProperty("Shape")!.SetValue(instance, shape);
            type.GetMethod("Setup")!.Invoke(instance, null);
            if (shape is "Tiny" or "Mixed")
            {
                string?[] choices = shape == "Tiny" ? ["a", "bc", "def"] :
                    [null, "", "a", "bc", "def", "four", "eight888", new string('a', 12), new string('b', 16),
                        new string('c', 24), new string('d', 32), new string('e', 33), new string('f', 64), "\u4e2d\u00e9\ud83c\udf1f"];
                string?[] expected = Enumerable.Range(0, 512).Select(index => choices[index % choices.Length]).ToArray();
                object data = type.GetField("_data", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(instance)!;
                var values = data.GetType().GetField("Values")!;
                values.SetValue(data, expected);
                foreach (string library in new[] { "LuminPack", "MemoryPack" })
                {
                    object payload = type.GetMethod(library + "Write")!.Invoke(instance, null)!;
                    type.GetField(library == "LuminPack" ? "_lp" : "_mp", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(instance, payload);
                    object restored = type.GetMethod(library + "Read")!.Invoke(instance, null)!;
                    if (!((string?[])values.GetValue(restored)!).SequenceEqual(expected))
                        throw new InvalidOperationException("The string ratio fixture lost data.");
                }
            }
            string prefix = Path.GetFileName(Path.TrimEndingDirectorySeparator(directory)) + ".";
            return (Names: operations.Select(name => prefix + name).ToArray(),
                Actions: operations.Select(name => Bind(type.GetMethod(name)!, instance)).ToArray(),
                Cleanup: (Action)(() => { }));
        }).ToArray();
        MeasureFixtures(fixtures, 256, 1024, 512, operations.Length, operations.Length / 2);
    }

    /// <summary>Measures raw keys and raw values independently against matching reference fixtures.</summary>
    /// <param name="directories">Complete retained benchmark output directories.</param>
    internal static void RunDictionarySides(params string[] directories)
    {
        Process.GetCurrentProcess().ProcessorAffinity = (nint)256;
        var operations = new[] { "LuminPackKey", "MemoryPackKey", "NinoKey", "LuminPackValue", "MemoryPackValue", "NinoValue" };
        var fixtures = directories.Select(directory =>
        {
            var type = BenchmarkImage.Load(directory).GetType("LuminPackBenchmark.DictionaryRawSideBenchmark", throwOnError: true)!;
            object instance = Activator.CreateInstance(type)!;
            type.GetMethod("Setup")!.Invoke(instance, null);
            string prefix = Path.GetFileName(Path.TrimEndingDirectorySeparator(directory)) + ".";
            return (Names: operations.Select(name => prefix + name).ToArray(),
                Actions: operations.Select(name => Bind(type.GetMethod(name)!, instance)).ToArray(),
                Cleanup: type.GetMethod("Cleanup")!.CreateDelegate<Action>(instance));
        }).ToArray();
        MeasureFixtures(fixtures, 1024, 16384, 16384, operations.Length, operations.Length);
    }

    /// <summary>Measures contiguous and gapped class layouts through concrete serializer calls.</summary>
    /// <param name="shape">Primitives, PaddedStructs or Gapped.</param>
    /// <param name="directories">Complete retained benchmark images.</param>
    internal static void RunFieldGroups(string shape, params string[] directories)
    {
        Process.GetCurrentProcess().ProcessorAffinity = (nint)256;
        var operations = new[] { "LuminPackWrite", "MemoryPackWrite", "NinoWrite", "LuminPackRead", "MemoryPackRead", "NinoRead" };
        var fixtures = directories.Select(directory =>
        {
            var type = BenchmarkImage.Load(directory).GetType("LuminPackBenchmark.ClassFieldCopyBenchmark", throwOnError: true)!;
            object instance = Activator.CreateInstance(type)!;
            type.GetProperty("Shape")!.SetValue(instance, shape);
            type.GetMethod("Setup")!.Invoke(instance, null);
            string prefix = Path.GetFileName(Path.TrimEndingDirectorySeparator(directory)) + ".";
            return (Names: operations.Select(name => prefix + name).ToArray(),
                Actions: operations.Select(name => Bind(type.GetMethod(name)!, instance)).ToArray(),
                Cleanup: type.GetMethod("Cleanup")!.CreateDelegate<Action>(instance));
        }).ToArray();
        MeasureFixtures(fixtures, 8192, 131072, 65536, operations.Length, operations.Length / 2);
    }

    /// <summary>Records rotating samples and allocation counts after a shared warmup.</summary>
    /// <param name="fixtures">Bound fixture operations and cleanup callbacks.</param>
    /// <param name="warmupIterations">Operations per warmup batch.</param>
    /// <param name="writeIterations">Operations per write sample.</param>
    /// <param name="readIterations">Operations per read sample.</param>
    /// <param name="operationCount">Operations per fixture.</param>
    /// <param name="writeCount">Leading write operations per fixture.</param>
    private static void MeasureFixtures((string[] Names, Action[] Actions, Action Cleanup)[] fixtures,
        int warmupIterations, int writeIterations, int readIterations, int operationCount, int writeCount)
    {
        var names = fixtures.SelectMany(fixture => fixture.Names).ToArray();
        var actions = fixtures.SelectMany(fixture => fixture.Actions).ToArray();
        try
        {
            var warmup = Stopwatch.StartNew();
            do { foreach (var action in actions) Measure(action, warmupIterations); } while (warmup.Elapsed.TotalSeconds < 6);
            Console.WriteLine("sample," + string.Join(",", names));
            for (int sample = 0; sample < 36; sample++)
            {
                var values = new double[actions.Length];
                for (int step = 0; step < actions.Length; step++)
                {
                    int index = sample % 2 == 0 ? (step + sample) % actions.Length : (actions.Length - 1 - step + sample) % actions.Length;
                    int iterations = index % operationCount < writeCount ? writeIterations : readIterations;
                    values[index] = Measure(actions[index], iterations);
                }
                Console.WriteLine(sample + "," + string.Join(",", values.Select(value => value.ToString("F4", System.Globalization.CultureInfo.InvariantCulture))));
            }
            for (int index = 0; index < actions.Length; index++)
            {
                long start = GC.GetAllocatedBytesForCurrentThread();
                for (int iteration = 0; iteration < 1024; iteration++) actions[index]();
                Console.WriteLine("allocation," + names[index] + "," + (GC.GetAllocatedBytesForCurrentThread() - start) / 1024);
            }
        }
        finally { foreach (var fixture in fixtures) fixture.Cleanup(); GC.KeepAlive(_sink); }
    }

    /// <summary>Binds an original operation while retaining returned character objects through a common wrapper.</summary>
    /// <param name="method">The fixture method.</param>
    /// <param name="instance">The initialized fixture.</param>
    /// <returns>A non-reflective timed invocation.</returns>
    private static Action Bind(MethodInfo method, object instance)
    {
        if (method.ReturnType == typeof(void)) return method.CreateDelegate<Action>(instance);
        var operation = method.CreateDelegate<Func<object>>(instance);
        return () => _sink = operation();
    }

    /// <summary>Times only the original fixture operation and a shared delegate loop.</summary>
    /// <param name="operation">The unchanged benchmark method.</param>
    /// <param name="iterations">The operation count in one sample.</param>
    /// <returns>Nanoseconds per operation, including the common delegate boundary.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static double Measure(Action operation, int iterations)
    {
        long start = Stopwatch.GetTimestamp();
        for (int index = 0; index < iterations; index++) operation();
        return (Stopwatch.GetTimestamp() - start) * (1e9 / Stopwatch.Frequency) / iterations;
    }
}
