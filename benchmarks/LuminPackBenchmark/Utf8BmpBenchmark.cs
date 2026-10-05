using System.Text;
using System.Text.Unicode;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using LuminPack.Utility;

namespace LuminPackBenchmark;

[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net10_0, launchCount: 1, warmupCount: 5, iterationCount: 12)]
public class Utf8BmpBenchmark
{
    [Params(32, 4096)] public int Length { get; set; }
    [Params("Chinese", "ChineseFirstMixed", "Ascii", "Emoji")] public string Data { get; set; } = "";
    private string _source = null!;
    private byte[] _destination = null!;
    [GlobalSetup] public void Setup()
    {
        string pattern = Data switch { "Chinese" => "中文序列化性能测试", "ChineseFirstMixed" => "中文LuminPack123", "Ascii" => "LuminPack123", _ => "😀中文🚀" };
        _source = string.Concat(Enumerable.Repeat(pattern, (Length + pattern.Length - 1) / pattern.Length)).Substring(0, Length);
        _destination = new byte[Encoding.UTF8.GetByteCount(_source)];
        int count = Runtime();
        if (!_destination.AsSpan(0, count).SequenceEqual(Encoding.UTF8.GetBytes(_source))) throw new InvalidOperationException("Output differs");
    }
    [Benchmark(Baseline = true)] public int Platform()
    {
        Utf8.FromUtf16(_source, _destination, out _, out int bytes);
        return bytes;
    }
    // Replicates the pre-CJK production wrapper: same non-inlined frame as
    // StringSerializer.Serialize but without the BMP prefix kernel, so the
    // measured difference is the marginal kernel effect rather than the
    // pre-existing wrapper call.
    [Benchmark] public int BeforeKernel() => SerializeWithoutBmpKernel(_source, _destination);
    [Benchmark] public int Runtime()
    {
        StringSerializer.Serialize(_source, _destination, out _, out int bytes);
        return bytes;
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static int SerializeWithoutBmpKernel(string source, byte[] destination)
    {
        Utf8.FromUtf16(source, destination, out _, out int bytes, true, true);
        return bytes;
    }
}
