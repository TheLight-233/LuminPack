using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using LuminPack.Code;
using LuminPack.Option;
using LuminPack.Utility;

namespace LuminPack.Core;
public unsafe ref partial struct LuminPackJsonWriter
{
    /// <summary>Writes byte raw to the current output.</summary>
    /// <param name = "value">The value consumed or produced by this operation.</param>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void WriteByteRaw<TOptions>(byte value)
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
        if ((default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8))
        {
            EnsureCapacity(1);
            GetCurrentSpanReference() = value;
            Advance(1);
        }
        else
        {
            EnsureCapacity(2);
            ref byte dst = ref GetCurrentSpanReference();
            Unsafe.WriteUnaligned(ref dst, (char)value);
            Advance(2);
        }
    }

    /// <summary>Writes a generated native-word UTF-8 property prefix and its integer value.</summary>
    /// <param name = "first">The first used by this operation.</param>
    /// <param name = "last">The last used by this operation.</param>
    /// <param name = "byteCount">The number of bytes or elements to process.</param>
    /// <param name = "value">The value consumed or produced by this operation.</param>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public void WriteGeneratedInteger<TOptions>(ulong first, ulong last, int byteCount, int value)
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
        Span<byte> number = ReserveGeneratedIntegerPrefix<TOptions>(first, last, byteCount, 11);
        int written = LuminPackJsonNumberFormatter.WriteInt32(number, value);
        _currentIndex += byteCount + written;
    }

    /// <summary>Writes a generated native-word UTF-8 property prefix and its integer value.</summary>
    /// <param name = "first">The first used by this operation.</param>
    /// <param name = "last">The last used by this operation.</param>
    /// <param name = "byteCount">The number of bytes or elements to process.</param>
    /// <param name = "value">The value consumed or produced by this operation.</param>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public void WriteGeneratedInteger<TOptions>(ulong first, ulong last, int byteCount, uint value)
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
        Span<byte> number = ReserveGeneratedIntegerPrefix<TOptions>(first, last, byteCount, 10);
        int written = LuminPackJsonNumberFormatter.WriteUInt32(number, value);
        _currentIndex += byteCount + written;
    }

    /// <summary>Writes a generated native-word UTF-8 property prefix and its integer value.</summary>
    /// <param name = "first">The first used by this operation.</param>
    /// <param name = "last">The last used by this operation.</param>
    /// <param name = "byteCount">The number of bytes or elements to process.</param>
    /// <param name = "value">The value consumed or produced by this operation.</param>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public void WriteGeneratedInteger<TOptions>(ulong first, ulong last, int byteCount, long value)
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
        Span<byte> number = ReserveGeneratedIntegerPrefix<TOptions>(first, last, byteCount, 20);
        int written = LuminPackJsonNumberFormatter.WriteLong(number, value);
        _currentIndex += byteCount + written;
    }

    /// <summary>Writes a generated native-word UTF-8 property prefix and its integer value.</summary>
    /// <param name = "first">The first used by this operation.</param>
    /// <param name = "last">The last used by this operation.</param>
    /// <param name = "byteCount">The number of bytes or elements to process.</param>
    /// <param name = "value">The value consumed or produced by this operation.</param>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public void WriteGeneratedInteger<TOptions>(ulong first, ulong last, int byteCount, ulong value)
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
        Span<byte> number = ReserveGeneratedIntegerPrefix<TOptions>(first, last, byteCount, 20);
        int written = LuminPackJsonNumberFormatter.WriteULong(number, value);
        _currentIndex += byteCount + written;
    }

    /// <summary>Performs the reserve generated integer prefix step for the current operation.</summary>
    /// <param name = "first">The first used by this operation.</param>
    /// <param name = "last">The last used by this operation.</param>
    /// <param name = "byteCount">The number of bytes or elements to process.</param>
    /// <param name = "maxDigits">The max digits used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private Span<byte> ReserveGeneratedIntegerPrefix<TOptions>(ulong first, ulong last, int byteCount, int maxDigits)
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
        if (!(default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8))
            LuminPackExceptionHelper.ThrowInvalidOperationException("Generated integer prefixes require UTF-8");
        if ((uint)(byteCount - 8) > 8)
            LuminPackExceptionHelper.ThrowSpanOutOfRange(byteCount);
        if ((uint)_currentIndex > (uint)_bufferReference.Length)
            LuminPackExceptionHelper.ThrowSpanOutOfRange(_currentIndex);
        // Reserve before obtaining either reference: growth/streaming may
        // replace the span and reset the cursor. Advance only after formatting.
        EnsureCapacity(byteCount + maxDigits);
        ref byte destination = ref GetCurrentSpanReference();
        Unsafe.WriteUnaligned(ref destination, first);
        Unsafe.WriteUnaligned(ref Unsafe.Add(ref destination, byteCount - 8), last);
        _isFirstElement = false;
        return _bufferReference.Slice(_currentIndex + byteCount);
    }

    /// <summary>Writes comma if needed to the current output.</summary>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private void WriteCommaIfNeeded<TOptions>()
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
        if (!_isFirstElement)
        {
            WriteByteRaw<TOptions>(Comma);
        }

        _isFirstElement = false;
    }

    /// <summary>Writes object start to the current output.</summary>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void WriteObjectStart<TOptions>()
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
        WriteCommaIfNeeded<TOptions>();
        WriteByteRaw<TOptions>(OpenBrace);
        _depth++;
        _isFirstElement = true;
    }

    /// <summary>Writes object end to the current output.</summary>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void WriteObjectEnd<TOptions>()
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
        WriteByteRaw<TOptions>(CloseBrace);
        _depth--;
        _isFirstElement = false;
    }

    /// <summary>Writes array start to the current output.</summary>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void WriteArrayStart<TOptions>()
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
        WriteCommaIfNeeded<TOptions>();
        WriteByteRaw<TOptions>(OpenBracket);
        _depth++;
        _isFirstElement = true;
    }

    /// <summary>Writes array end to the current output.</summary>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void WriteArrayEnd<TOptions>()
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
        WriteByteRaw<TOptions>(CloseBracket);
        _depth--;
        _isFirstElement = false;
    }

    /// <summary>Writes property name to the current output.</summary>
    /// <param name = "utf8PropertyName">The utf8 property name used by this operation.</param>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void WritePropertyName<TOptions>(ReadOnlySpan<byte> utf8PropertyName)
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
        WriteCommaIfNeeded<TOptions>();
        WriteByteRaw<TOptions>(Quote);
        if ((default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8))
            WriteStringContentUtf8Raw<TOptions>(utf8PropertyName);
        else
            WriteStringContentUtf16<TOptions>(Encoding.UTF8.GetString(utf8PropertyName).AsSpan());
        WriteByteRaw<TOptions>(Quote);
        WriteByteRaw<TOptions>(Colon);
        _isFirstElement = true;
    }

    /// <summary>Writes property name to the current output.</summary>
    /// <param name = "utf16PropertyName">The utf16 property name used by this operation.</param>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void WritePropertyName<TOptions>(ReadOnlySpan<char> utf16PropertyName)
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
        WriteCommaIfNeeded<TOptions>();
        WriteByteRaw<TOptions>(Quote);
        if ((default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8))
        {
            WriteStringContentUtf8<TOptions>(utf16PropertyName);
        }
        else
        {
            WriteStringContentUtf16<TOptions>(utf16PropertyName);
        }

        WriteByteRaw<TOptions>(Quote);
        WriteByteRaw<TOptions>(Colon);
        _isFirstElement = true;
    }

    /// <summary>Writes property name to the current output.</summary>
    /// <param name = "propertyName">The property name used by this operation.</param>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void WritePropertyName<TOptions>(string propertyName)
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
        WriteCommaIfNeeded<TOptions>();
        WriteByteRaw<TOptions>(Quote);
        if ((default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8))
            WriteStringContentUtf8<TOptions>(propertyName.AsSpan());
        else
            WriteStringContentUtf16<TOptions>(propertyName.AsSpan());
        WriteByteRaw<TOptions>(Quote);
        WriteByteRaw<TOptions>(Colon);
        _isFirstElement = true;
    }

    /// <summary>
    /// Emits an already formatted, escape-free ASCII payload as a JSON string.
    /// The ISO-8601, GUID and TimeSpan formats used below never contain a
    /// character that requires JSON escaping, so the content writers only
    /// need to transcode or copy.
    /// </summary>
    /// <param name = "text">The text used by this operation.</param>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private void WriteFormattedValue<TOptions>(ReadOnlySpan<char> text)
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
        WriteCommaIfNeeded<TOptions>();
        WriteByteRaw<TOptions>(Quote);
        if ((default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8))
            WriteStringContentUtf8<TOptions>(text);
        else
            WriteStringContentUtf16<TOptions>(text);
        WriteByteRaw<TOptions>(Quote);
    }

    /// <summary>Writes a <see cref = "DateTime"/> in its round-trip ("O") form without allocating a string.</summary>
    /// <param name = "value">The value consumed or produced by this operation.</param>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void WriteDateTime<TOptions>(DateTime value)
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
        // "yyyy-MM-ddTHH:mm:ss.fffffff" is 27 characters.
        Span<char> buffer = stackalloc char[33];
        if (value.TryFormat(buffer, out int written, "O"))
        {
            WriteFormattedValue<TOptions>(buffer.Slice(0, written));
            return;
        }

        WriteString<TOptions>(value.ToString("O"));
    }

    /// <summary>Writes a <see cref = "DateTimeOffset"/> in its round-trip ("O") form without allocating a string.</summary>
    /// <param name = "value">The value consumed or produced by this operation.</param>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void WriteDateTimeOffset<TOptions>(DateTimeOffset value)
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
        // Round-trip text plus the "+hh:mm" offset is 33 characters.
        Span<char> buffer = stackalloc char[33];
        if (value.TryFormat(buffer, out int written, "O"))
        {
            WriteFormattedValue<TOptions>(buffer.Slice(0, written));
            return;
        }

        WriteString<TOptions>(value.ToString("O"));
    }

    /// <summary>Writes a <see cref = "Guid"/> in its "D" form without allocating a string.</summary>
    /// <param name = "value">The value consumed or produced by this operation.</param>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void WriteGuid<TOptions>(Guid value)
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
        Span<char> buffer = stackalloc char[36];
        if (value.TryFormat(buffer, out int written))
        {
            WriteFormattedValue<TOptions>(buffer.Slice(0, written));
            return;
        }

        WriteString<TOptions>(value.ToString());
    }

    /// <summary>Writes a <see cref = "TimeSpan"/> in its constant ("c") form without allocating a string.</summary>
    /// <param name = "value">The value consumed or produced by this operation.</param>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void WriteTimeSpan<TOptions>(TimeSpan value)
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
        // "-10675199.02:48:05.4775808" is 26 characters.
        Span<char> buffer = stackalloc char[32];
        if (value.TryFormat(buffer, out int written, "c"))
        {
            WriteFormattedValue<TOptions>(buffer.Slice(0, written));
            return;
        }

        WriteString<TOptions>(value.ToString());
    }

    /// <summary>Writes string to the current output.</summary>
    /// <param name = "value">The value consumed or produced by this operation.</param>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void WriteString<TOptions>(string? value)
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
        WriteCommaIfNeeded<TOptions>();
        if (value == null)
        {
            WriteNullInternal<TOptions>();
            return;
        }

