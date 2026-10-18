using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
#if NET8_0_OR_GREATER
using System.Runtime.Intrinsics;
using System.Text.Unicode;
#endif
using LuminPack.Option;
using LuminPack.Code;
using LuminPack.Internal;
using LuminPack.Utility;
using static LuminPack.Code.LuminPackMarshal;

namespace LuminPack.Core;
public unsafe ref partial struct LuminPackReader
{
    /// <summary>Performs the peek is null string step for the current operation.</summary>
    /// <param name = "index">The position at which the operation starts.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public bool PeekIsNullString<TOptions>(ref int index)
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
        return (default(TOptions).StringRecording == global::LuminPack.Option.LuminPackStringRecording.Token)
#if NET8_0_OR_GREATER
        ? Unsafe.Add(ref _bufferStart, (nint)(uint)index) == 0 : Unsafe.ReadUnaligned<int>(ref Unsafe.Add(ref _bufferStart, (nint)(uint)index)) == LuminPackCode.NullCollection;
#else
                ? Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index) == 0
                : Unsafe.ReadUnaligned<int>(ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index)) == LuminPackCode.NullCollection;
#endif
    }

    /// <summary>
    /// Reads a string length header in length-recording mode.
    /// </summary>
    /// <param name = "index"></param>
    /// <param name = "length"></param>
    /// <returns></returns>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public bool TryReadStringHead<TOptions>(ref int index, out int length)
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
        if ((default(TOptions).StringRecording == global::LuminPack.Option.LuminPackStringRecording.Token))
            LuminPackExceptionHelper.ThrowFailedParseStringWithLength();
#if NET8_0_OR_GREATER
        length = Unsafe.ReadUnaligned<int>(ref Unsafe.Add(ref _bufferStart, (nint)(uint)index));
#else
            length = Unsafe.ReadUnaligned<int>(ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index));
#endif
        ValidateStringLength<TOptions>(index, length);
        return length is not LuminPackCode.NullCollection;
    }

    /// <summary>Validates string length before the operation continues.</summary>
    /// <param name = "index">The position at which the operation starts.</param>
    /// <param name = "length">The number of bytes or elements to process.</param>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private void ValidateStringLength<TOptions>(int index, int length)
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
        if (length < 0 && length != LuminPackCode.NullCollection)
            LuminPackExceptionHelper.ThrowInvalidStringLength(length);
        int headerSize = (default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8) ? sizeof(int) * 2 : sizeof(int);
        if ((uint)index > (uint)_bufferReference.Length || (uint)headerSize > (uint)(_bufferReference.Length - index) || (length >= 0 && (uint)length > (uint)(_bufferReference.Length - index - headerSize)))
            LuminPackExceptionHelper.ThrowInSufficientBuffer(length);
    }

    /// <summary>
    /// Calculates the binary string encoding length.
    /// </summary>
    /// <param name = "index"></param>
    /// <param name = "length"></param>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void ReadStringLength<TOptions>(ref int index, out int length)
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
        if ((default(TOptions).StringRecording == global::LuminPack.Option.LuminPackStringRecording.Token))
        {
            var remaining = _bufferReference.Slice(index);
            if ((default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8))
            {
                length = remaining.IndexOf((byte)0);
            }
            else
            {
                var characters = MemoryMarshal.Cast<byte, ushort>(remaining.Slice(0, remaining.Length & ~1));
                var characterLength = characters.IndexOf((ushort)0);
                length = characterLength < 0 ? -1 : checked(characterLength * sizeof(ushort));
            }

            if (length < 0)
                global::LuminPack.Code.LuminPackExceptionHelper.ThrowInvalidDataException("String token terminator was not found.");
            return;
        }

        EnsureReadable(index, sizeof(int));
#if NET8_0_OR_GREATER
        length = Unsafe.ReadUnaligned<int>(ref Unsafe.Add(ref _bufferStart, (nint)(uint)index));
#else
        length = Unsafe.ReadUnaligned<int>(ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index));
