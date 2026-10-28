#if NETSTANDARD2_1
using System;
using System.Buffers;
using System.Threading;
using LuminPack.Code;

namespace LuminPack.Core;

public static unsafe partial class LuminPackBurst
{
    /// <summary>Attempts complete UTF-8 encoding with the original replacement and final-block semantics.</summary>
    /// <param name="source">The complete source window.</param>
    /// <param name="destination">The complete output window.</param>
    /// <param name="replaceInvalid">Whether malformed UTF-16 is replaced.</param>
    /// <param name="finalBlock">Whether the input is the final block.</param>
    /// <param name="consumed">Receives consumed characters.</param>
    /// <param name="written">Receives written bytes.</param>
    /// <param name="status">Receives the complete operation status.</param>
    /// <returns>Whether the backend supplied the complete result.</returns>
    internal static bool TryEncodeUtf8(ReadOnlySpan<char> source, Span<byte> destination, bool replaceInvalid, bool finalBlock,
        out int consumed, out int written, out OperationStatus status)
    {
        consumed = written = 0;
        status = OperationStatus.Done;
        var registration = Volatile.Read(ref _registration);
        if (registration is null || registration.Table.Utf8Encode == null) return false;
        uint flags = (replaceInvalid ? 1u : 0u) | (finalBlock ? 2u : 0u);
        int read = 0, produced = 0, result;
        fixed (char* input = source)
        fixed (byte* output = destination)
            result = registration.Table.Utf8Encode(input, source.Length, output, destination.Length, &read, &produced, flags);
        if (!ValidateTextResult(result, read, produced, source.Length, destination.Length, out status)) return false;
        consumed = read;
        written = produced;
        return true;
    }

    /// <summary>Attempts complete UTF-8 decoding without replacing the caller's framing or validation policy.</summary>
    /// <param name="source">The complete encoded window.</param>
    /// <param name="destination">The complete character window.</param>
    /// <param name="replaceInvalid">Whether malformed UTF-8 is replaced.</param>
    /// <param name="finalBlock">Whether the input is final.</param>
    /// <param name="consumed">Receives consumed bytes.</param>
    /// <param name="written">Receives written characters.</param>
    /// <param name="status">Receives the complete operation status.</param>
    /// <returns>Whether the backend supplied the complete result.</returns>
    internal static bool TryDecodeUtf8(ReadOnlySpan<byte> source, Span<char> destination, bool replaceInvalid, bool finalBlock,
        out int consumed, out int written, out OperationStatus status)
    {
        consumed = written = 0;
        status = OperationStatus.Done;
        var registration = Volatile.Read(ref _registration);
        if (registration is null || registration.Table.Utf8Decode == null) return false;
        uint flags = (replaceInvalid ? 1u : 0u) | (finalBlock ? 2u : 0u);
        int read = 0, produced = 0, result;
        fixed (byte* input = source)
        fixed (char* output = destination)
            result = registration.Table.Utf8Decode(input, source.Length, output, destination.Length, &read, &produced, flags);
        if (!ValidateTextResult(result, read, produced, source.Length, destination.Length, out status)) return false;
        consumed = read;
        written = produced;
        return true;
    }

    /// <summary>Checks counts and status before a native codec result can alter managed cursors.</summary>
    /// <param name="result">The native status, or minus one when unsupported.</param>
    /// <param name="read">The consumed count.</param>
    /// <param name="written">The produced count.</param>
    /// <param name="sourceLength">The complete input count.</param>
    /// <param name="destinationLength">The complete output count.</param>
    /// <param name="status">Receives the managed status.</param>
    /// <returns>Whether this is a supported operation result.</returns>
    private static bool ValidateTextResult(int result, int read, int written, int sourceLength, int destinationLength, out OperationStatus status)
    {
        status = OperationStatus.Done;
        ValidateCompleted(read, sourceLength);
        ValidateCompleted(written, destinationLength);
        if (result == -1 && read == 0 && written == 0) return false;
        if ((uint)result > (uint)OperationStatus.InvalidData)
            LuminPackExceptionHelper.ThrowInvalidOperationException("A LuminPackBurst codec returned an invalid status.");
        if (result == (int)OperationStatus.Done && read != sourceLength)
            LuminPackExceptionHelper.ThrowInvalidOperationException("A LuminPackBurst codec completed without consuming its input.");
        status = (OperationStatus)result;
        return true;
    }

    /// <summary>Attempts complete valid UTF-8 equality against a UTF-16 candidate.</summary>
    /// <param name="source">The complete encoded input.</param>
    /// <param name="candidate">The complete candidate.</param>
    /// <param name="equal">Receives equality only for valid exactly matching input.</param>
    /// <returns>Whether the registered comparison supplied a result.</returns>
    internal static bool TryUtf8Equal(ReadOnlySpan<byte> source, ReadOnlySpan<char> candidate, out bool equal)
    {
        equal = false;
        var registration = Volatile.Read(ref _registration);
        if (registration is null || registration.Table.Utf8Equal == null) return false;
        fixed (byte* input = source)
        fixed (char* text = candidate)
            equal = registration.Table.Utf8Equal(input, source.Length, text, candidate.Length) != 0;
        return true;
    }

    /// <summary>Attempts bounded unquoted ASCII encoding, optionally excluding every JSON escape.</summary>
    /// <param name="source">The complete text to validate before writing.</param>
    /// <param name="destination">The complete output window.</param>
    /// <param name="escapeJson">Whether quotes, backslashes and control characters must decline the kernel.</param>
    /// <returns>Whether all characters were encoded; false leaves output untouched.</returns>
    internal static bool TryAsciiEncode(ReadOnlySpan<char> source, Span<byte> destination, bool escapeJson)
    {
        var registration = Volatile.Read(ref _registration);
        if (registration is null || registration.Table.AsciiEncode == null || destination.Length < source.Length) return false;
        fixed (char* input = source)
        fixed (byte* output = destination)
            return registration.Table.AsciiEncode(input, source.Length, output, escapeJson ? 1u : 0u) != 0;
    }
}

#endif