#if NET8_0_OR_GREATER
        // Reserve the quotes and content together. The transcoder only writes
        // after validating the complete value, so misses retain the normal path.
        if ((default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8) && (uint)(value.Length - 4) <= 28 && global::System.Runtime.Intrinsics.Vector128.IsHardwareAccelerated)
        {
            EnsureCapacity(value.Length + 2);
            if (global::LuminPack.Internal.ShortAsciiTranscoder.TryEncode(value.AsSpan(), _bufferReference.Slice(_currentIndex + 1, value.Length), escapeJson: true))
            {
                ref byte output = ref GetCurrentSpanReference();
                output = Quote;
                Unsafe.Add(ref output, value.Length + 1) = Quote;
                Advance(value.Length + 2);
                return;
            }

            WriteByteRaw<TOptions>(Quote);
            WriteStringContentUtf8Encoded<TOptions>(value.AsSpan());
            WriteByteRaw<TOptions>(Quote);
            return;
        }

#endif
        WriteByteRaw<TOptions>(Quote);
        if ((default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8))
        {
            WriteStringContentUtf8<TOptions>(value.AsSpan());
        }
        else
        {
            WriteStringContentUtf16<TOptions>(value.AsSpan());
        }

        WriteByteRaw<TOptions>(Quote);
    }

    /// <summary>Writes string content utf8 to the current output.</summary>
    /// <param name = "value">The value consumed or produced by this operation.</param>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private void WriteStringContentUtf8<TOptions>(ReadOnlySpan<char> value)
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
#if NET8_0_OR_GREATER
        // Tiny ASCII values are cheaper to scan and narrow without a transcoder call.
        if (value.Length <= 32)
        {
            EnsureCapacity(value.Length);
            if (global::LuminPack.Internal.ShortAsciiTranscoder.TryEncode(value, _bufferReference.Slice(_currentIndex), escapeJson: true))
            {
                Advance(value.Length);
                return;
            }

            ref byte destination = ref GetCurrentSpanReference();
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (c > 0x7F || NeedsJsonEscape(c))
                {
                    if (c <= 0x7F)
                        WriteStringContentSlow<TOptions>(value);
                    else
                        WriteStringContentUtf8Encoded<TOptions>(value);
                    return;
                }

                Unsafe.Add(ref destination, i) = (byte)c;
            }

            Advance(value.Length);
            return;
        }

        WriteStringContentUtf8Encoded<TOptions>(value);
