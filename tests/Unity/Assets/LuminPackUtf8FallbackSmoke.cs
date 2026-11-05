using System;
using System.Text;
using LuminPack.Core;
using LuminPack.Option;

/// <summary>Checks platform UTF-8 replacement through public JSON operations on Mono and IL2CPP.</summary>
public static unsafe class LuminPackUtf8FallbackSmoke
{
    /// <summary>Compares reused builders, character failures and property names with the host decoder.</summary>
    public static void Verify()
    {
        byte[][] cases =
        {
            Array.Empty<byte>(), Encoding.UTF8.GetBytes("\u00e9\u4e2d\ud83d\ude00"),
            Encoding.UTF8.GetBytes(new string('\u00e9', 4096)), Encoding.UTF8.GetBytes(new string('\u4e2d', 257)),
            new byte[] { 0x80 }, new byte[] { 0xc0, 0x80 }, new byte[] { 0xc2 },
            new byte[] { 0xed, 0xa0, 0x80 }, new byte[] { 0xf0, 0x9f }, new byte[] { 0xf4, 0x90, 0x80, 0x80 }
        };
        foreach (byte[] content in cases)
        {
            string expected = Encoding.UTF8.GetString(content);
            byte[] json = new byte[content.Length + 2];
            json[0] = json[json.Length - 1] = (byte)'"';
            content.CopyTo(json, 1);
            var state = new LuminPackReaderOptionalState();
            StringBuilder result = null;
            ReadBuilder(json, state, ref result);
            StringBuilder retained = result;
            ReadBuilder(json, state, ref result);
            if (!ReferenceEquals(retained, result) || result.ToString() != expected)
                throw new InvalidOperationException("Platform UTF-8 builder replacement or reuse changed.");

            Exception expectedError = null, actualError = null;
            char[] chars = new char[2];
            int count = 0;
            try { count = Encoding.UTF8.GetChars(content, chars); }
            catch (Exception error) { expectedError = error; }
            char value = default;
            try
            {
                fixed (byte* pointer = json)
                {
                    ReadOnlySpan<byte> bytes = json;
                    var reader = new LuminPackJsonReader(ref bytes, state);
                    reader.Read<LuminPackUtf8LengthOptions>(ref value);
                }
            }
            catch (Exception error) { actualError = error; }
            if (expectedError?.GetType() != actualError?.GetType() || expectedError?.Message != actualError?.Message ||
                actualError == null && value != (count == 0 ? '\0' : chars[0]))
                throw new InvalidOperationException("Platform UTF-8 character capacity behavior changed.");

            byte[] first = new byte[expected.Length * 6 + 32], second = new byte[first.Length];
            var writerState = new LuminPackWriterOptionalState();
            fixed (byte* a = first)
            fixed (byte* b = second)
            {
                Span<byte> left = first, right = second;
                var byteWriter = new LuminPackJsonWriter(ref left, writerState);
                var charWriter = new LuminPackJsonWriter(ref right, writerState);
                byteWriter.WritePropertyName<LuminPackUtf16LengthOptions>(content);
                charWriter.WritePropertyName<LuminPackUtf16LengthOptions>(expected.AsSpan());
                if (byteWriter.Position != charWriter.Position || !first.AsSpan().SequenceEqual(second))
                    throw new InvalidOperationException("Platform UTF-8 property name replacement changed.");
            }
        }
        VerifyTimeZones();
    }

    /// <summary>Checks non-ASCII and malformed snapshots on both sides of the bounded cache admission.</summary>
    private static void VerifyTimeZones()
    {
        foreach (int length in new[] { 8, 80, 128, 257 })
        {
            var zone = TimeZoneInfo.CreateCustomTimeZone("Utf8Fallback", TimeSpan.FromHours(2), new string('\u4e2d', length), "Standard");
            byte[] content = Encoding.UTF8.GetBytes(zone.ToSerializedString());
            content[Array.IndexOf(content, (byte)0xe4)] = 0xff;
            string snapshot = Encoding.UTF8.GetString(content);
            string expected = TimeZoneInfo.FromSerializedString(snapshot).ToSerializedString();
            byte[] json = new byte[content.Length + 2];
            json[0] = json[json.Length - 1] = (byte)'"';
            content.CopyTo(json, 1);
            var state = new LuminPackReaderOptionalState();
            TimeZoneInfo previous = null;
            for (int i = 0; i < 2; i++)
            {
                fixed (byte* pointer = json)
                {
                    ReadOnlySpan<byte> bytes = json;
                    var reader = new LuminPackJsonReader(ref bytes, state);
                    var result = reader.ReadTimeZoneInfo<LuminPackUtf8LengthOptions>();
                    if (result.ToSerializedString() != expected || i != 0 && snapshot.Length <= 256 && !ReferenceEquals(previous, result))
                        throw new InvalidOperationException("Platform UTF-8 time-zone snapshot behavior changed.");
                    previous = result;
                }
            }
        }
    }

    /// <summary>Reads a complete token while keeping its portable input cursor pinned.</summary>
    /// <param name="json">The quoted UTF-8 token.</param>
    /// <param name="state">The retained reader state.</param>
    /// <param name="value">The fresh or reused builder.</param>
    private static void ReadBuilder(byte[] json, LuminPackReaderOptionalState state, ref StringBuilder value)
    {
        fixed (byte* pointer = json)
        {
            ReadOnlySpan<byte> bytes = json;
            var reader = new LuminPackJsonReader(ref bytes, state);
            reader.ReadStringBuilder<LuminPackUtf8LengthOptions>(ref value);
        }
    }
}
