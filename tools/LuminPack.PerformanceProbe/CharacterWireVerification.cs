using System.Reflection;

/// <summary>Checks original character-fixture bytes across two retained build images without measuring throughput.</summary>
internal static class CharacterWireVerification
{
    /// <summary>Reads each image's original character payload in both implementations and compares the rewritten bytes.</summary>
    /// <param name="baselineDirectory">The complete retained baseline benchmark output.</param>
    /// <param name="candidateDirectory">The complete candidate benchmark output.</param>
    internal static void Run(string baselineDirectory, string candidateDirectory)
    {
        var images = new[] { Load(baselineDirectory), Load(candidateDirectory) };
        try
        {
            foreach (var source in images)
            foreach (var target in images)
            {
                target.Fixture.GetType().GetField("_luminBuffer", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .SetValue(target.Fixture, source.Payload);
                object restored = target.Fixture.GetType().GetMethod("LuminPackDeserialize")!.Invoke(target.Fixture, null)!;
                var serializer = target.Fixture.GetType().Assembly.GetType("LuminPack.LuminPackSerializer", throwOnError: true)!;
                var write = serializer.GetMethods(BindingFlags.Public | BindingFlags.Static).SingleOrDefault(method =>
                    method.Name == "Serialize" && method.ReturnType == typeof(byte[]) &&
                    (method.GetParameters()[0].ParameterType.GetElementType() ?? method.GetParameters()[0].ParameterType) ==
                        target.Fixture.GetType().GetMethod("LuminPackDeserialize")!.ReturnType &&
                    (target.Options == null ? method.IsGenericMethodDefinition && method.GetGenericArguments().Length == 1 && method.GetParameters().Length == 1 :
                        !method.IsGenericMethod && method.GetParameters() is [_, var option] && option.ParameterType == target.Options.GetType())) ??
                    throw new InvalidOperationException("Missing character array entry in " + target.Directory + " (options " + target.Options?.GetType() + "): " +
                        string.Join("; ", serializer.GetMethods(BindingFlags.Public | BindingFlags.Static).Where(method => method.Name == "Serialize" &&
                            method.GetParameters()[0].ParameterType.ToString().Contains("CharacterSaveData", StringComparison.Ordinal)).Select(method => method.ToString())));
                if (target.Options == null)
                {
                    var optionsAssembly = write.GetGenericArguments()[0].GetGenericParameterConstraints().Single(type => type.IsInterface).Assembly;
                    write = write.MakeGenericMethod(optionsAssembly.GetType("LuminPack.Option.LuminPackUtf16LengthOptions", true)!);
                }
                byte[] rewritten = (byte[])write.Invoke(null, target.Options == null ? [restored] : [restored, target.Options])!;
                if (!rewritten.AsSpan().SequenceEqual(source.Payload))
                    throw new InvalidOperationException($"Character wire mismatch: {source.Directory} -> {target.Directory}.");
            }
            Console.WriteLine($"Passed four original UTF-16 character wire combinations ({images[0].Payload.Length}/{images[1].Payload.Length} bytes); no latency was measured.");
        }
        finally
        {
            foreach (var image in images) image.Fixture.GetType().GetMethod("CleanUp")!.Invoke(image.Fixture, null);
        }
    }

    /// <summary>Initializes an unchanged original fixture in its own retained-image load context.</summary>
    /// <param name="directory">A complete benchmark output directory.</param>
    /// <returns>The fixture, its original payload, and its serialization options.</returns>
    private static (string Directory, object Fixture, byte[] Payload, object? Options) Load(string directory)
    {
        var assembly = BenchmarkImage.Load(directory);
        var type = assembly.GetType("LuminPackBenchmark.SerializeBenchmark", throwOnError: true)!;
        object fixture = Activator.CreateInstance(type)!;
        type.GetMethod("SetUp")!.Invoke(fixture, null);
        return (directory, fixture,
            (byte[])type.GetField("_luminBuffer", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(fixture)!,
            type.GetField("luminPackSerializerOption", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(fixture));
    }
}