#else
            bool needsEscape = false;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (c > 0x7F || c < 0x20 || c == '"' || c == '\\')
                {
                    needsEscape = true;
                    break;
                }
            }

            if (!needsEscape)
            {
                EnsureCapacity(value.Length);
                ref byte dst = ref GetCurrentSpanReference();
                for (int i = 0; i < value.Length; i++)
                {
                    Unsafe.Add(ref dst, i) = (byte)value[i];
                }
                Advance(value.Length);
            }
            else
            {
                WriteStringContentSlow<TOptions>(value);
            }
#endif
    }

#if (NET8_0_OR_GREATER)
    /// <summary>Writes string content utf8 encoded to the current output.</summary>
    /// <param name = "value">The value consumed or produced by this operation.</param>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private void WriteStringContentUtf8Encoded<TOptions>(ReadOnlySpan<char> value)
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
        int escapeIndex = value.IndexOfAny(s_stringEscapeChars);
        if (escapeIndex < 0)
            WriteUtf8StringSegment(value);
        else
            WriteStringContentUtf8Escaped<TOptions>(value, escapeIndex);
    }

#endif
#if (NET8_0_OR_GREATER)
    /// <summary>Writes string content utf8 escaped to the current output.</summary>
    /// <param name = "value">The value consumed or produced by this operation.</param>
    /// <param name = "escapeIndex">The escape index used by this operation.</param>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private void WriteStringContentUtf8Escaped<TOptions>(ReadOnlySpan<char> value, int escapeIndex)
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
        while (true)
        {
            if (escapeIndex <= 8 && value.Length - escapeIndex >= 3 && NeedsJsonEscape(value[escapeIndex + 1]) && NeedsJsonEscape(value[escapeIndex + 2]))
            {
                // Keep dense escape clusters on the existing scalar path, in bounded
                // blocks so a dense prefix does not slow a long ordinary suffix.
                int blockLength = Math.Min(64, value.Length);
                if (blockLength < value.Length && char.IsHighSurrogate(value[blockLength - 1]) && char.IsLowSurrogate(value[blockLength]))
                    blockLength++;
                WriteStringContentSlow<TOptions>(value.Slice(0, blockLength));
                value = value.Slice(blockLength);
                escapeIndex = value.IndexOfAny(s_stringEscapeChars);
                if (escapeIndex < 0)
                {
                    WriteUtf8StringSegment(value);
                    return;
                }

                continue;
            }

            if (escapeIndex > 0)
                WriteUtf8StringSegment(value.Slice(0, escapeIndex));
            // Consecutive escapes stay scalar; do not search or transcode empty runs.
            do
            {
                WriteEscapedUtf8Char(value[escapeIndex++]);
            }
            while (escapeIndex < value.Length && NeedsJsonEscape(value[escapeIndex]));
            value = value.Slice(escapeIndex);
            escapeIndex = value.IndexOfAny(s_stringEscapeChars);
            if (escapeIndex < 0)
            {
                WriteUtf8StringSegment(value);
                return;
            }
        }
    }