#endif
        ValidateStringLength<TOptions>(index, length);
    }

    /// <summary>Returns the size of the string header used by the current recording mode.</summary>
    /// <returns>The result of the operation.</returns>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public int StringRecordLength<TOptions>()
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
        // UTF-16 token mode uses a two-byte 0x0000 terminator because individual zero bytes are common within code units.
        return (default(TOptions).StringRecording == global::LuminPack.Option.LuminPackStringRecording.Token) ? ((default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8) ? 1 : 2) : (default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8) ? 8 : 4;
    }

    /// <summary>
    /// Reads one string record and advances the supplied absolute offset.
    /// Intended for source-generated parsers so the JIT sees a single call and
    /// does not have to keep the offset live across four string helpers.
    /// </summary>
    /// <param name = "index">The position at which the operation starts.</param>
    /// <returns>The result of the operation.</returns>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public string? ReadStringAndAdvance<TOptions>(ref int index)
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
        bool isUtf8 = (default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8);
        if ((default(TOptions).StringRecording == global::LuminPack.Option.LuminPackStringRecording.Token))
        {
            return ReadTokenStringAndAdvance<TOptions>(ref index, isUtf8);
        }

        int recordLength = isUtf8 ? sizeof(int) * 2 : sizeof(int);
        int bufferLength = _bufferReference.Length;
        if ((uint)index > (uint)bufferLength || (uint)recordLength > (uint)(bufferLength - index))
        {
            LuminPackExceptionHelper.ThrowInSufficientBuffer(recordLength);
        }

#if NET8_0_OR_GREATER
        int length = Unsafe.ReadUnaligned<int>(ref Unsafe.Add(ref _bufferStart, (nint)(uint)index));
#else
            int length = Unsafe.ReadUnaligned<int>(ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index));
#endif
        if (length < 0)
        {
            if (length != LuminPackCode.NullCollection)
                LuminPackExceptionHelper.ThrowInvalidStringLength(length);
            index += recordLength;
            return null;
        }
        else if ((uint)length > (uint)(bufferLength - index - recordLength))
        {
            LuminPackExceptionHelper.ThrowInSufficientBuffer(length);
        }

        var value = length == 0 ? string.Empty : isUtf8 ? ReadUtf8StringWithLength<TOptions>(index, length) : ReadUtf16StringWithLength<TOptions>(index, length);
        index += length + recordLength;
        return value;
    }

    /// <summary>Reads token string and advance from the current input.</summary>
    /// <param name = "index">The position at which the operation starts.</param>
    /// <param name = "isUtf8">The is utf8 used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private string? ReadTokenStringAndAdvance<TOptions>(ref int index, bool isUtf8)
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
        ReadStringLength<TOptions>(ref index, out var length);
        // Token mode has no null marker: null and empty are both encoded as an empty
        // token and must therefore deserialize to string.Empty.
        var value = isUtf8 ? ReadUtf8StringWithToken<TOptions>(index, length) : ReadUtf16StringWithToken<TOptions>(index, length);
        index += length + (isUtf8 ? 1 : 2);
        return value;
    }

    /// <summary>
    /// Reads a complete length-prefixed list of strings and advances an absolute offset.
    /// This is a generated-parser fast path that keeps collection bookkeeping out of
    /// very large parser methods after their JIT inline budget has been exhausted.
    /// </summary>
    /// <param name = "index">The position at which the operation starts.</param>
    /// <returns>The result of the operation.</returns>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public List<string>? ReadStringListAndAdvance<TOptions>(ref int index)
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
        if (!TryReadCollectionHead(ref index, out int count))
        {
            index += sizeof(int);
            return null;
        }

        var value = new List<string>(count);
        int itemOffset = index + sizeof(int);
        var items = LuminPackMarshal.DangerousGetFreshListArray(value, count);
        for (int i = 0; i < items.Length; i++)
        {
            items[i] = ReadStringAndAdvance<TOptions>(ref itemOffset)!;
        }

        index = itemOffset;
        return value;
    }

    /// <summary>
    /// Reads a serialized string.
    /// </summary>
    /// <returns></returns>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public string? ReadString<TOptions>()
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
        if ((default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8))
            return (default(TOptions).StringRecording == global::LuminPack.Option.LuminPackStringRecording.Token) ? ReadUtf8StringWithToken<TOptions>(_currentIndex) : ReadUtf8StringWithLength<TOptions>(_currentIndex);
        return (default(TOptions).StringRecording == global::LuminPack.Option.LuminPackStringRecording.Token) ? ReadUtf16StringWithToken<TOptions>(_currentIndex) : ReadUtf16StringWithLength<TOptions>(_currentIndex);
    }

    /// <summary>
    /// Reads a string with a known length.
    /// </summary>
    /// <returns></returns>
    /// <param name = "length">The number of bytes or elements to process.</param>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public string? ReadString<TOptions>(int length)
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
        if ((default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8))
            return (default(TOptions).StringRecording == global::LuminPack.Option.LuminPackStringRecording.Token) ? ReadUtf8StringWithToken<TOptions>(_currentIndex, length) : ReadUtf8StringWithLength<TOptions>(_currentIndex, length);
        return (default(TOptions).StringRecording == global::LuminPack.Option.LuminPackStringRecording.Token) ? ReadUtf16StringWithToken<TOptions>(_currentIndex, length) : ReadUtf16StringWithLength<TOptions>(_currentIndex, length);
    }

    /// <summary>
    /// Reads a serialized string.
    /// </summary>
    /// <returns></returns>
    /// <param name = "index">The position at which the operation starts.</param>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public string? ReadString<TOptions>(ref int index)
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
        if ((default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8))
            return (default(TOptions).StringRecording == global::LuminPack.Option.LuminPackStringRecording.Token) ? ReadUtf8StringWithToken<TOptions>(index) : ReadUtf8StringWithLength<TOptions>(index);
        return (default(TOptions).StringRecording == global::LuminPack.Option.LuminPackStringRecording.Token) ? ReadUtf16StringWithToken<TOptions>(index) : ReadUtf16StringWithLength<TOptions>(index);
    }

    /// <summary>
    /// Reads a string with a known length.
    /// </summary>
    /// <returns></returns>
    /// <param name = "index">The position at which the operation starts.</param>
    /// <param name = "length">The number of bytes or elements to process.</param>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public string? ReadString<TOptions>(int index, int length)
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
        if ((default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8))
            return (default(TOptions).StringRecording == global::LuminPack.Option.LuminPackStringRecording.Token) ? ReadUtf8StringWithToken<TOptions>(index, length) : ReadUtf8StringWithLength<TOptions>(index, length);
        return (default(TOptions).StringRecording == global::LuminPack.Option.LuminPackStringRecording.Token) ? ReadUtf16StringWithToken<TOptions>(index, length) : ReadUtf16StringWithLength<TOptions>(index, length);
    }

    /// <summary>Reads utf8 string with token from the current input.</summary>
    /// <param name = "index">The position at which the operation starts.</param>
    /// <returns>The result of the operation.</returns>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private string? ReadUtf8StringWithToken<TOptions>(int index)
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
        ReadStringLength<TOptions>(ref index, out var length);
