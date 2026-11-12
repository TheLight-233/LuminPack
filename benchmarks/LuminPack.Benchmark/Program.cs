using BenchmarkDotNet.Running;
using LuminPack.Attribute;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Reflection;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;
using System.Security.Cryptography;
using System.Text;

[assembly: LuminPackGeneratorOptions(LuminPackGenerationMode.Minimal)]

/// <summary>Runs benchmark classes and their separate native-host diagnostics.</summary>
internal static class Program
{
    /// <summary>Starts BenchmarkDotNet or a native-host diagnostic.</summary>
    /// <param name="args">BenchmarkDotNet arguments or a native-host diagnostic option.</param>
    /// <returns>Zero for a successful invocation, or one for a benchmark or diagnostic failure.</returns>
    private static int Main(string[] args)
    {
        Console.Error.WriteLine("LuminPack generator: " + (typeof(Program).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => attribute.Key == "LuminPack.Generator")?.Value ?? "unknown"));
        if (args is ["--verify-host", var architecture, var vendor])
            return VerifyHost(architecture, vendor);
        if (args is ["--polymorphism-native-probe", var operation])
            return CapturePolymorphismNativeCode(operation);
        if (args is ["--verify-fixtures"])
            return VerifyFixtures();
        if (args is ["--verify-roundtrip"])
            return VerifyRoundTrip();

        int jitModeIndex = Array.IndexOf(args, "--jit-mode");
        if (jitModeIndex >= 0)
        {
            if (jitModeIndex + 1 >= args.Length || args[jitModeIndex + 1] is not ("tiered" or "fullopts" or "both"))
            {
                Console.Error.WriteLine("--jit-mode requires tiered, fullopts or both.");
                return 1;
            }
            Environment.SetEnvironmentVariable(LuminPackBenchmark.BenchmarkConfig.JitModeVariable, args[jitModeIndex + 1]);
            args = args.Take(jitModeIndex).Concat(args.Skip(jitModeIndex + 2)).ToArray();
        }

