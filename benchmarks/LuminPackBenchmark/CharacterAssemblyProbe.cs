using System.Runtime.CompilerServices;
using LuminPack;
using LuminPack.Option;
using LuminPack.Utility;

namespace LuminPackBenchmark;

internal static class CharacterAssemblyProbe
{
    private static readonly LuminPackSerializerOption Strict = new() { ValidateJsonSyntax = true };
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

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static byte[] WriteFixedFields(CharacterFixedFieldsRatioBenchmark benchmark) => benchmark.LuminPackWrite();

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

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void WriteBinary(LuminBufferWriter writer, CharacterSaveData data) => LuminPackSerializer.Serialize(data, writer);
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static CharacterSaveData ReadBinary(byte[] data) => LuminPackSerializer.Deserialize<CharacterSaveData>(data);
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void WriteJson(LuminBufferWriter writer, CharacterSaveData data) => LuminPackSerializer.SerializeJson(data, writer);
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static CharacterSaveData ReadJson(byte[] data) => LuminPackSerializer.DeserializeJson<CharacterSaveData>(data, Strict);
}
