using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Unicode;
using LuminPack.Option;
using LuminPack.Utility;

/// <summary>Uses identical preallocated transcoding buffers in isolated runtime images.</summary>
public static class StringFixture
{
    private static string _text = "";
    private static byte[] _encoded = [];
    private static byte[] _utf16 = [];
    private static byte[] _destination = [];
    private static char[] _decoded = [];
    private static SerializeMode _mode;

    /// <summary>Creates deterministic input and validates every measured operation before timing.</summary>
    /// <param name="shape">The Unicode distribution.</param>
    /// <param name="length">The UTF-16 code-unit count.</param>
    public static void Setup(string shape, int length)
    {
        string pattern = shape switch
        {
            "ASCII" => "abcdefgh01234567",
            "Latin1" => "\u00E9\u00E0\u0080\u00FF\u00FC\u00DF\u00E7\u00F1",
            "MixedLatin1" => "\u00E9abcdef\u00FF",
            "AsciiLatin1" => "abcdef\u00E9\u00FF",
            "Latin2" => "\u03A9\u07FF\u0100\u06FF",
            "CJK" => "\u4E2D\u6587\u6D4B\u8BD5",
            "Emoji" => "\uD83D\uDE00\uD83D\uDE03",
            "Mixed" => "abc\u00E9\u03A9\u4E2D\uD83D\uDE00",
            _ => throw new ArgumentException("Unknown distribution.", nameof(shape))
        };
        _text = string.Concat(Enumerable.Repeat(pattern, (length + pattern.Length - 1) / pattern.Length))[..length];
        _encoded = Encoding.UTF8.GetBytes(_text);
        _utf16 = Encoding.Unicode.GetBytes(_text);
        _destination = new byte[length * 3 + 16];
        _decoded = new char[length + 16];
        _mode = SerializeMode.Utf8;
        foreach (Func<int> encode in new Func<int>[] { LegacyEncode, DefaultEncode, RuntimeModeEncode, BclEncode
#if PROFILE_API
            , ProfileEncode
#endif
        })
        {
            if (encode() != _encoded.Length || !_encoded.AsSpan().SequenceEqual(_destination.AsSpan(0, _encoded.Length)))
                throw new InvalidOperationException("UTF-8 bytes differ.");
        }
        foreach (Func<int> decode in new Func<int>[] { LegacyDecode, BclDecode
#if PROFILE_API
            , ProfileDecode
#endif
        })
        {
            if (decode() != _text.Length || !_text.AsSpan().SequenceEqual(_decoded.AsSpan(0, _text.Length)))
                throw new InvalidOperationException("Decoded UTF-8 differs.");
        }
        if (LegacyUtf16Encode() != _utf16.Length || !_utf16.AsSpan().SequenceEqual(_destination.AsSpan(0, _utf16.Length)))
            throw new InvalidOperationException("UTF-16 bytes differ.");
#if PROFILE_API
        if (ProfileUtf16Encode() != _utf16.Length || !_utf16.AsSpan().SequenceEqual(_destination.AsSpan(0, _utf16.Length)))
            throw new InvalidOperationException("Profile UTF-16 bytes differ.");
        if (ProfileUtf16Decode() != _text.Length || !_text.AsSpan().SequenceEqual(_decoded.AsSpan(0, _text.Length)))
            throw new InvalidOperationException("Profile UTF-16 decode differs.");
#endif
    }

    /// <summary>Encodes through the existing literal-mode entry.</summary>
    /// <returns>The number of encoded bytes.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int LegacyEncode() => (int)StringSerializer.Serialize(_text, _destination, out _, out int written, SerializeMode.Utf8, false) + written;

    /// <summary>Encodes through the existing default-mode entry.</summary>
    /// <returns>The number of encoded bytes.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int DefaultEncode() => (int)StringSerializer.Serialize(_text, _destination, out _, out int written, replaceInvalidSequences: false) + written;

    /// <summary>Encodes through a runtime-selected mode.</summary>
    /// <returns>The number of encoded bytes.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int RuntimeModeEncode() => (int)StringSerializer.Serialize(_text, _destination, out _, out int written, _mode, false) + written;

    /// <summary>Encodes through the same platform reference in every image.</summary>
    /// <returns>The number of encoded bytes.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int BclEncode() => (int)Utf8.FromUtf16(_text, _destination, out _, out int written, false) + written;

    /// <summary>Decodes through the existing default-mode entry.</summary>
    /// <returns>The number of decoded characters.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int LegacyDecode() => (int)StringSerializer.Deserialize(_encoded, _decoded, out _, out int written, replaceInvalidSequences: false) + written;

    /// <summary>Decodes through the same platform reference in every image.</summary>
    /// <returns>The number of decoded characters.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int BclDecode() => (int)Utf8.ToUtf16(_encoded, _decoded, out _, out int written, false) + written;

    /// <summary>Encodes through the existing UTF-16 literal-mode entry.</summary>
    /// <returns>The number of encoded bytes.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int LegacyUtf16Encode() => (int)StringSerializer.Serialize(_text, _destination, out _, out int written, SerializeMode.Utf16, false) + written;

#if PROFILE_API
    /// <summary>Encodes through a closed UTF-8 profile.</summary>
    /// <returns>The number of encoded bytes.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int ProfileEncode() => (int)StringSerializer.Serialize<LuminPackDefaultOptions>(_text, _destination, out _, out int written, false) + written;

    /// <summary>Decodes through a closed UTF-8 profile.</summary>
    /// <returns>The number of decoded characters.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int ProfileDecode() => (int)StringSerializer.Deserialize<LuminPackDefaultOptions>(_encoded, _decoded, out _, out int written, false) + written;

    /// <summary>Encodes through a closed UTF-16 profile.</summary>
    /// <returns>The number of encoded bytes.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int ProfileUtf16Encode() => (int)StringSerializer.Serialize<LuminPackUtf16LengthOptions>(_text, _destination, out _, out int written, false) + written;

    /// <summary>Decodes through a closed UTF-16 profile.</summary>
    /// <returns>The number of decoded characters.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int ProfileUtf16Decode() => (int)StringSerializer.Deserialize<LuminPackUtf16LengthOptions>(_utf16, _decoded, out _, out int written, false) + written;
#endif
}