        var summaries = BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args).ToArray();
        if (summaries.Length == 0)
            return args.Any(argument => argument is "--list" or "--help" or "--version" or "-h") ? 0 : 1;
        return summaries.Any(summary => summary.HasCriticalValidationErrors ||
            summary.Reports.Any(report => !report.Success)) ? 1 : 0;
    }

    /// <summary>Verifies repeated setup and fresh fixtures have identical serialized input bytes.</summary>
    /// <returns>Zero for reproducible fixtures, or one when a payload changes.</returns>
    /// <summary>Reads every benchmark payload into fresh and existing targets through the ordinary serializer entries and
    /// checks that writing the result reproduces the original bytes.</summary>
    /// <remarks>No formatter is registered, so a call that is not statically bound to a generated root fails here.</remarks>
    private static int VerifyRoundTrip()
    {
        int failures = 0;
        void Check(string name, ReadOnlySpan<byte> expected, ReadOnlySpan<byte> actual)
        {
            bool same = expected.SequenceEqual(actual);
            Console.WriteLine($"{name}: {(same ? "ok" : "MISMATCH")} ({actual.Length} bytes)");
            if (!same) failures++;
        }

        using var buffer = LuminPack.Utility.LuminBufferWriterPool.Rent();

        LuminPackBenchmark.SimpleClassBase simple = LuminPackBenchmark.SimpleClass.Create();
        byte[] simpleBytes = LuminPack.LuminPackSerializer.Serialize(simple);
        LuminPackBenchmark.SimpleClassBase simpleRead = null!;
        int simpleConsumed = LuminPack.LuminPackSerializer.Deserialize(simpleBytes, ref simpleRead);
        Check("Simple fresh read", simpleBytes, LuminPack.LuminPackSerializer.Serialize(simpleRead));
        Check("Simple consumed", BitConverter.GetBytes(simpleBytes.Length), BitConverter.GetBytes(simpleConsumed));
        LuminPack.LuminPackSerializer.Deserialize(simpleBytes, ref simpleRead);
        Check("Simple existing target", simpleBytes, LuminPack.LuminPackSerializer.Serialize(simpleRead));
        buffer.ResetCore();
        LuminPack.LuminPackSerializer.Serialize(simple, buffer);
        Check("Simple buffer writer", simpleBytes, buffer.GetSpan());
        Check("Simple returning read", simpleBytes,
            LuminPack.LuminPackSerializer.Serialize(LuminPack.LuminPackSerializer.Deserialize<LuminPackBenchmark.SimpleClassBase>(simpleBytes)));

        LuminPackBenchmark.CharacterSaveData data = LuminPackBenchmark.CharacterSaveData.Create();
        byte[] dataBytes = LuminPack.LuminPackSerializer.Serialize<LuminPackBenchmark.CharacterSaveData, LuminPack.Option.LuminPackUtf16LengthOptions>(data);
        LuminPackBenchmark.CharacterSaveData dataRead = null!;
        LuminPack.LuminPackSerializer.Deserialize<LuminPackBenchmark.CharacterSaveData, LuminPack.Option.LuminPackUtf16LengthOptions>(dataBytes, ref dataRead);
        Check("CharacterSaveData fresh read", dataBytes,
            LuminPack.LuminPackSerializer.Serialize<LuminPackBenchmark.CharacterSaveData, LuminPack.Option.LuminPackUtf16LengthOptions>(dataRead));
        LuminPack.LuminPackSerializer.Deserialize<LuminPackBenchmark.CharacterSaveData, LuminPack.Option.LuminPackUtf16LengthOptions>(dataBytes, ref dataRead);
        Check("CharacterSaveData existing target", dataBytes,
            LuminPack.LuminPackSerializer.Serialize<LuminPackBenchmark.CharacterSaveData, LuminPack.Option.LuminPackUtf16LengthOptions>(dataRead));
        Check("CharacterSaveData declared profile", dataBytes, LuminPack.LuminPackSerializer.Serialize(data));
        buffer.ResetCore();
        LuminPack.LuminPackSerializer.Serialize<LuminPackBenchmark.CharacterSaveData, LuminPack.Option.LuminPackUtf16LengthOptions>(data, buffer);
        Check("CharacterSaveData buffer writer", dataBytes, buffer.GetSpan());
        var bufferWriter = new System.Buffers.ArrayBufferWriter<byte>();
        LuminPack.LuminPackSerializer.Serialize(data, bufferWriter);
        Check("CharacterSaveData IBufferWriter", dataBytes, bufferWriter.WrittenSpan);

        string json = LuminPack.LuminPackSerializer.SerializeJson<LuminPackBenchmark.CharacterSaveData, LuminPack.Option.LuminPackDefaultOptions>(data);
        LuminPackBenchmark.CharacterSaveData jsonRead = null!;
        LuminPack.LuminPackSerializer.DeserializeJson<LuminPackBenchmark.CharacterSaveData, LuminPack.Option.LuminPackDefaultOptions>(json, ref jsonRead);
        Check("JSON fresh read", Encoding.UTF8.GetBytes(json), Encoding.UTF8.GetBytes(
            LuminPack.LuminPackSerializer.SerializeJson<LuminPackBenchmark.CharacterSaveData, LuminPack.Option.LuminPackDefaultOptions>(jsonRead)));
        LuminPack.LuminPackSerializer.DeserializeJson<LuminPackBenchmark.CharacterSaveData, LuminPack.Option.LuminPackDefaultOptions>(json, ref jsonRead);
        Check("JSON existing target", Encoding.UTF8.GetBytes(json), Encoding.UTF8.GetBytes(
            LuminPack.LuminPackSerializer.SerializeJson<LuminPackBenchmark.CharacterSaveData, LuminPack.Option.LuminPackDefaultOptions>(jsonRead)));
        Check("JSON returning read", Encoding.UTF8.GetBytes(json), Encoding.UTF8.GetBytes(
            LuminPack.LuminPackSerializer.SerializeJson<LuminPackBenchmark.CharacterSaveData, LuminPack.Option.LuminPackDefaultOptions>(
                LuminPack.LuminPackSerializer.DeserializeJson<LuminPackBenchmark.CharacterSaveData, LuminPack.Option.LuminPackDefaultOptions>(json))));
        Check("JSON binary of JSON read", dataBytes,
            LuminPack.LuminPackSerializer.Serialize<LuminPackBenchmark.CharacterSaveData, LuminPack.Option.LuminPackUtf16LengthOptions>(jsonRead));

        LuminPackBenchmark.PolymorphismClassBase polymorphic = LuminPackBenchmark.PolymorphismClass.Create();
        byte[] polymorphicBytes = LuminPack.LuminPackSerializer.Serialize<LuminPackBenchmark.PolymorphismClassBase, LuminPack.Option.LuminPackUtf16LengthOptions>(polymorphic);
        LuminPackBenchmark.PolymorphismClassBase polymorphicRead = null!;
        LuminPack.LuminPackSerializer.Deserialize<LuminPackBenchmark.PolymorphismClassBase, LuminPack.Option.LuminPackUtf16LengthOptions>(polymorphicBytes, ref polymorphicRead);
        Check("Polymorphism fresh read", polymorphicBytes,
            LuminPack.LuminPackSerializer.Serialize<LuminPackBenchmark.PolymorphismClassBase, LuminPack.Option.LuminPackUtf16LengthOptions>(polymorphicRead));
        LuminPack.LuminPackSerializer.Deserialize<LuminPackBenchmark.PolymorphismClassBase, LuminPack.Option.LuminPackUtf16LengthOptions>(polymorphicBytes, ref polymorphicRead);
        Check("Polymorphism existing target", polymorphicBytes,
            LuminPack.LuminPackSerializer.Serialize<LuminPackBenchmark.PolymorphismClassBase, LuminPack.Option.LuminPackUtf16LengthOptions>(polymorphicRead));
        buffer.ResetCore();
        LuminPack.LuminPackSerializer.Serialize<LuminPackBenchmark.PolymorphismClassBase, LuminPack.Option.LuminPackUtf16LengthOptions>(polymorphic, buffer);
        Check("Polymorphism buffer writer", polymorphicBytes, buffer.GetSpan());
        Console.WriteLine(failures == 0 ? "Round trip verified." : $"{failures} round-trip check(s) failed.");
        return failures == 0 ? 0 : 1;
    }

    private static int VerifyFixtures()
    {
        Type[] types = [typeof(LuminPackBenchmark.SimpleBenchmark), typeof(LuminPackBenchmark.SerializeBenchmark),
            typeof(LuminPackBenchmark.JsonBenchmark), typeof(LuminPackBenchmark.PolymorphismBenchmark)];
        foreach (Type type in types)
        {
            var expected = new Dictionary<string, byte[]>();
            for (int instanceIndex = 0; instanceIndex < 2; instanceIndex++)
            {
                object instance = Activator.CreateInstance(type)!;
                try
                {
                    for (int setup = 0; setup < 2; setup++)
                    {
                        type.GetMethod("SetUp")!.Invoke(instance, null);
                        foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic))
                        {
                            byte[]? payload = field.GetValue(instance) switch
                            {
                                byte[] bytes => bytes,
                                string json => Encoding.UTF8.GetBytes(json),
                                _ => null
                            };
                            if (payload is null) continue;
                            if (expected.TryGetValue(field.Name, out byte[]? original))
                            {
                                if (!original.AsSpan().SequenceEqual(payload))
                                {
                                    Console.Error.WriteLine($"{type.Name}.{field.Name} changed between fixture setups.");
                                    return 1;
                                }
                            }
                            else expected.Add(field.Name, payload.ToArray());
                        }
                    }
                }
                finally { type.GetMethod("CleanUp")!.Invoke(instance, null); }
            }
            foreach (var payload in expected.OrderBy(pair => pair.Key, StringComparer.Ordinal))
                Console.WriteLine($"{type.Name}.{payload.Key}: {payload.Value.Length} bytes; SHA256 {Convert.ToHexString(SHA256.HashData(payload.Value))}");
        }
        return 0;
    }

    /// <summary>Checks the actual .NET process architecture and optional x86 CPU vendor before timing.</summary>
    /// <param name="architecture">The required native architecture, such as x64 or arm64.</param>
    /// <param name="vendor">The required CPUID vendor, or any to accept the detected vendor.</param>
    /// <returns>Zero when the native host matches, or one otherwise.</returns>
    private static int VerifyHost(string architecture, string vendor)
    {
        string actualArchitecture = RuntimeInformation.ProcessArchitecture.ToString();
        string actualVendor = "non-x86";
        if (X86Base.IsSupported)
        {
            var cpu = X86Base.CpuId(0, 0);
            Span<byte> bytes = stackalloc byte[12];
            BinaryPrimitives.WriteInt32LittleEndian(bytes, cpu.Ebx);
            BinaryPrimitives.WriteInt32LittleEndian(bytes[4..], cpu.Edx);
            BinaryPrimitives.WriteInt32LittleEndian(bytes[8..], cpu.Ecx);
            actualVendor = Encoding.ASCII.GetString(bytes);
        }

        Console.WriteLine($"Process architecture: {actualArchitecture}; OS architecture: {RuntimeInformation.OSArchitecture}; CPU vendor: {actualVendor}");
        Console.WriteLine($"{RuntimeInformation.OSDescription}; {RuntimeInformation.FrameworkDescription}");
        if (!string.Equals(actualArchitecture, architecture, StringComparison.OrdinalIgnoreCase) ||
            RuntimeInformation.ProcessArchitecture != RuntimeInformation.OSArchitecture ||
            (vendor != "any" && !string.Equals(actualVendor, vendor, StringComparison.Ordinal)))
        {
            Console.Error.WriteLine($"Expected a native {architecture} host with CPU vendor {vendor}.");
            return 1;
        }

        return 0;
    }

    /// <summary>Exercises one existing polymorphism operation for a separate JIT disassembly capture.</summary>
    /// <param name="operation">The LuminPack or Nino read or write operation to exercise.</param>
    /// <returns>Zero when the selected operation completed, or one when it is unknown.</returns>
    private static int CapturePolymorphismNativeCode(string operation)
    {
        var benchmark = new LuminPackBenchmark.PolymorphismBenchmark();
        Action? action = operation switch
        {
            "lumin-write" => benchmark.LuminPackSerialize,
            "lumin-read" => benchmark.LuminPackDeserialize,
            "nino-write" => benchmark.NinoSerialize,
            "nino-read" => benchmark.NinoDeserialize,
            _ => null
        };
        if (action is null)
        {
            Console.Error.WriteLine($"Unknown polymorphism native probe operation: {operation}.");
            benchmark.CleanUp();
            return 1;
        }

        Console.Error.WriteLine($"Native probe: {operation}; {RuntimeInformation.FrameworkDescription}; {RuntimeInformation.ProcessArchitecture}; server GC: {GCSettings.IsServerGC}");
        try
        {
            benchmark.SetUp();
            DescribePolymorphismRoots();
            var timer = Stopwatch.StartNew();
            long operations = 0;
            do
            {
                for (int i = 0; i < 256; i++) action();
                operations += 256;
            }
            while (timer.Elapsed.TotalSeconds < 8);

            Console.Error.WriteLine($"Completed {operations} operations in {timer.Elapsed.TotalSeconds:F3} seconds.");
            return 0;
        }
        finally
        {
            benchmark.CleanUp();
        }
    }

    /// <summary>Identifies the generated collection methods in the accompanying JIT listings.</summary>
    private static void DescribePolymorphismRoots()
    {
        foreach (Type type in typeof(Program).Assembly.GetTypes().Where(type =>
            type.IsNested && (type.Name.StartsWith("LuminRoots_", StringComparison.Ordinal) || type.Name.EndsWith("Formatter", StringComparison.Ordinal))))
        {
            foreach (MethodInfo method in type.GetMethods(BindingFlags.Static | BindingFlags.NonPublic))
            {
                if (method.Name is not ("RootRead" or "RootWrite" or "Read" or "Write")) continue;
                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length != 2) continue;
                Type parameter = parameters[1].ParameterType;
                if (parameter.IsByRef) parameter = parameter.GetElementType()!;
                if (!parameter.IsGenericType || !parameter.GenericTypeArguments.Contains(typeof(LuminPackBenchmark.IFoo))) continue;

                int? ilBytes = method.GetMethodBody()?.GetILAsByteArray()?.Length;
                Console.Error.WriteLine($"Generated root: {type.FullName}:{method.Name}; parameter: {parameter}; IL bytes: {ilBytes}");
            }
        }
    }
}
