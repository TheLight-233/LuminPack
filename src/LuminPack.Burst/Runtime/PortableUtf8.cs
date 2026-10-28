#if LUMINPACK_BURST
using System;
using System.Buffers;
namespace LuminPack.Burst
{
    /// <summary>Maps unavailable platform UTF-8 intrinsics to Core's established portable loop.</summary>
    internal static class PortableUtf8
    {
        /// <summary>Encodes through Core's portable status and replacement implementation.</summary><param name="source">Input.</param><param name="destination">Output.</param><param name="charsRead">Consumed characters.</param><param name="bytesWritten">Produced bytes.</param><param name="replaceInvalidSequences">Replacement policy.</param><param name="isFinalBlock">Final block policy.</param><returns>The operation status.</returns>
        internal static OperationStatus FromUtf16(ReadOnlySpan<char> source, Span<byte> destination, out int charsRead, out int bytesWritten, bool replaceInvalidSequences = true, bool isFinalBlock = true) => CoreTextFallback.Encode(source, destination, out charsRead, out bytesWritten, replaceInvalidSequences, isFinalBlock);
        /// <summary>Decodes through Core's portable status and replacement implementation.</summary><param name="source">Input.</param><param name="destination">Output.</param><param name="bytesRead">Consumed bytes.</param><param name="charsWritten">Produced characters.</param><param name="replaceInvalidSequences">Replacement policy.</param><param name="isFinalBlock">Final block policy.</param><returns>The operation status.</returns>
        internal static unsafe OperationStatus ToUtf16(ReadOnlySpan<byte> source, Span<char> destination, out int bytesRead, out int charsWritten, bool replaceInvalidSequences = true, bool isFinalBlock = true)
        {
            if (BitConverter.IsLittleEndian && Vector256.IsHardwareAccelerated && source.Length > 32 && destination.Length >= source.Length && source[0] < 128)
            {
                fixed (byte* input = source) fixed (char* output = destination)
                    if (CoreTextAlgorithms.TryWidenAscii(ref *input, source.Length, ref *(ushort*)output)) { bytesRead = charsWritten = source.Length; return OperationStatus.Done; }
            }
            return CoreTextAlgorithms.DeserializeFromUtf8(source, destination, out bytesRead, out charsWritten, replaceInvalidSequences, isFinalBlock);
        }
    }
    /// <summary>Uses Core's bounded comparison fallback for the unavailable platform ASCII primitive.</summary>
    internal static class PortableAscii
    {
        /// <summary>Compares complete encoded and character ranges with strict validation.</summary><param name="bytes">Encoded input.</param><param name="text">Candidate.</param><returns>Whether valid text matches.</returns>
        internal static bool Equals(ReadOnlySpan<byte> bytes, ReadOnlySpan<char> text) => CoreTextAlgorithms.MatchesUtf8Chunks(bytes, text);
    }
}
#endif

