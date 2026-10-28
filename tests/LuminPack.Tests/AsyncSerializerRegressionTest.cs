using LuminPack;

namespace LuminPackUnitTest;

/// <summary>Provides async serializer regression test functionality.</summary>
internal static class AsyncSerializerRegressionTest
{
    /// <summary>Verifies run.</summary>
    /// <param name="results">The results used by this operation.</param>
    public static void Run(List<string> results)
    {
        RunCase(results, nameof(SerializeAsyncMatchesSynchronousBytes), SerializeAsyncMatchesSynchronousBytes);
        RunCase(results, nameof(DeserializeAsyncHandlesExactBufferBoundary), DeserializeAsyncHandlesExactBufferBoundary);
        RunCase(results, nameof(DeserializeAsyncHandlesChunkedMultiSegmentInput), DeserializeAsyncHandlesChunkedMultiSegmentInput);
        RunCase(results, nameof(DeserializeAsyncRecoversAfterReadFailure), DeserializeAsyncRecoversAfterReadFailure);
    }

    /// <summary>Serializes the supplied value using the selected format and options.</summary>
    private static void SerializeAsyncMatchesSynchronousBytes()
    {
        var value = Enumerable.Range(0, 4096).ToArray();
        var expected = LuminPackSerializer.Serialize(value);
        using var stream = new MemoryStream();

        LuminPackSerializer.SerializeAsync(stream, value).AsTask().GetAwaiter().GetResult();

        Assert(stream.ToArray().AsSpan().SequenceEqual(expected),
            "SerializeAsync produced bytes different from synchronous Serialize.");
    }

    /// <summary>Deserializes the supplied input using the selected format and options.</summary>
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

    /// <summary>Deserializes the supplied input using the selected format and options.</summary>
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

    /// <summary>Deserializes the supplied input using the selected format and options.</summary>
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

    /// <summary>Verifies fill.</summary>
    /// <param name="bytes">The bytes used by this operation.</param>
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

    /// <summary>Verifies run case.</summary>
    /// <param name="results">The results used by this operation.</param>
    /// <param name="name">The name used by this operation.</param>
    /// <param name="test">The test used by this operation.</param>
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

    /// <summary>Verifies assert.</summary>
    /// <param name="condition">The condition used by this operation.</param>
    /// <param name="message">The message describing the failure.</param>
    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    /// <summary>Provides chunked read stream functionality.</summary>
    private sealed class ChunkedReadStream : Stream
    {
        private readonly byte[] _data;
        private readonly int _chunkSize;
        private readonly int _throwAfterReads;
        private int _position;
        private int _reads;

        /// <summary>Initializes a new instance of <c>ChunkedReadStream</c>.</summary>
        /// <param name="data">The metadata describing the serializable type.</param>
        /// <param name="chunkSize">The chunk size used by this operation.</param>
        /// <param name="throwAfterReads">The throw after reads used by this operation.</param>
        public ChunkedReadStream(byte[] data, int chunkSize, int throwAfterReads = -1)
        {
            _data = data;
            _chunkSize = chunkSize;
            _throwAfterReads = throwAfterReads;
        }

        /// <summary>Gets the can read.</summary>
        public override bool CanRead => true;
        /// <summary>Gets the can seek.</summary>
        public override bool CanSeek => false;
        /// <summary>Gets the can write.</summary>
        public override bool CanWrite => false;
        /// <summary>Gets the length.</summary>
        public override long Length => _data.Length;
        /// <summary>Gets or sets the position.</summary>
        public override long Position { get => _position; set => throw new NotSupportedException(); }

        /// <summary>Reads async from the current input.</summary>
        /// <param name="buffer">The buffer used by this operation.</param>
        /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
        /// <returns>The result of the operation.</returns>
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

        /// <summary>Reads  from the current input.</summary>
        /// <param name="buffer">The buffer used by this operation.</param>
        /// <param name="offset">The position at which the operation starts.</param>
        /// <param name="count">The number of entries involved in the operation.</param>
        /// <returns>The result of the operation.</returns>
        public override int Read(byte[] buffer, int offset, int count)
            => throw new NotSupportedException();
        /// <summary>Publishes pending buffer contents to the configured destination.</summary>
        public override void Flush() { }
        /// <summary>Verifies seek.</summary>
        /// <param name="offset">The position at which the operation starts.</param>
        /// <param name="origin">The origin used by this operation.</param>
        /// <returns>The result of the operation.</returns>
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        /// <summary>Updates the length used by the current operation.</summary>
        /// <param name="value">The value consumed or produced by this operation.</param>
        public override void SetLength(long value) => throw new NotSupportedException();
        /// <summary>Writes  to the current output.</summary>
        /// <param name="buffer">The buffer used by this operation.</param>
        /// <param name="offset">The position at which the operation starts.</param>
        /// <param name="count">The number of entries involved in the operation.</param>
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
