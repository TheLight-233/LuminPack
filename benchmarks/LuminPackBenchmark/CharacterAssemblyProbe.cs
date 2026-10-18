using System.Runtime.CompilerServices;
using LuminPack;
using LuminPack.Option;
using LuminPack.Utility;

namespace LuminPackBenchmark;

/// <summary>Provides character assembly probe functionality.</summary>
internal static class CharacterAssemblyProbe
{
    private static readonly BenchmarkProfile Strict = new() { ValidateJsonSyntax = true };
    /// <summary>Measures run fixed fields for the configured benchmark input.</summary>
    internal static void RunFixedFields()
    {
        if (OperatingSystem.IsWindows())
            System.Diagnostics.Process.GetCurrentProcess().ProcessorAffinity = (nint)256;
        var benchmark = new CharacterFixedFieldsRatioBenchmark();
        benchmark.Setup();
        long bytes = 0;
        // More elapsed warmup lets background tiered compilation complete on
        // the same logical processor used by the performance benchmarks.
        for (int pass = 0; pass < 32; pass++)
        {
            for (int i = 0; i < 65536; i++)
            {
                byte[] result = WriteFixedFields(benchmark);
                if (result.Length != 20997)
                    throw new InvalidOperationException("Fixed-field probe changed payload size.");
                bytes += result.Length;
            }
        }
        Console.WriteLine($"Fixed-field assembly probe OK: {bytes} output bytes observed.");
    }

    /// <summary>Writes fixed fields to the current output.</summary>
    /// <param name="benchmark">The benchmark used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static byte[] WriteFixedFields(CharacterFixedFieldsRatioBenchmark benchmark) => benchmark.LuminPackWrite();

    /// <summary>Measures run for the configured benchmark input.</summary>
    internal static void Run()
    {
        var data = OptimizationBenchmark.CreateDeterministicData();
        var writer = LuminBufferWriterPool.Rent();
        byte[] binary = LuminPackSerializer.Serialize(data);
        byte[] json = System.Text.Encoding.UTF8.GetBytes(LuminPackSerializer.SerializeJson(data));
        CharacterSaveData result = null!;
        try
        {
            for (int i = 0; i < 30_000; i++)
            {
                WriteBinary(writer, data);
                result = ReadBinary(binary);
                WriteJson(writer, data);
                result = ReadJson(json);
            }
            if (LuminPackSerializer.SerializeJson(result) != LuminPackSerializer.SerializeJson(data))
                throw new InvalidOperationException("Assembly probe lost object data.");
            Console.WriteLine($"Character assembly probe OK: {binary.Length} binary bytes, {json.Length} JSON bytes.");
        }
        finally { LuminBufferWriterPool.Return(writer); }
    }

    /// <summary>Writes binary to the current output.</summary>
    /// <param name="writer">The writer receiving the output data.</param>
    /// <param name="data">The metadata describing the serializable type.</param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void WriteBinary(LuminBufferWriter writer, CharacterSaveData data)
    {
        writer.ResetCore();
        writer.WriterState.ResetOperationState();
        LuminPackSerializer.Serialize(data, writer);
    }
    /// <summary>Reads binary from the current input.</summary>
    /// <param name="data">The metadata describing the serializable type.</param>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static CharacterSaveData ReadBinary(byte[] data) => LuminPackSerializer.Deserialize<CharacterSaveData>(data);
    /// <summary>Writes json to the current output.</summary>
    /// <param name="writer">The writer receiving the output data.</param>
    /// <param name="data">The metadata describing the serializable type.</param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void WriteJson(LuminBufferWriter writer, CharacterSaveData data)
    {
        writer.ResetCore();
        writer.WriterState.ResetOperationState();
        LuminPackSerializer.SerializeJson(data, writer);
    }
    /// <summary>Reads json from the current input.</summary>
    /// <param name="data">The metadata describing the serializable type.</param>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static CharacterSaveData ReadJson(byte[] data) => LuminPackSerializer.DeserializeJson<CharacterSaveData>(data, Strict);
}
