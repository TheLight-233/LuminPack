using System.Buffers;
using LuminPack.Utility;

namespace LuminPack.NinoBenchmark;

internal static class BenchmarkBuffers
{
    internal const int Capacity = 1 << 20;

    /// <summary>Creates an empty writer with the same initial room as Nino's benchmark writer.</summary>
    /// <returns>The writer owned by the benchmark.</returns>
    internal static LuminBufferWriter Create()
    {
        var writer = new LuminBufferWriter(useFirstBuffer: true);
        _ = ((IBufferWriter<byte>)writer).GetSpan(Capacity);
        return writer;
    }

    /// <summary>Captures a read fixture using its concrete retained-buffer serialization path.</summary>
    /// <typeparam name="T">The fixture type.</typeparam>
    /// <param name="value">The fixture to write.</param>
    /// <param name="write">The concrete serializer call.</param>
    /// <returns>The bytes to decode in the benchmark.</returns>
    internal static byte[] Serialize<T>(T value, Action<T, LuminBufferWriter> write)
    {
        using var writer = Create();
        write(value, writer);
        return writer.GetSpan().ToArray();
    }
}
