using System.Buffers;
using System.Reflection;
using System.Text;

/// <summary>Verifies unchanged polymorphic JSON documents across retained serializer implementations.</summary>
internal static class JsonWireVerification
{
    /// <summary>Reads and rewrites both implementations' JSON using owned contexts and scoped span entries.</summary>
    /// <param name="baselineDirectory">The retained baseline image.</param>
    /// <param name="candidateDirectory">The candidate image.</param>
    internal static void Run(string baselineDirectory, string candidateDirectory)
    {
        var images = new[] { Load(baselineDirectory), Load(candidateDirectory) };
        try
        {
            foreach (var source in images)
            foreach (var target in images)
            {
                var bufferType = target.Buffer.GetType();
                var option = bufferType.GetProperty("Option")?.GetValue(target.Buffer);
                if (option != null) option.GetType().GetProperty("StringEncoding")!.SetValue(option,
                    Enum.Parse(option.GetType().GetProperty("StringEncoding")!.PropertyType, "UTF8"));
                bufferType.GetMethod("LoadSequence")!.Invoke(target.Buffer, [new ReadOnlySequence<byte>(Encoding.UTF8.GetBytes(source.Json))]);
                var serializer = target.Fixture.GetType().Assembly.GetType("LuminPack.LuminPackSerializer", true)!;
                var root = target.Fixture.GetType().Assembly.GetType("LuminPackBenchmark.SimpleClassBase", true)!;
                var read = serializer.GetMethods(BindingFlags.Public | BindingFlags.Static).Single(method =>
                    method.Name == "DeserializeJson" && method.IsGenericMethodDefinition && method.GetGenericArguments().Length == 1 &&
                    method.GetParameters() is [var parameter] && parameter.ParameterType == bufferType);
                object restored = read.MakeGenericMethod(root).Invoke(null, [target.Buffer])!;
                var write = serializer.GetMethods(BindingFlags.Public | BindingFlags.Static).Single(method =>
                    method.Name == "SerializeJson" && method.IsGenericMethodDefinition && method.GetGenericArguments().Length == 1 &&
                    method.ReturnType == typeof(string) && method.GetParameters()[0].ParameterType.IsGenericParameter &&
                    method.GetParameters().Length == (option == null ? 1 : 2));
                string actual = (string)write.MakeGenericMethod(root).Invoke(null, option == null ? [restored] : [restored, option])!;
                if (actual != source.Json) throw new InvalidOperationException("The retained JSON implementations disagree.");
            }
            Console.WriteLine($"Passed four original polymorphic JSON read/rewrite combinations ({images[0].Json.Length}/{images[1].Json.Length} characters); no latency was measured.");
        }
        finally
        {
            foreach (var image in images) image.Fixture.GetType().GetMethod("CleanUp")!.Invoke(image.Fixture, null);
        }
    }

    /// <summary>Captures JSON for the original polymorphic fixture.</summary>
    /// <param name="directory">One retained benchmark image.</param>
    /// <returns>The fixture, owned buffer and original JSON.</returns>
    private static (object Fixture, object Buffer, string Json) Load(string directory)
    {
        var assembly = BenchmarkImage.Load(directory);
        var type = assembly.GetType("LuminPackBenchmark.SimpleBenchmark", true)!;
        object fixture = Activator.CreateInstance(type)!;
        type.GetMethod("SetUp")!.Invoke(fixture, null);
        object root = type.GetField("myClass", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(fixture)!;
        object buffer = type.GetField("luminBufferWriter", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(fixture)!;
        var serializer = assembly.GetType("LuminPack.LuminPackSerializer", true)!;
        bool compiled = buffer.GetType().GetProperty("Option") == null;
        var write = serializer.GetMethods(BindingFlags.Public | BindingFlags.Static).Single(method =>
            method.Name == "SerializeJson" && method.IsGenericMethodDefinition && method.GetGenericArguments().Length == 1 &&
            method.ReturnType == typeof(string) && method.GetParameters()[0].ParameterType.IsGenericParameter &&
            method.GetParameters().Length == (compiled ? 1 : 2));
        string json = (string)write.MakeGenericMethod(assembly.GetType("LuminPackBenchmark.SimpleClassBase", true)!).Invoke(null, compiled ? [root] : [root, null])!;
        return (fixture, buffer, json);
    }
}
