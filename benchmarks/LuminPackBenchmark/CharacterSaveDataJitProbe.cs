using System.Runtime.CompilerServices;
using LuminPack;
using LuminPack.Option;
using LuminPack.Utility;

namespace LuminPackBenchmark;

internal static class CharacterSaveDataJitProbe
{
    private const int Iterations = 100_000;

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void Run()
    {
        var data = CharacterSaveData.Create();
        var option = new LuminPackSerializerOption
        {
            StringEncoding = LuminPackStringEncoding.UTF16,
            StringRecording = LuminPackStringRecording.Length
        };
        var payload = LuminPackSerializer.Serialize(data, option);
        var writer = LuminBufferWriterPool.Rent();
        writer.Option.StringEncoding = option.StringEncoding;
        writer.Option.StringRecording = option.StringRecording;

        CharacterSaveData? result = null;
        try
        {
            for (var i = 0; i < Iterations; i++)
            {
                LuminPackSerializer.Serialize(data, writer);
                result = LuminPackSerializer.Deserialize<CharacterSaveData>(payload, option);
            }
        }
        finally
        {
            LuminBufferWriterPool.Return(writer);
        }

        Console.WriteLine($"Character JIT probe complete: {result?.CharacterId}");
    }
}
