using System.Buffers;
using System.Reflection;

/// <summary>Checks the unchanged polymorphic Simple fixture across retained serializer images.</summary>
internal static class SimpleWireVerification
{
    /// <summary>Reads both original payloads in both implementations and compares their rewritten bytes.</summary>
    /// <param name="baselineDirectory">The complete baseline benchmark image.</param>
    /// <param name="candidateDirectory">The complete candidate benchmark image.</param>
    internal static void Run(string baselineDirectory, string candidateDirectory)
    {
        var images = new[] { Load(baselineDirectory), Load(candidateDirectory) };
        try
        {
            foreach (var source in images)
            foreach (var target in images)
            {
                var fixtureType = target.Fixture.GetType();
                var buffer = fixtureType.GetField("luminBufferWriter", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .GetValue(target.Fixture)!;
                buffer.GetType().GetMethod("LoadSequence")!.Invoke(buffer, [new ReadOnlySequence<byte>(source.Payload)]);
                var serializer = fixtureType.Assembly.GetType("LuminPack.LuminPackSerializer", throwOnError: true)!;
                var root = fixtureType.Assembly.GetType("LuminPackBenchmark.SimpleClassBase", throwOnError: true)!;
                var read = serializer.GetMethods(BindingFlags.Public | BindingFlags.Static).Single(method =>
                    method.Name == "Deserialize" && method.IsGenericMethodDefinition &&
                    method.GetParameters() is [var parameter] && parameter.ParameterType == buffer.GetType());
                object restored = read.MakeGenericMethod(root).Invoke(null, [buffer])!;
                fixtureType.GetField("myClass", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(target.Fixture, restored);
                fixtureType.GetMethod("LuminPackSerialize")!.Invoke(target.Fixture, null);
                var memory = (Memory<byte>)buffer.GetType().GetMethod("GetMemory")!.Invoke(buffer, [0])!;
                if (!memory.Span.SequenceEqual(source.Payload))
                    throw new InvalidOperationException($"Simple wire mismatch: {source.Directory} -> {target.Directory}.");
            }
            Console.WriteLine($"Passed four original Simple wire combinations ({images[0].Payload.Length}/{images[1].Payload.Length} bytes); no latency was measured.");
        }
        finally
        {
            foreach (var image in images) image.Fixture.GetType().GetMethod("CleanUp")!.Invoke(image.Fixture, null);
        }
    }

    /// <summary>Initializes the original fixture and captures its byte-array entry output.</summary>
    /// <param name="directory">A complete retained benchmark image.</param>
    /// <returns>The fixture and its original payload.</returns>
    private static (string Directory, object Fixture, byte[] Payload) Load(string directory)
    {
        var type = BenchmarkImage.Load(directory).GetType("LuminPackBenchmark.SimpleBenchmark", throwOnError: true)!;
        object fixture = Activator.CreateInstance(type)!;
        type.GetMethod("SetUp")!.Invoke(fixture, null);
        return (directory, fixture, (byte[])type.GetField("buffer1", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(fixture)!);
    }
}
