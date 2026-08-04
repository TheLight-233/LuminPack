using LuminPack;

namespace LuminPackUnitTest;

internal static class AsyncSerializerRegressionTest
{
    public static void Run(List<string> results)
    {
        RunCase(results, nameof(SerializeAsyncMatchesSynchronousBytes), SerializeAsyncMatchesSynchronousBytes);
        RunCase(results, nameof(DeserializeAsyncHandlesExactBufferBoundary), DeserializeAsyncHandlesExactBufferBoundary);
        RunCase(results, nameof(DeserializeAsyncHandlesChunkedMultiSegmentInput), DeserializeAsyncHandlesChunkedMultiSegmentInput);
        RunCase(results, nameof(DeserializeAsyncRecoversAfterReadFailure), DeserializeAsyncRecoversAfterReadFailure);
    }

    private static void SerializeAsyncMatchesSynchronousBytes()
    {
        var value = Enumerable.Range(0, 4096).ToArray();
        var expected = LuminPackSerializer.Serialize(value);
        using var stream = new MemoryStream();

        LuminPackSerializer.SerializeAsync(stream, value).AsTask().GetAwaiter().GetResult();

        Assert(stream.ToArray().AsSpan().SequenceEqual(expected),
            "SerializeAsync produced bytes different from synchronous Serialize.");
    }

    private static void DeserializeAsyncHandlesExactBufferBoundary()
    {
        // byte[] wire format is [count:4][payload], exactly matching the first 64 KiB rent.
        var value = new byte[65_532];
        Fill(value);
        var bytes = LuminPackSerializer.Serialize(value);
        Assert(bytes.Length == 65_536, $"Boundary fixture was {bytes.Length} bytes instead of 65,536.");

        using var stream = new ChunkedReadStream(bytes, bytes.Length);
        var roundTrip = LuminPackSerializer.DeserializeAsync<byte[]>(stream).AsTask().GetAwaiter().GetResult();
        Assert(roundTrip is not null && roundTrip.AsSpan().SequenceEqual(value),
            "DeserializeAsync failed at the exact initial-buffer boundary.");
    }

    private static void DeserializeAsyncHandlesChunkedMultiSegmentInput()
    {
        var value = new byte[180_000];
        Fill(value);
        var bytes = LuminPackSerializer.Serialize(value);

        using var stream = new ChunkedReadStream(bytes, 997);
        var roundTrip = LuminPackSerializer.DeserializeAsync<byte[]>(stream).AsTask().GetAwaiter().GetResult();
        Assert(roundTrip is not null && roundTrip.AsSpan().SequenceEqual(value),
            "DeserializeAsync corrupted a multi-segment, short-read stream.");
    }

    private static void DeserializeAsyncRecoversAfterReadFailure()
    {
        var value = Enumerable.Range(0, 1000).ToArray();
        var bytes = LuminPackSerializer.Serialize(value);

        using (var failing = new ChunkedReadStream(bytes, 128, throwAfterReads: 1))
        {
            var threw = false;
            try
            {
                LuminPackSerializer.DeserializeAsync<int[]>(failing).AsTask().GetAwaiter().GetResult();
            }
            catch (IOException)
            {
                threw = true;
            }
            Assert(threw, "DeserializeAsync swallowed a stream read failure.");
        }

        using var valid = new ChunkedReadStream(bytes, 73);
        var roundTrip = LuminPackSerializer.DeserializeAsync<int[]>(valid).AsTask().GetAwaiter().GetResult();
        Assert(roundTrip is not null && roundTrip.SequenceEqual(value),
            "A failed async read poisoned the pooled sequence builder.");
    }

    private static void Fill(Span<byte> bytes)
    {
        uint state = 0x9E37_79B9u;
        for (var i = 0; i < bytes.Length; i++)
        {
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            bytes[i] = (byte)state;
        }
    }

    private static void RunCase(List<string> results, string name, Action test)
    {
        try
        {
            test();
            results.Add($"✓ {name} - PASSED");
        }
        catch (Exception ex)
        {
            results.Add($"✗ {name} - ERROR: {ex.Message}");
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class ChunkedReadStream : Stream
    {
        private readonly byte[] _data;
        private readonly int _chunkSize;
        private readonly int _throwAfterReads;
        private int _position;
        private int _reads;

        public ChunkedReadStream(byte[] data, int chunkSize, int throwAfterReads = -1)
        {
            _data = data;
            _chunkSize = chunkSize;
            _throwAfterReads = throwAfterReads;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => _data.Length;
        public override long Position { get => _position; set => throw new NotSupportedException(); }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_throwAfterReads >= 0 && _reads++ >= _throwAfterReads)
                throw new IOException("Injected read failure.");
            if (_position == _data.Length)
                return ValueTask.FromResult(0);

            var count = Math.Min(Math.Min(_chunkSize, buffer.Length), _data.Length - _position);
            _data.AsSpan(_position, count).CopyTo(buffer.Span);
            _position += count;
            return ValueTask.FromResult(count);
        }

        public override int Read(byte[] buffer, int offset, int count)
            => throw new NotSupportedException();
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
