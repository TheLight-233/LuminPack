using LuminPack.Option;

namespace LuminPack.Core;

public unsafe ref partial struct LuminPackWriter
{
    /// <summary>Writes the named version-tolerant header with the default binary string profile.</summary>
    /// <param name="count">The number of table entries, at most 249.</param>
    public void WriteVersionHeader(byte count) => WriteVersionHeader<LuminPackDefaultOptions>(count);

    /// <summary>Writes the record marker, revision, binary string profile and member count.</summary>
    /// <typeparam name="TOptions">The compiled binary profile.</typeparam>
    /// <param name="count">The number of table entries, at most 249.</param>
    public void WriteVersionHeader<TOptions>(byte count) where TOptions : struct, ILuminPackOptions
    {
        byte profile = (byte)(((int)default(TOptions).StringEncoding << 1) | (int)default(TOptions).StringRecording);
        EnsureAdditionalCapacity(4);
        Advance(WriteUnmanaged((byte)251, (byte)1, profile, count));
    }

    /// <summary>Copies the complete cached name and type table at its dynamic framing boundary.</summary>
    /// <param name="metadata">The generated immutable metadata table.</param>
    /// <param name="count">The number of payload lengths that follow the metadata.</param>
    public void WriteVersionMetadata(scoped ReadOnlySpan<byte> metadata, int count)
    {
        EnsureAdditionalCapacity(checked(metadata.Length + count * 5));
        metadata.CopyTo(_bufferReference.Slice(_currentIndex));
        _currentIndex += metadata.Length;
    }
}
