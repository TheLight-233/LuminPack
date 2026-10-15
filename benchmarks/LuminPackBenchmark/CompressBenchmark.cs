using System.Buffers;
using System.IO.Compression;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using K4os.Compression.LZ4;
using LuminPack;
using LuminPack.Utility;
using Snappier;

namespace LuminPackBenchmark;

/// <summary>Provides compress benchmark functionality.</summary>
[HideColumns("StdDev", "RatioSD", "Error")]
[MinColumn, MaxColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[ShortRunJob(RuntimeMoniker.Net10_0)]
[MemoryDiagnoser]
[GcServer]
[MarkdownExporterAttribute.GitHub]
public class CompressBenchmark
{
    private byte[] _testData;
    private byte[] _scratch;
    private const string RatioCategory = "Ratio";

    SimpleClass data = SimpleClass.Create();

    LuminBufferWriter _bufferWriter1 = LuminBufferWriterPool.Rent();
    LuminBufferWriter _bufferWriter2 = LuminBufferWriterPool.Rent();
    private ArrayBufferWriter<byte> _arrayBufferWriter = new ArrayBufferWriter<byte>();

    /// <summary>Updates the up used by the current operation.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _testData = LuminPackSerializer.Serialize(SimpleClass.Create());
        LuminPackSerializer.Serialize(SimpleClass.Create(), _bufferWriter1);
        _scratch = ArrayPool<byte>.Shared.Rent(LuminCompressor.GetMaxCompressedSize(_testData.Length));
    }

    /// <summary>Measures cleanup for the configured benchmark input.</summary>
    [GlobalCleanup]
    public void Cleanup() => ArrayPool<byte>.Shared.Return(_scratch);

    /// <summary>Measures lumin compress for the configured benchmark input.</summary>
    [Benchmark(Baseline = true)]
    public void LuminCompress()
    {
        LuminPackSerializer.Compress(_bufferWriter1, _bufferWriter2);
    }

    // ===== LZ4 implementations =====
    /// <summary>Measures lz4 pickle for the configured benchmark input.</summary>
    [Benchmark]
    public void LZ4Pickle()
    {
        _arrayBufferWriter.ResetWrittenCount();
        LZ4Pickler.Pickle(_testData, _arrayBufferWriter);
    }

    /// <summary>Measures zstd for the configured benchmark input.</summary>
    [Benchmark]
    public void Zstd()
    {
        ZstdSharp.Zstd.Compress(_testData);
    }

    /// <summary>Measures snappy compress for the configured benchmark input.</summary>
    [Benchmark]
    public void SnappyCompress()
    {
        using var compressed = Snappy.CompressToMemory(_testData);
    }

    /// <summary>Measures brotli encoder max compressed size for the configured benchmark input.</summary>
    /// <param name="input_size">The input size used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    internal static int BrotliEncoderMaxCompressedSize(int input_size)
    {
        int num1 = 2 + 4 * (input_size >> 14) + 3 + 1;
        int num2 = input_size + num1;
        if (input_size == 0)
            return 2;
        return num2 >= input_size ? num2 : 0;
    }

    /// <summary>Measures brotli fastest for the configured benchmark input.</summary>
    [Benchmark]
    public void BrotliFastest()
    {

        var buffer = ArrayPool<byte>.Shared.Rent(BrotliEncoderMaxCompressedSize(_testData.Length));
        try
        {
            using (BrotliEncoder brotliEncoder = new BrotliEncoder(1, 22))
            {
                brotliEncoder.Compress(_testData, buffer, out int bytesConsumed, out int bytesWritten, true);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    /// <summary>Measures lumin decompress for the configured benchmark input.</summary>
    [Benchmark]
    public void LuminDecompress()
    {
        LuminPackSerializer.Compress(_bufferWriter1, _bufferWriter2);
        LuminPackSerializer.Decompress(_bufferWriter2, _bufferWriter1);
    }

    /// <summary>Measures lz4 decompress for the configured benchmark input.</summary>
    [Benchmark]
    public void LZ4Decompress()
    {
        var compressed = LZ4Pickler.Pickle(_testData);
        LZ4Pickler.Unpickle(compressed);
    }


    /// <summary>Measures snappy decompress for the configured benchmark input.</summary>
    [Benchmark]
    public void SnappyDecompress()
    {
        using var compressed = Snappy.CompressToMemory(_testData);
        Snappy.DecompressToMemory(compressed.Memory.Span);
    }

    // ===== Ratio group: return compressed byte count so BenchmarkDotNet prints it =====
    /// <summary>Measures lumin size for the configured benchmark input.</summary>
    /// <returns>The result of the operation.</returns>
    [Benchmark, BenchmarkCategory(RatioCategory)]
    public int LuminSize()
    {
        return LuminCompressor.Compress(_testData, _scratch);
    }

    /// <summary>Measures lz4 size for the configured benchmark input.</summary>
    /// <returns>The result of the operation.</returns>
    [Benchmark, BenchmarkCategory(RatioCategory)]
    public int LZ4Size()
    {
        _arrayBufferWriter.ResetWrittenCount();
        LZ4Pickler.Pickle(_testData, _arrayBufferWriter);
        return _arrayBufferWriter.WrittenCount;
    }

    /// <summary>Measures zstd size for the configured benchmark input.</summary>
    /// <returns>The result of the operation.</returns>
    [Benchmark, BenchmarkCategory(RatioCategory)]
    public int ZstdSize()
    {
        return ZstdSharp.Zstd.Compress(_testData).Length;
    }

    /// <summary>Measures snappy size for the configured benchmark input.</summary>
    /// <returns>The result of the operation.</returns>
    [Benchmark, BenchmarkCategory(RatioCategory)]
    public int SnappySize()
    {
        using var compressed = Snappy.CompressToMemory(_testData);
        return compressed.Memory.Length;
    }

    /// <summary>Measures brotli size for the configured benchmark input.</summary>
    /// <returns>The result of the operation.</returns>
    [Benchmark, BenchmarkCategory(RatioCategory)]
    public int BrotliSize()
    {
        var buffer = ArrayPool<byte>.Shared.Rent(BrotliEncoderMaxCompressedSize(_testData.Length));
        try
        {
            using (BrotliEncoder brotliEncoder = new BrotliEncoder(1, 22))
            {
                brotliEncoder.Compress(_testData, buffer, out int bytesConsumed, out int bytesWritten, true);
                return bytesWritten;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}
