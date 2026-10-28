#if NET8_0_OR_GREATER
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text.Unicode;

namespace LuminPack.Utility;

public static partial class StringSerializer
{
    /// <summary>Keeps mixed Latin1 conversion and platform fallback state outside the ordinary encoder's stack frame.</summary>
    /// <param name="source">The input beginning with non-ASCII Latin1.</param>
    /// <param name="destination">The bounded byte destination.</param>
    /// <param name="charsRead">Receives the total consumed character count.</param>
    /// <param name="bytesWritten">Receives the total produced byte count.</param>
    /// <param name="replaceInvalidSequences">Whether invalid UTF-16 is replaced.</param>
    /// <param name="isFinalBlock">Whether this is the final input block.</param>
    /// <returns>The complete prefix and Unicode tail conversion status.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static OperationStatus SerializeLatin1AsUtf8(ReadOnlySpan<char> source, Span<byte> destination,
        out int charsRead, out int bytesWritten, bool replaceInvalidSequences, bool isFinalBlock)
    {
        global::LuminPack.Internal.Utf8Latin1Transcoder.EncodePrefix(source, destination, out charsRead, out bytesWritten);
        if (charsRead == source.Length) return OperationStatus.Done;
        var status = Utf8.FromUtf16(source[charsRead..], destination[bytesWritten..], out int tailChars, out int tailBytes,
            replaceInvalidSequences, isFinalBlock);
        charsRead += tailChars;
        bytesWritten += tailBytes;
        return status;
    }
}
#endif