#endif
    /// <summary>Writes string content utf8 raw to the current output.</summary>
    /// <param name = "value">The value consumed or produced by this operation.</param>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private void WriteStringContentUtf8Raw<TOptions>(ReadOnlySpan<byte> value)
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
#if NET8_0_OR_GREATER
        int escapeIndex = value.IndexOfAny(s_stringEscapeBytes);
        if (escapeIndex < 0)
        {
            WriteRaw(value);
            return;
        }

        while (true)
        {
            if (escapeIndex > 0)
                WriteRaw(value.Slice(0, escapeIndex));
            do
            {
                WriteEscapedUtf8Char((char)value[escapeIndex++]);
            }
            while (escapeIndex < value.Length && NeedsJsonEscape((char)value[escapeIndex]));
            value = value.Slice(escapeIndex);
            escapeIndex = value.IndexOfAny(s_stringEscapeBytes);
            if (escapeIndex < 0)
            {
                WriteRaw(value);
                return;
            }
        }
#else
            bool needsEscape = false;
            for (int i = 0; i < value.Length; i++)
            {
                byte b = value[i];
                if (b < 0x20 || b == '"' || b == '\\')
                {
                    needsEscape = true;
                    break;
                }
            }

            if (!needsEscape)
            {
                EnsureCapacity(value.Length);
                ref byte dst = ref GetCurrentSpanReference();
                Unsafe.CopyBlockUnaligned(ref dst, ref MemoryMarshal.GetReference(value), (uint)value.Length);
                Advance(value.Length);
            }
            else
            {
                WriteStringContentUtf8Slow<TOptions>(value);
            }
