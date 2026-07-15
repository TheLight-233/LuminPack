using System.Buffers;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using K4os.Compression.LZ4;
using LuminPack;
using LuminPack.Utility;
using ZstdSharp;
using Snappier;
using System.IO.Compression;
using MemoryPack;
using MemoryPack.Compression;

namespace LuminPackBenchmark;

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
    
    SimpleClass data = SimpleClass.Create();
    
    LuminBufferWriter _bufferWriter1 = LuminBufferWriterPool.Rent();
    LuminBufferWriter _bufferWriter2 = LuminBufferWriterPool.Rent();
    private ArrayBufferWriter<byte> _arrayBufferWriter = new ArrayBufferWriter<byte>();

    [GlobalSetup]
    public void Setup()
    {
        _testData = LuminPackSerializer.Serialize(SimpleClass.Create());
        LuminPackSerializer.Serialize(SimpleClass.Create(), _bufferWriter1);
    }
    
    [Benchmark(Baseline = true)]
    public void LuminCompress()
    {
        LuminPackSerializer.Compress(_bufferWriter1, _bufferWriter2);
    }

    // ===== LZ4 系列 =====
    [Benchmark]
    public void LZ4Pickle()
    {
        _arrayBufferWriter.ResetWrittenCount();
        LZ4Pickler.Pickle(_testData,  _arrayBufferWriter);
    }

    [Benchmark]
    public void Zstd()
    {
        ZstdSharp.Zstd.Compress(_testData);
    }
    
    [Benchmark]
    public void SnappyCompress()
    {
        using var compressed = Snappy.CompressToMemory(_testData);
    }

    internal static int BrotliEncoderMaxCompressedSize(int input_size)
    {
        int num1 = 2 + 4 * (input_size >> 14) + 3 + 1;
        int num2 = input_size + num1;
        if (input_size == 0)
            return 2;
        return num2 >= input_size ? num2 : 0;
    }
    
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
    
    [Benchmark]
    public void LuminDecompress()
    {
        LuminPackSerializer.Compress(_bufferWriter1, _bufferWriter2);
        LuminPackSerializer.Decompress(_bufferWriter2, _bufferWriter1);
    }
    
    [Benchmark]
    public void LZ4Decompress()
    {
        var compressed = LZ4Pickler.Pickle(_testData);
        LZ4Pickler.Unpickle(compressed);
    }
    
    
    [Benchmark]
    public void SnappyDecompress()
    {
        using var compressed = Snappy.CompressToMemory(_testData);
        Snappy.DecompressToMemory(compressed.Memory.Span);
    }

}