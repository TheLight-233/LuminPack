using System.Runtime.CompilerServices;
using LuminPack;
using LuminPack.Option;
using LuminPack.Utility;

namespace LuminPackBenchmark;

/// <summary>Provides character save data jit probe functionality.</summary>
internal static class CharacterSaveDataJitProbe
{
    private const int Iterations = 100_000;

    /// <summary>Measures run for the configured benchmark input.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void Run()
    {
        var data = CharacterSaveData.Create();
        var payload = LuminPackSerializer.Serialize<LuminPackUtf16LengthOptions>(data);
        var writer = LuminBufferWriterPool.Rent();

        CharacterSaveData? result = null;
        try
        {
            for (var i = 0; i < Iterations; i++)
            {
                writer.ResetForReuse();
                LuminPackSerializer.Serialize<LuminPackUtf16LengthOptions>(data, writer);
                result = LuminPackSerializer.Deserialize<CharacterSaveData, LuminPackUtf16LengthOptions>(payload);
            }
        }
        finally
        {
            LuminBufferWriterPool.Return(writer);
        }

        Console.WriteLine($"Character JIT probe complete: {result?.CharacterId}");
    }
}