#endif
    }

    /// <summary>Writes string content utf8 slow to the current output.</summary>
    /// <param name = "value">The value consumed or produced by this operation.</param>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private void WriteStringContentUtf8Slow<TOptions>(ReadOnlySpan<byte> value)
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
        for (int i = 0; i < value.Length; i++)
        {
            byte b = value[i];
            if (b == '"')
            {
                WriteByteRaw<TOptions>(Backslash);
                WriteByteRaw<TOptions>((byte)'"');
            }
            else if (b == '\\')
            {
                WriteByteRaw<TOptions>(Backslash);
                WriteByteRaw<TOptions>((byte)'\\');
            }
            else if (b == '\n')
            {
                WriteByteRaw<TOptions>(Backslash);
                WriteByteRaw<TOptions>((byte)'n');
            }
            else if (b == '\r')
            {
                WriteByteRaw<TOptions>(Backslash);
                WriteByteRaw<TOptions>((byte)'r');
            }
            else if (b == '\t')
            {
                WriteByteRaw<TOptions>(Backslash);
                WriteByteRaw<TOptions>((byte)'t');
            }
            else if (b < 0x20)
            {
                WriteByteRaw<TOptions>(Backslash);
                WriteByteRaw<TOptions>((byte)'u');
                WriteByteRaw<TOptions>((byte)'0');
                WriteByteRaw<TOptions>((byte)'0');
                WriteByteRaw<TOptions>(HexChar((b >> 4) & 0xF));
                WriteByteRaw<TOptions>(HexChar(b & 0xF));
            }
            else
            {
                WriteByteRaw<TOptions>(b);
            }
        }
    }

    /// <summary>Writes string content utf16 to the current output.</summary>
    /// <param name = "value">The value consumed or produced by this operation.</param>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private void WriteStringContentUtf16<TOptions>(ReadOnlySpan<char> value)
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
        bool needsEscape = false;
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            if (c < 0x20 || c == '"' || c == '\\')
            {
                needsEscape = true;
                break;
            }
        }

        if (!needsEscape)
        {
            if (_output is not null)
            {
                WriteRaw(value);
                return;
            }

            int byteCount = checked(value.Length * 2);
            EnsureCapacity(byteCount);
            ref byte src = ref Unsafe.As<char, byte>(ref MemoryMarshal.GetReference(value));
            ref byte dst = ref GetCurrentSpanReference();
            Unsafe.CopyBlock(ref dst, ref src, (uint)byteCount);
            Advance(byteCount);
        }
        else
        {
            WriteStringContentUtf16Slow<TOptions>(value);
        }
    }

    /// <summary>Writes string content utf16 slow to the current output.</summary>
    /// <param name = "value">The value consumed or produced by this operation.</param>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private void WriteStringContentUtf16Slow<TOptions>(ReadOnlySpan<char> value)
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
        int lastWritten = 0;
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            bool needsEscape = false;
            if (c == '"' || c == '\\' || c == '\n' || c == '\r' || c == '\t' || c < 0x20)
            {
                needsEscape = true;
            }

            if (needsEscape)
            {
                if (i > lastWritten)
                {
                    var segment = value.Slice(lastWritten, i - lastWritten);
                    if (_output is not null)
                    {
                        WriteRaw(segment);
                    }
                    else
                    {
                        int byteCount = checked(segment.Length * 2);
                        EnsureCapacity(byteCount);
                        ref byte src = ref Unsafe.As<char, byte>(ref MemoryMarshal.GetReference(segment));
                        ref byte dst = ref GetCurrentSpanReference();
                        Unsafe.CopyBlock(ref dst, ref src, (uint)byteCount);
                        Advance(byteCount);
                    }
                }

                if (c == '"')
                {
                    WriteByteRaw<TOptions>(Backslash);
                    WriteUtf16CharDirect('"');
                }
                else if (c == '\\')
                {
                    WriteByteRaw<TOptions>(Backslash);
                    WriteUtf16CharDirect('\\');
                }
                else if (c == '\n')
                {
                    WriteByteRaw<TOptions>(Backslash);
                    WriteUtf16CharDirect('n');
                }
                else if (c == '\r')
                {
                    WriteByteRaw<TOptions>(Backslash);
                    WriteUtf16CharDirect('r');
                }
                else if (c == '\t')
                {
                    WriteByteRaw<TOptions>(Backslash);
                    WriteUtf16CharDirect('t');
                }
                else if (c < 0x20)
                {
                    WriteByteRaw<TOptions>(Backslash);
                    WriteUtf16CharDirect('u');
                    WriteUtf16CharDirect(HexCharUtf16((c >> 12) & 0xF));
                    WriteUtf16CharDirect(HexCharUtf16((c >> 8) & 0xF));
                    WriteUtf16CharDirect(HexCharUtf16((c >> 4) & 0xF));
                    WriteUtf16CharDirect(HexCharUtf16(c & 0xF));
                }

                lastWritten = i + 1;
            }
        }

        if (lastWritten < value.Length)
        {
            var segment = value.Slice(lastWritten);
            if (_output is not null)
            {
                WriteRaw(segment);
                return;
            }

            int byteCount = checked(segment.Length * 2);
            EnsureCapacity(byteCount);
            ref byte src = ref Unsafe.As<char, byte>(ref MemoryMarshal.GetReference(segment));
            ref byte dst = ref GetCurrentSpanReference();
            Unsafe.CopyBlock(ref dst, ref src, (uint)byteCount);
            Advance(byteCount);
        }
    }

    /// <summary>Writes string content slow to the current output.</summary>
    /// <param name = "value">The value consumed or produced by this operation.</param>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private void WriteStringContentSlow<TOptions>(ReadOnlySpan<char> value)
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            if (NeedsJsonEscape(c))
            {
                WriteEscapedUtf8Char(c);
            }
            else if (c <= 0x7F)
            {
                WriteByteRaw<TOptions>((byte)c);
            }
            else
            {
                uint scalar = c;
                if (char.IsSurrogate(c))
                {
                    if (char.IsHighSurrogate(c) && i + 1 < value.Length && char.IsLowSurrogate(value[i + 1]))
                        scalar = 0x10000u + (((uint)c - 0xD800u) << 10) + (value[++i] - 0xDC00u);
                    else
                        scalar = 0xFFFD;
                }

                int length = scalar <= 0x7FF ? 2 : scalar <= 0xFFFF ? 3 : 4;
                EnsureCapacity(length);
                ref byte destination = ref GetCurrentSpanReference();
                if (length == 2)
                {
                    destination = (byte)(0xC0 | (scalar >> 6));
                    Unsafe.Add(ref destination, 1) = (byte)(0x80 | (scalar & 0x3F));
                }
                else if (length == 3)
                {
                    destination = (byte)(0xE0 | (scalar >> 12));
                    Unsafe.Add(ref destination, 1) = (byte)(0x80 | ((scalar >> 6) & 0x3F));
                    Unsafe.Add(ref destination, 2) = (byte)(0x80 | (scalar & 0x3F));
                }
                else
                {
                    destination = (byte)(0xF0 | (scalar >> 18));
                    Unsafe.Add(ref destination, 1) = (byte)(0x80 | ((scalar >> 12) & 0x3F));
                    Unsafe.Add(ref destination, 2) = (byte)(0x80 | ((scalar >> 6) & 0x3F));
                    Unsafe.Add(ref destination, 3) = (byte)(0x80 | (scalar & 0x3F));
                }

                Advance(length);
            }
        }
    }

    /// <summary>Writes float to the current output.</summary>
    /// <param name = "value">The value consumed or produced by this operation.</param>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void WriteFloat<TOptions>(float value)
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
        WriteCommaIfNeeded<TOptions>();
        EnsureCapacity((default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8) ? 24 : 64);
        if (float.IsNaN(value) || float.IsInfinity(value))
            LuminPackExceptionHelper.ThrowFormatException("NaN and Infinity are not valid JSON numbers");
        if ((default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8))
        {
            _currentIndex += LuminPackJsonFloatFormatter.WriteFloatUtf8(_bufferReference.Slice(_currentIndex), value);
        }
        else
        {
            _currentIndex += LuminPackJsonFloatFormatter.WriteFloatUtf16(MemoryMarshal.Cast<byte, char>(_bufferReference.Slice(_currentIndex)), value) * 2;
        }
    }

    /// <summary>Writes double to the current output.</summary>
    /// <param name = "value">The value consumed or produced by this operation.</param>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void WriteDouble<TOptions>(double value)
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
        WriteCommaIfNeeded<TOptions>();
        EnsureCapacity((default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8) ? 32 : 64);
        if (double.IsNaN(value) || double.IsInfinity(value))
            LuminPackExceptionHelper.ThrowFormatException("NaN and Infinity are not valid JSON numbers");
        if ((default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8))
        {
            _currentIndex += LuminPackJsonFloatFormatter.WriteDoubleUtf8(_bufferReference.Slice(_currentIndex), value);
        }
        else
        {
            _currentIndex += LuminPackJsonFloatFormatter.WriteDoubleUtf16(MemoryMarshal.Cast<byte, char>(_bufferReference.Slice(_currentIndex)), value) * 2;
        }
    }

    /// <summary>Writes int to the current output.</summary>
    /// <param name = "value">The value consumed or produced by this operation.</param>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void WriteInt<TOptions>(int value)
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
        WriteCommaIfNeeded<TOptions>();
        if ((default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8))
        {
            EnsureCapacity(11);
            _currentIndex += LuminPackJsonNumberFormatter.WriteInt32(_bufferReference.Slice(_currentIndex), value);
        }
        else
        {
            EnsureCapacity(22);
            var charSpan = MemoryMarshal.Cast<byte, char>(_bufferReference.Slice(_currentIndex, 22));
            value.TryFormat(charSpan, out int written);
            _currentIndex += written << 1;
        }
    }

    /// <summary>Writes u int to the current output.</summary>
    /// <param name = "value">The value consumed or produced by this operation.</param>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void WriteUInt<TOptions>(uint value)
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
        WriteCommaIfNeeded<TOptions>();
        if ((default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8))
        {
            EnsureCapacity(10);
            _currentIndex += LuminPackJsonNumberFormatter.WriteUInt32(_bufferReference.Slice(_currentIndex), value);
        }
        else
        {
            EnsureCapacity(20);
            var charSpan = MemoryMarshal.Cast<byte, char>(_bufferReference.Slice(_currentIndex, 20));
            value.TryFormat(charSpan, out int written);
            _currentIndex += written << 1;
        }
    }

    /// <summary>Writes byte to the current output.</summary>
    /// <param name = "value">The value consumed or produced by this operation.</param>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void WriteByte<TOptions>(byte value)
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
        WriteCommaIfNeeded<TOptions>();
        if ((default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8))
        {
            EnsureCapacity(4); // packed store intentionally writes 4 bytes
            _currentIndex += LuminPackJsonNumberFormatter.WriteByte(_bufferReference.Slice(_currentIndex), value);
        }
        else
        {
            EnsureCapacity(6);
            var charSpan = MemoryMarshal.Cast<byte, char>(_bufferReference.Slice(_currentIndex, 6));
            value.TryFormat(charSpan, out int written);
            _currentIndex += written << 1;
        }
    }

    /// <summary>Writes s byte to the current output.</summary>
    /// <param name = "value">The value consumed or produced by this operation.</param>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void WriteSByte<TOptions>(sbyte value)
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
        WriteCommaIfNeeded<TOptions>();
        if ((default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8))
        {
            EnsureCapacity(5); // sign + packed 4-byte store
            _currentIndex += LuminPackJsonNumberFormatter.WriteSByte(_bufferReference.Slice(_currentIndex), value);
        }
        else
        {
            EnsureCapacity(8);
            var charSpan = MemoryMarshal.Cast<byte, char>(_bufferReference.Slice(_currentIndex, 8));
            value.TryFormat(charSpan, out int written);
            _currentIndex += written << 1;
        }
    }

    /// <summary>Writes short to the current output.</summary>
    /// <param name = "value">The value consumed or produced by this operation.</param>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void WriteShort<TOptions>(short value)
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
        WriteCommaIfNeeded<TOptions>();
        if ((default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8))
        {
            EnsureCapacity(6);
            _currentIndex += LuminPackJsonNumberFormatter.WriteShort(_bufferReference.Slice(_currentIndex), value);
        }
        else
        {
            EnsureCapacity(12);
            var charSpan = MemoryMarshal.Cast<byte, char>(_bufferReference.Slice(_currentIndex, 12));
            value.TryFormat(charSpan, out int written);
            _currentIndex += written << 1;
        }
    }

    /// <summary>Writes u short to the current output.</summary>
    /// <param name = "value">The value consumed or produced by this operation.</param>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void WriteUShort<TOptions>(ushort value)
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
        WriteCommaIfNeeded<TOptions>();
        if ((default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8))
        {
            EnsureCapacity(5);
            _currentIndex += LuminPackJsonNumberFormatter.WriteUShort(_bufferReference.Slice(_currentIndex), value);
        }
        else
        {
            EnsureCapacity(10);
            var charSpan = MemoryMarshal.Cast<byte, char>(_bufferReference.Slice(_currentIndex, 10));
            value.TryFormat(charSpan, out int written);
            _currentIndex += written << 1;
        }
    }

    /// <summary>Writes long to the current output.</summary>
    /// <param name = "value">The value consumed or produced by this operation.</param>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void WriteLong<TOptions>(long value)
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
        WriteCommaIfNeeded<TOptions>();
        if ((default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8))
        {
            EnsureCapacity(20);
            _currentIndex += LuminPackJsonNumberFormatter.WriteLong(_bufferReference.Slice(_currentIndex), value);
        }
        else
        {
            EnsureCapacity(40);
            var charSpan = MemoryMarshal.Cast<byte, char>(_bufferReference.Slice(_currentIndex, 40));
            value.TryFormat(charSpan, out int written);
            _currentIndex += written << 1;
        }
    }

    /// <summary>Writes u long to the current output.</summary>
    /// <param name = "value">The value consumed or produced by this operation.</param>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void WriteULong<TOptions>(ulong value)
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
        WriteCommaIfNeeded<TOptions>();
        if ((default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8))
        {
            EnsureCapacity(20);
            _currentIndex += LuminPackJsonNumberFormatter.WriteULong(_bufferReference.Slice(_currentIndex), value);
        }
        else
        {
            EnsureCapacity(40);
            var charSpan = MemoryMarshal.Cast<byte, char>(_bufferReference.Slice(_currentIndex, 40));
            value.TryFormat(charSpan, out int written);
            _currentIndex += written << 1;
        }
    }

    /// <summary>Writes decimal to the current output.</summary>
    /// <param name = "value">The value consumed or produced by this operation.</param>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void WriteDecimal<TOptions>(decimal value)
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
        WriteCommaIfNeeded<TOptions>();
        if ((default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8))
        {
            EnsureCapacity(31);
            _currentIndex += LuminPackJsonNumberFormatter.WriteDecimal(_bufferReference.Slice(_currentIndex), value);
        }
        else
        {
            EnsureCapacity(62);
            var charSpan = MemoryMarshal.Cast<byte, char>(_bufferReference.Slice(_currentIndex, 62));
            value.TryFormat(charSpan, out int written, default, System.Globalization.NumberFormatInfo.InvariantInfo);
            _currentIndex += written << 1;
        }
    }

    /// <summary>Writes char to the current output.</summary>
    /// <param name = "value">The value consumed or produced by this operation.</param>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void WriteChar<TOptions>(char value)
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
        WriteCommaIfNeeded<TOptions>();
        WriteByteRaw<TOptions>(Quote);
        if (value == '"' || value == '\\')
        {
            WriteByteRaw<TOptions>(Backslash);
            WriteByteRaw<TOptions>((byte)value);
        }
        else if (value == '\b' || value == '\f' || value == '\n' || value == '\r' || value == '\t')
        {
            WriteByteRaw<TOptions>(Backslash);
            WriteByteRaw<TOptions>(value switch
            {
                '\b' => (byte)'b',
                '\f' => (byte)'f',
                '\n' => (byte)'n',
                '\r' => (byte)'r',
                _ => (byte)'t'
            });
        }
        else if (value < 0x20 || char.IsSurrogate(value))
        {
            WriteByteRaw<TOptions>(Backslash);
            WriteByteRaw<TOptions>((byte)'u');
            WriteByteRaw<TOptions>(HexChar((value >> 12) & 0xF));
            WriteByteRaw<TOptions>(HexChar((value >> 8) & 0xF));
            WriteByteRaw<TOptions>(HexChar((value >> 4) & 0xF));
            WriteByteRaw<TOptions>(HexChar(value & 0xF));
        }
        else if ((default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8))
        {
            if (value <= 0x7F)
            {
                WriteByteRaw<TOptions>((byte)value);
            }
            else
            {
                Span<byte> utf8Buf = stackalloc byte[4];
                Span<char> charBuf = stackalloc char[1]
                {
                    value
                };
                int len = Encoding.UTF8.GetBytes(charBuf, utf8Buf);
                WriteRaw(utf8Buf[..len]);
            }
        }
        else
        {
            WriteUtf16CharDirect(value);
        }

        WriteByteRaw<TOptions>(Quote);
    }

    /// <summary>Writes bool to the current output.</summary>
    /// <param name = "value">The value consumed or produced by this operation.</param>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void WriteBool<TOptions>(bool value)
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
        WriteCommaIfNeeded<TOptions>();
        if ((default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8))
        {
            if (value)
            {
                EnsureCapacity(4);
                ref byte dst = ref GetCurrentSpanReference();
                Unsafe.WriteUnaligned(ref dst, 0x65757274u); // "true"
                _currentIndex += 4;
            }
            else
            {
                EnsureCapacity(5);
                ref byte dst = ref GetCurrentSpanReference();
                Unsafe.WriteUnaligned(ref dst, 0x736C6166u); // "fals"
                Unsafe.Add(ref dst, 4) = (byte)'e';
                _currentIndex += 5;
            }

            return;
        }

        // UTF-16 path was not benchmarked.
        if (value)
        {
            WriteByteRaw<TOptions>((byte)'t');
            WriteByteRaw<TOptions>((byte)'r');
            WriteByteRaw<TOptions>((byte)'u');
            WriteByteRaw<TOptions>((byte)'e');
        }
        else
        {
            WriteByteRaw<TOptions>((byte)'f');
            WriteByteRaw<TOptions>((byte)'a');
            WriteByteRaw<TOptions>((byte)'l');
            WriteByteRaw<TOptions>((byte)'s');
            WriteByteRaw<TOptions>((byte)'e');
        }
    }

    /// <summary>Writes null to the current output.</summary>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void WriteNull<TOptions>()
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
        WriteCommaIfNeeded<TOptions>();
        WriteNullInternal<TOptions>();
    }

    /// <summary>Writes null internal to the current output.</summary>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private void WriteNullInternal<TOptions>()
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
        if ((default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8))
        {
            EnsureCapacity(4);
            ref byte dst = ref GetCurrentSpanReference();
            Unsafe.WriteUnaligned(ref dst, 0x6C6C756Eu); // "null"
            _currentIndex += 4;
            return;
        }

        WriteByteRaw<TOptions>((byte)'n');
        WriteByteRaw<TOptions>((byte)'u');
        WriteByteRaw<TOptions>((byte)'l');
        WriteByteRaw<TOptions>((byte)'l');
    }
}