#if NET8_0_OR_GREATER
        ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif
        return Encoding.UTF8.GetString(CreateSpan(ref spanRef, length));
    }

    /// <summary>Reads utf8 string with token from the current input.</summary>
    /// <param name = "index">The position at which the operation starts.</param>
    /// <param name = "length">The number of bytes or elements to process.</param>
    /// <returns>The result of the operation.</returns>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private string? ReadUtf8StringWithToken<TOptions>(int index, int length)
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
#if NET8_0_OR_GREATER
        ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif
        return Encoding.UTF8.GetString(CreateSpan(ref spanRef, length));
    }

    /// <summary>Reads utf16 string with token from the current input.</summary>
    /// <param name = "index">The position at which the operation starts.</param>
    /// <returns>The result of the operation.</returns>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private string? ReadUtf16StringWithToken<TOptions>(int index)
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
        ReadStringLength<TOptions>(ref index, out var length);
#if NET8_0_OR_GREATER
        ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif
        return Encoding.Unicode.GetString(CreateSpan(ref spanRef, length));
    }

    /// <summary>Reads utf16 string with token from the current input.</summary>
    /// <param name = "index">The position at which the operation starts.</param>
    /// <param name = "length">The number of bytes or elements to process.</param>
    /// <returns>The result of the operation.</returns>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private string? ReadUtf16StringWithToken<TOptions>(int index, int length)
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
#if NET8_0_OR_GREATER
        ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index);
