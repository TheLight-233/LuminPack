using System.Text;
using System.Text.Unicode;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using LuminPack.Utility;

namespace LuminPackBenchmark;

/// <summary>Provides utf8 bmp benchmark functionality.</summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net10_0, launchCount: 1, warmupCount: 5, iterationCount: 12)]
public class Utf8BmpBenchmark
{
    /// <summary>Gets or sets the length.</summary>
    [Params(32, 4096)] public int Length { get; set; }
    /// <summary>Gets or sets the data.</summary>
    [Params("Chinese", "ChineseFirstMixed", "Ascii", "Emoji")] public string Data { get; set; } = "";
    private string _source = null!;
    private byte[] _destination = null!;
    /// <summary>Updates the up used by the current operation.</summary>
    [GlobalSetup]
    public void Setup()
    {
        string pattern = Data switch { "Chinese" => "\u4E2D\u6587\u5E8F\u5217\u5316\u6027\u80FD\u6D4B\u8BD5", "ChineseFirstMixed" => "\u4E2D\u6587LuminPack123", "Ascii" => "LuminPack123", _ => "\uD83D\uDE00\u4E2D\u6587\uD83D\uDE80" };
        _source = string.Concat(Enumerable.Repeat(pattern, (Length + pattern.Length - 1) / pattern.Length)).Substring(0, Length);
        _destination = new byte[Encoding.UTF8.GetByteCount(_source)];
        int count = Runtime();
        if (!_destination.AsSpan(0, count).SequenceEqual(Encoding.UTF8.GetBytes(_source))) throw new InvalidOperationException("Output differs");
    }
    /// <summary>Measures platform for the configured benchmark input.</summary>
    /// <returns>The result of the operation.</returns>
    [Benchmark(Baseline = true)]
    public int Platform()
    {
        Utf8.FromUtf16(_source, _destination, out _, out int bytes);
        return bytes;
    }
    // Replicates the pre-CJK production wrapper: same non-inlined frame as
    // StringSerializer.Serialize but without the BMP prefix kernel, so the
    // measured difference is the marginal kernel effect rather than the
    // pre-existing wrapper call.
    /// <summary>Measures before kernel for the configured benchmark input.</summary>
    /// <returns>The result of the operation.</returns>
    [Benchmark] public int BeforeKernel() => SerializeWithoutBmpKernel(_source, _destination);
    /// <summary>Measures runtime for the configured benchmark input.</summary>
    /// <returns>The result of the operation.</returns>
    [Benchmark]
    public int Runtime()
    {
        StringSerializer.Serialize(_source, _destination, out _, out int bytes);
        return bytes;
    }

    /// <summary>Serializes the supplied value using the selected format and options.</summary>
    /// <param name="source">The input data to process.</param>
    /// <param name="destination">The destination receiving the result.</param>
    /// <returns>The result of the operation.</returns>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static int SerializeWithoutBmpKernel(string source, byte[] destination)
    {
        Utf8.FromUtf16(source, destination, out _, out int bytes, true, true);
        return bytes;
    }
}
