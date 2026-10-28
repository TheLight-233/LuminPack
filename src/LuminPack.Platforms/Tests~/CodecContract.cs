using LuminPack.Code;
using LuminPack.Core;

namespace LuminPack.Platforms.Tests;

/// <summary>Checks explicit external value records without rebuilding Core or running unrelated unit suites.</summary>
public static class CodecContract
{
    /// <summary>Counts verified sample values.</summary>
    public static int Samples { get; private set; }

    /// <summary>Checks semantic/byte round trips, exact sizes, every shortened window, trailing data and write canaries.</summary>
    /// <typeparam name="T">The platform value.</typeparam>
    /// <param name="codec">The module's complete codec.</param><param name="value">The sample.</param><param name="name">The assertion context.</param>
    public static void Check<T>(LuminPackCodec<T> codec, T value, string name)
    {
        byte[] bytes = codec.Serialize(value);
        Assert(bytes.Length == codec.GetSize(value), name + ": size mismatch.");
        T restored = codec.Deserialize(bytes);
        Assert(EqualityComparer<T>.Default.Equals(value, restored), name + ": changed public value.");
        Assert(bytes.AsSpan().SequenceEqual(codec.Serialize(restored)), name + ": changed component bits or identity.");
        nint key = LuminPackMarshal.GetMethodTable(typeof(T));
        Assert(LuminPackFormatterRegistry.TryGetValue(in key, out var entry) && entry.WriteValue != 0 && entry.ReadValue != 0 && entry.CalculateOffset != 0,
            name + ": missing explicit registration.");
        nint wrappedKey = LuminPackMarshal.GetMethodTable(typeof(LuminPackValue<T>));
        Assert(LuminPackFormatterRegistry.TryGetValue(in wrappedKey, out _), name + ": missing Custom wrapper registration.");
        for (int length = 0; length < bytes.Length; length++)
        {
            ReadOnlySpan<byte> shortInput = bytes.AsSpan(0, length);
            var reader = new LuminPackReader(ref shortInput);
            T sentinel = value;
            bool rejected = false;
            try { codec.Read(ref reader, ref sentinel); } catch { rejected = true; }
            Assert(rejected && reader.Position == 0 && bytes.AsSpan().SequenceEqual(codec.Serialize(sentinel)), name + ": partial input published state.");
            byte[] guarded = Enumerable.Repeat((byte)0xA5, bytes.Length + 8).ToArray();
            Span<byte> shortOutput = guarded.AsSpan(3, length);
            var writer = new LuminPackWriter(ref shortOutput);
            rejected = false;
            try { codec.Write(ref writer, value); } catch { rejected = true; }
            Assert(rejected && writer.Position == 0 && guarded.All(b => b == 0xA5), name + ": short output changed storage.");
        }
        byte[] trailing = bytes.Concat(new byte[] { 0xA5 }).ToArray();
        T retained = value;
        bool trailingRejected = false;
        try { codec.Deserialize(trailing, ref retained); } catch { trailingRejected = true; }
        Assert(trailingRejected && bytes.AsSpan().SequenceEqual(codec.Serialize(retained)), name + ": trailing input published state.");
        byte[] fullGuard = Enumerable.Repeat((byte)0xA5, bytes.Length + 8).ToArray();
        Span<byte> output = fullGuard.AsSpan(3, bytes.Length);
        var completeWriter = new LuminPackWriter(ref output);
        codec.Write(ref completeWriter, value);
        Assert(fullGuard.Take(3).Concat(fullGuard.Skip(bytes.Length + 3)).All(b => b == 0xA5), name + ": write escaped its range.");
        Assert(completeWriter.Position == bytes.Length && fullGuard.AsSpan(3, bytes.Length).SequenceEqual(bytes), name + ": cursor or record mismatch.");
        Samples++;
    }

    /// <summary>Rejects an invalid empty/null marker without destination or cursor publication.</summary>
    /// <typeparam name="T">The platform value.</typeparam>
    /// <param name="codec">The codec with a leading marker.</param><param name="value">A valid sample.</param><param name="name">The assertion context.</param>
    public static void RejectMarker<T>(LuminPackCodec<T> codec, T value, string name)
    {
        byte[] invalid = codec.Serialize(value);
        invalid[0] = 0xFE;
        ReadOnlySpan<byte> input = invalid;
        var reader = new LuminPackReader(ref input);
        bool rejected = false;
        try { codec.Read(ref reader, ref value); } catch { rejected = true; }
        Assert(rejected && reader.Position == 0, name + ": accepted an invalid marker.");
    }

    /// <summary>Raises a test failure through the shared runtime diagnostic helper.</summary>
    /// <param name="condition">The required condition.</param><param name="message">The failure context.</param>
    public static void Assert(bool condition, string message)
    {
        if (!condition) LuminPackExceptionHelper.ThrowInvalidOperationException(message);
    }
}