#endif
        return Encoding.Unicode.GetString(CreateSpan(ref spanRef, length));
    }

    /// <summary>Reads utf8 string with length from the current input.</summary>
    /// <param name = "index">The position at which the operation starts.</param>
    /// <returns>The result of the operation.</returns>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private string? ReadUtf8StringWithLength<TOptions>(int index)
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
        ReadStringLength<TOptions>(ref index, out var length);
        return ReadUtf8StringWithLength<TOptions>(index, length);
    }

    /// <summary>Reads utf8 string with length from the current input.</summary>
    /// <param name = "index">The position at which the operation starts.</param>
    /// <param name = "length">The number of bytes or elements to process.</param>
    /// <returns>The result of the operation.</returns>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private string? ReadUtf8StringWithLength<TOptions>(int index, int length)
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
        if (length < 0)
            return null;
        if (length == 0)
            return string.Empty;
        int index1 = index + 4;
#if NET8_0_OR_GREATER
        ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index1);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index1);
#endif
        var utf16Length = Unsafe.ReadUnaligned<int>(ref spanRef);
#if NETSTANDARD2_1
            // Unity Mono's implementation of the custom span decoder can report
            // InvalidData for valid four-byte UTF-8 scalar values (for example emoji).
            // The runtime decoder is correct on Unity and this branch is isolated to
            // the Unity-targeted netstandard2.1 build.
            return Encoding.UTF8.GetString(
                LuminPackMarshal.CreateReadOnlySpan(ref Unsafe.Add(ref spanRef, 4), length));
#else
        if (utf16Length <= 0)
        {
            var src = LuminPackMarshal.CreateReadOnlySpan(ref Unsafe.Add(ref spanRef, 4), length);
            return Encoding.UTF8.GetString(src);
        }
        else
        {
            if ((uint)utf16Length > (uint)length)
                LuminPackExceptionHelper.ThrowFailedEncoding(OperationStatus.InvalidData);
            if (utf16Length == length)
            {
                if ((uint)(length - 1) <= 2)
                {
                    var tinyString = ReadTinyAsciiString(ref Unsafe.Add(ref spanRef, 4), length);
                    if (tinyString is not null)
                        return tinyString;
                }

                if (Vector128.IsHardwareAccelerated && (uint)(length - 4) <= 28)
                {
                    var shortString = length <= 16 ? ReadShortAsciiString(ref Unsafe.Add(ref spanRef, 4), length) : ShortAsciiTranscoder.TryDecodeMedium(ref Unsafe.Add(ref spanRef, 4), length);
                    if (shortString is not null)
                        return shortString;
                }

                unsafe
                {
                    fixed (byte* p = &Unsafe.Add(ref spanRef, 4))
                    {
                        var asciiBytes = new ReadOnlySpan<byte>(p, length);
                        if (Ascii.IsValid(asciiBytes))
                            return string.Create(length, new AsciiStringState(p, length), DecodeAsciiString);
                    }
                }
            }

            unsafe
            {
                fixed (byte* p = &Unsafe.Add(ref spanRef, 4))
                {
                    return string.Create(utf16Length, new Utf8StringState(p, length), DecodeUtf8String);
                }
            }
        }
#endif
    }

    /// <summary>Reads utf16 string with length from the current input.</summary>
    /// <param name = "index">The position at which the operation starts.</param>
    /// <returns>The result of the operation.</returns>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private string? ReadUtf16StringWithLength<TOptions>(int index)
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
        ReadStringLength<TOptions>(ref index, out var length);
        return ReadUtf16StringWithLength<TOptions>(index, length);
    }

    /// <summary>Reads utf16 string with length from the current input.</summary>
    /// <param name = "index">The position at which the operation starts.</param>
    /// <param name = "length">The number of bytes or elements to process.</param>
    /// <returns>The result of the operation.</returns>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private string? ReadUtf16StringWithLength<TOptions>(int index, int length)
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
        if (length < 0)
            return null;
        if (length == 0)
            return string.Empty;
        if ((length & 1) != 0)
        {
            LuminPackExceptionHelper.ThrowFailedEncoding(OperationStatus.InvalidData);
        }

        int index1 = index + 4;
        var utf16Length = length >> 1;
#if NET8_0_OR_GREATER
        ref var spanRef = ref Unsafe.Add(ref _bufferStart, (nint)(uint)index1);
#else
            ref var spanRef = ref Unsafe.Add(ref Unsafe.AsRef<byte>(_bufferStart), (nint)(uint)index1);
#endif
        fixed (byte* pointer = &spanRef)
        {
            return new string((char*)pointer, 0, utf16Length);
        }
    }

}
