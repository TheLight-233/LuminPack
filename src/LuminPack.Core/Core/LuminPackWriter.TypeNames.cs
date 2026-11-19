using System.Buffers;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using LuminPack.Code;
using LuminPack.Option;
using LuminPack.Utility;

namespace LuminPack.Core;

/// <summary>Provides allocation-free assembly-qualified name writing through the existing text kernels.</summary>
public ref partial struct LuminPackWriter
{
    /// <summary>Writes a non-null type name after removing its established assembly qualifiers.</summary>
    /// <param name="fullName">The validated assembly-qualified name.</param>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public void WriteTypeName(string fullName) => WriteTypeName<LuminPackDefaultOptions>(fullName);

    /// <summary>Writes a compact type name without constructing a replacement string.</summary>
    /// <typeparam name="TOptions">The compiled encoding and recording profile.</typeparam>
    /// <param name="fullName">The validated assembly-qualified name.</param>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.NoInlining)]
#if NET8_0_OR_GREATER
    [SkipLocalsInit]
#endif
    public void WriteTypeName<TOptions>(string fullName) where TOptions : struct, ILuminPackOptions
    {
        if (!LuminPackTypeNameText.FindRemoval(fullName.AsSpan(), out int start, out int end))
        {
            WriteUnshortenedTypeName<TOptions>(fullName);
            return; }
        char[]? rented = null;
        Span<char> scratch = fullName.Length <= 512 ? stackalloc char[fullName.Length] : (rented = ArrayPool<char>.Shared.Rent(fullName.Length));
        try
        {
            int length = LuminPackTypeNameText.Compact(fullName.AsSpan(), scratch, start, end);
            WriteTypeNameSnapshot<TOptions>(scratch.Slice(0, length));
        }
        finally
        {
            if (rented is not null)
                ArrayPool<char>.Shared.Return(rented); }
    }

    /// <summary>Keeps the unchanged-name string writer outside the normalization method's native budget.</summary>
    /// <typeparam name="TOptions">The compiled binary profile.</typeparam>
    /// <param name="fullName">The original name with no removable qualifiers.</param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void WriteUnshortenedTypeName<TOptions>(string fullName) where TOptions : struct, ILuminPackOptions => Write<TOptions>(fullName);

    /// <summary>Frames a non-null character snapshot using the existing binary text representations.</summary>
    /// <typeparam name="TOptions">The compiled encoding and recording profile.</typeparam>
    /// <param name="text">The compact name, retained until encoding completes.</param>
    private void WriteTypeNameSnapshot<TOptions>(scoped ReadOnlySpan<char> text) where TOptions : struct, ILuminPackOptions =>
        default(TOptions).WriteText(ref this, text);

    /// <summary>Writes a compact name with the code unit and framing chosen by its codec.</summary>
    /// <typeparam name="TUnit">The codec’s byte or UTF-16 code unit.</typeparam>
    /// <typeparam name="TFraming">The codec’s binary record framing.</typeparam>
    /// <param name="text">The compact name snapshot.</param>
    internal void WriteTextSnapshot<TUnit, TFraming>(scoped ReadOnlySpan<char> text) where TUnit : unmanaged where TFraming : struct
    {
        bool utf8 = typeof(TUnit) == typeof(byte);
        bool token = typeof(TFraming) == typeof(TokenStringFraming);
        bool variable = typeof(TFraming) == typeof(VarIntStringFraming);
        if (utf8 && !variable)
        {
            WriteTypeNameUtf8(text, token);
            return; }
        bool latin1 = !utf8 && !token && !text.IsEmpty && StringSerializer.IsLatin1(text);
        int payload = utf8 ? Encoding.UTF8.GetByteCount(text) : latin1 ? text.Length : checked(text.Length * sizeof(char));
        int length = latin1 ? StringSerializer.Latin1Header(text.Length) : payload;
        int prefix = token ? 0 : variable ? VarIntHeaderLength(length) + (utf8 ? VarIntHeaderLength(text.Length) : 0) : utf8 ? 8 : 4;
        int total = checked(prefix + payload + (token ? utf8 ? 1 : 2 : 0));
        if (!FitsRecord(total))
            EnsureAdditionalCapacity(!utf8 && !token && !variable ? checked(4 + text.Length * sizeof(char)) : total);
        var destination = _buffer.Slice(Position, total);
        if (variable)
        {
            int offset = WriteVarIntHeader(destination, length);
            if (utf8)
                WriteVarIntHeader(destination.Slice(offset), text.Length);
        }
        ref byte target = ref MemoryMarshal.GetReference(destination.Slice(prefix));
        if (utf8)
        {
            var status = StringSerializer.Serialize<LuminPackUtf8LengthOptions>(text, destination.Slice(prefix, payload), out _, out _, replaceInvalidSequences: false);
            if (status != OperationStatus.Done)
                LuminPackExceptionHelper.ThrowFailedEncoding(status);
        }
        else if (latin1)
            StringSerializer.TryNarrowLatin1(ref MemoryMarshal.GetReference(text), ref target, text.Length);
        else if (payload != 0)
            StringSerializer.SerializeUtf16(ref target, ref Unsafe.As<char, byte>(ref MemoryMarshal.GetReference(text)), (uint)payload);
        if (token)
        {
            destination[payload] = 0;
            if (!utf8)
                destination[payload + 1] = 0;
        }
        else if (!variable)
        {
            LuminPackWriter.Write<int>(ref MemoryMarshal.GetReference(destination), length);
            if (utf8)
                LuminPackWriter.Write<int>(ref Unsafe.Add(ref MemoryMarshal.GetReference(destination), sizeof(int)), text.Length);
        }
        AdvanceSafe(total);
    }

    /// <summary>Retains the fixed and token UTF-8 encoder's capacity retry and failure-prefix behavior.</summary>
    /// <param name="text">The compact name.</param>
    /// <param name="token">Whether the record ends in the existing zero token.</param>
    private void WriteTypeNameUtf8(scoped ReadOnlySpan<char> text, bool token)
    {
        int index = Position, prefix = token ? 0 : 8;
        if (text.IsEmpty)
        {
            EnsureCapacity(index, token ? 1 : 8);
            if (token)
                Current = 0;
            else
                WriteReservedUtf8Header(index, 0, 0);
            AdvanceSafe(token ? 1 : 8);
            return;
        }
#if NET8_0_OR_GREATER
        if (!token && (ulong)(uint)index + 8 + (uint)text.Length <= (uint)_buffer.Length &&
            global::LuminPack.Internal.ShortAsciiTranscoder.TryEncode(text, _buffer.Slice(index + 8, text.Length), escapeJson: false))
        {
            WriteReservedUtf8Header(index, text.Length, text.Length);
            AdvanceSafe(text.Length + 8);
            return;
        }
#endif
        int start = checked(index + prefix);
        var destination = (uint)start <= (uint)_buffer.Length ? _buffer.Slice(start) : Span<byte>.Empty;
        var status = StringSerializer.Serialize<LuminPackUtf8LengthOptions>(text, destination, out _, out int written, replaceInvalidSequences: false);
        if (status != OperationStatus.Done)
        {
            if (status != OperationStatus.DestinationTooSmall)
                LuminPackExceptionHelper.ThrowFailedEncoding(status);
            EnsureCapacity(index, checked((text.Length + 1) * 3 + prefix + (token ? 1 : 0)));
            written = EncodeUtf8Slow(text, _buffer.Slice(index + prefix));
        }
        if (token)
        {
            EnsureCapacity(index, checked(written + 1));
            _buffer[index + written] = 0;
        }
        else
            WriteReservedUtf8Header(index, written, text.Length);
        AdvanceSafe(written + prefix + (token ? 1 : 0));
    }

}
