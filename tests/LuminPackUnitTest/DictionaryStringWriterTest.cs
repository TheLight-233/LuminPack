using LuminPack;
using LuminPack.Core;
using LuminPack.Option;
using LuminPack.Utility;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Protects cached string headers in dense and sparse dictionary writers.</summary>
public class DictionaryStringWriterTest
{
    /// <summary>Compares collection output with the original element operations across every string recording mode.</summary>
    /// <param name="encoding">The payload encoding.</param>
    /// <param name="recording">The string framing mode.</param>
    [Theory]
    [InlineData(LuminPackStringEncoding.UTF8, LuminPackStringRecording.Length)]
    [InlineData(LuminPackStringEncoding.UTF16, LuminPackStringRecording.Length)]
    [InlineData(LuminPackStringEncoding.UTF8, LuminPackStringRecording.Token)]
    [InlineData(LuminPackStringEncoding.UTF16, LuminPackStringRecording.Token)]
    public void DenseSparseAndGrowingPayloadsKeepElementFraming(LuminPackStringEncoding encoding, LuminPackStringRecording recording)
    {
        var option = new LuminPackSerializerOption { StringEncoding = encoding, StringRecording = recording };
        foreach (bool sparse in new[] { false, true })
        {
            var keyStrings = new Dictionary<string, long>();
            var valueStrings = new Dictionary<int, string>();
            var bothStrings = new Dictionary<string, string>();
            for (int i = 0; i < 120; i++)
            {
                string key = i + "\u4e2d";
                string value = i == 70 ? new string('x', 40000) : i % 3 == 0 ? "" : "v" + i + "\u00e9";
                keyStrings.Add(key, -i);
                valueStrings.Add(i, value);
                bothStrings.Add(key, value);
            }
            if (sparse)
                for (int i = 1; i < 120; i += 3)
                {
                    keyStrings.Remove(i + "\u4e2d");
                    valueStrings.Remove(i);
                    bothStrings.Remove(i + "\u4e2d");
                }
            byte[] storage = new byte[200000];
            Span<byte> span = storage;
            var state = new LuminPackWriterOptionalState();
            state.Init(option);
            var writer = new LuminPackWriter(ref span, state);
            int index = 0;
            writer.WriteCollectionHeader(ref index, keyStrings.Count);
            writer.Advance(4);
            foreach (var pair in keyStrings)
            {
                WriteText(ref writer, pair.Key);
                index = writer.GetCurrentSpanIndex();
                writer.Advance(writer.WriteUnmanaged(ref index, pair.Value));
            }
            byte[] keyBytes = LuminPackSerializer.Serialize(keyStrings, option);
            Assert.Equal(storage.AsSpan(0, writer.GetCurrentSpanIndex()).ToArray(), keyBytes);
            Assert.Equal(keyStrings.ToArray(), LuminPackSerializer.Deserialize<Dictionary<string, long>>(keyBytes, option).ToArray());

            writer = new LuminPackWriter(ref span, state);
            index = 0;
            writer.WriteCollectionHeader(ref index, valueStrings.Count);
            writer.Advance(4);
            foreach (var pair in valueStrings)
            {
                index = writer.GetCurrentSpanIndex();
                writer.Advance(writer.WriteUnmanaged(ref index, pair.Key));
                WriteText(ref writer, pair.Value);
            }
            byte[] valueBytes = LuminPackSerializer.Serialize(valueStrings, option);
            Assert.Equal(storage.AsSpan(0, writer.GetCurrentSpanIndex()).ToArray(), valueBytes);
            Assert.Equal(valueStrings.ToArray(), LuminPackSerializer.Deserialize<Dictionary<int, string>>(valueBytes, option).ToArray());

            writer = new LuminPackWriter(ref span, state);
            index = 0;
            writer.WriteCollectionHeader(ref index, bothStrings.Count);
            writer.Advance(4);
            foreach (var pair in bothStrings)
            {
                WriteText(ref writer, pair.Key);
                WriteText(ref writer, pair.Value);
            }
            byte[] bothBytes = LuminPackSerializer.Serialize(bothStrings, option);
            Assert.Equal(storage.AsSpan(0, writer.GetCurrentSpanIndex()).ToArray(), bothBytes);
            Assert.Equal(bothStrings.ToArray(), LuminPackSerializer.Deserialize<Dictionary<string, string>>(bothBytes, option).ToArray());
        }
    }

    /// <summary>Writes one element through the original string core and recording-width lookup.</summary>
    /// <param name="writer">The caller-owned destination.</param>
    /// <param name="value">The element's text.</param>
    private static void WriteText(ref LuminPackWriter writer, string value)
        => writer.Advance(writer.WriteString(value) + writer.StringRecordLength());
}
