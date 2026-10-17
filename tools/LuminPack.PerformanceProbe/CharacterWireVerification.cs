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
                var write = serializer.GetMethods(BindingFlags.Public | BindingFlags.Static).Single(method =>
                    method.Name == "Serialize" && !method.IsGenericMethod && method.ReturnType == typeof(byte[]) &&
                    method.GetParameters() is [var payload, var option] &&
                    payload.ParameterType.GetElementType() == restored.GetType() && option.ParameterType == target.Options.GetType());
                byte[] rewritten = (byte[])write.Invoke(null, [restored, target.Options])!;
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
    private static (string Directory, object Fixture, byte[] Payload, object Options) Load(string directory)
    {
        var assembly = BenchmarkImage.Load(directory);
        var type = assembly.GetType("LuminPackBenchmark.SerializeBenchmark", throwOnError: true)!;
        object fixture = Activator.CreateInstance(type)!;
        type.GetMethod("SetUp")!.Invoke(fixture, null);
        return (directory, fixture,
            (byte[])type.GetField("_luminBuffer", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(fixture)!,
            type.GetField("luminPackSerializerOption", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(fixture)!);
    }
}
