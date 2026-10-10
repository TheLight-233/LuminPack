using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;

namespace LuminPackBenchmark;

using System;
using System.Buffers;
using System.Text;
using System.Text.Unicode;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using LuminPack.Utility;

public abstract class StringBenchmarkData
{
    [Params(4096)]
    public int Length { get; set; }

    [Params("ASCII", "Latin2", "CJK3", "Emoji4", "Mixed", "CJK_SparseASCII")]
    public string CharacterSet { get; set; } = "ASCII";

    protected string Text = string.Empty;
    protected byte[] Utf8Bytes = Array.Empty<byte>();
    protected byte[] EncodeDestination = Array.Empty<byte>();
    protected char[] DecodeDestination = Array.Empty<char>();

    [GlobalSetup]
    public void Setup()
    {
        Text = CreateInput(CharacterSet, Length);
        Utf8Bytes = Encoding.UTF8.GetBytes(Text);

        // Every valid UTF-16 code unit produces at most 3 UTF-8 bytes.
        // Reuse the exact same buffer for every encoder in each benchmark case.
        EncodeDestination = new byte[checked(Length * 3)];
        DecodeDestination = new char[Length];

        // Correctness is checked OUTSIDE the timed region, including all output bytes.
        OperationStatus status = StringSerializer.Serialize(
            Text.AsSpan(), EncodeDestination.AsSpan(),
            out int charsRead, out int bytesWritten,
            SerializeMode.Utf8, replaceInvalidSequences: true, isFinalBlock: true);

        if (status != OperationStatus.Done || charsRead != Text.Length ||
            bytesWritten != Utf8Bytes.Length ||
            !EncodeDestination.AsSpan(0, bytesWritten).SequenceEqual(Utf8Bytes))
        {
            throw new InvalidOperationException(
                $"StringSerializer.Serialize mismatch: {CharacterSet}, UTF-16 length={Length}, " +
                $"status={status}, charsRead={charsRead}, bytesWritten={bytesWritten}.");
        }

        status = StringSerializer.Deserialize(
            Utf8Bytes.AsSpan(), DecodeDestination.AsSpan(),
            out int bytesRead, out int charsWritten,
            SerializeMode.Utf8, replaceInvalidSequences: true, isFinalBlock: true);

        if (status != OperationStatus.Done || bytesRead != Utf8Bytes.Length ||
            charsWritten != Text.Length ||
            !DecodeDestination.AsSpan(0, charsWritten).SequenceEqual(Text.AsSpan()))
        {
            throw new InvalidOperationException(
                $"StringSerializer.Deserialize mismatch: {CharacterSet}, UTF-16 length={Length}, " +
                $"status={status}, bytesRead={bytesRead}, charsWritten={charsWritten}.");
        }

        // Check the System.Text.Unicode.Utf8 baseline with the very same buffers.
        status = Utf8.FromUtf16(Text.AsSpan(), EncodeDestination.AsSpan(),
            out charsRead, out bytesWritten, replaceInvalidSequences: true, isFinalBlock: true);
        if (status != OperationStatus.Done || charsRead != Length ||
            bytesWritten != Utf8Bytes.Length ||
            !EncodeDestination.AsSpan(0, bytesWritten).SequenceEqual(Utf8Bytes))
        {
            throw new InvalidOperationException("Utf8.FromUtf16 baseline output mismatch.");
        }

        status = Utf8.ToUtf16(Utf8Bytes.AsSpan(), DecodeDestination.AsSpan(),
            out bytesRead, out charsWritten, replaceInvalidSequences: true, isFinalBlock: true);
        if (status != OperationStatus.Done || bytesRead != Utf8Bytes.Length ||
            charsWritten != Length ||
            !DecodeDestination.AsSpan(0, charsWritten).SequenceEqual(Text.AsSpan()))
        {
            throw new InvalidOperationException("Utf8.ToUtf16 baseline output mismatch.");
        }
    }

    private static string CreateInput(string kind, int length)
    {
        if (length < 0) throw new ArgumentOutOfRangeException(nameof(length));

        if (kind == "CJK_SparseASCII")
        {
            // A single ASCII code unit every 65 characters stresses SIMD re-entry.
            char[] chars = new char[length];
            for (int i = 0; i < chars.Length; i++)
                chars[i] = i % 65 == 64 ? 'A' : '\u4E2D';
            return new string(chars);
        }

        string pattern = kind switch
        {
            "ASCII"  => "abcdefghijklmnopqrstuvwxyz0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ",
            "Latin2" => "ПриветмирЖизньДанныеТест", // U+0080..U+07FF -> 2 UTF-8 bytes
            "CJK3"   => "中文世界高性能字符串序列化基准测试", // BMP -> 3 UTF-8 bytes
            "Emoji4" => "😀🚀🎮🔥🧠🌟💡🤖", // surrogate pairs -> 4 UTF-8 bytes
            "Mixed"  => "Hello中文Привет🚀World测试123!",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown character set")
        };

        var sb = new StringBuilder(length);
        while (sb.Length + pattern.Length <= length)
            sb.Append(pattern);

        int remaining = length - sb.Length;
        for (int p = 0; remaining > 0 && p < pattern.Length; p++)
        {
            char c = pattern[p];
            if (char.IsHighSurrogate(c))
            {
                if (p + 1 < pattern.Length && char.IsLowSurrogate(pattern[p + 1]) && remaining >= 2)
                {
                    sb.Append(c);
                    sb.Append(pattern[++p]);
                    remaining -= 2;
                }
                else
                {
                    // Do not create malformed UTF-16 in a throughput benchmark.
                    sb.Append('A');
                    remaining--;
                    if (p + 1 < pattern.Length && char.IsLowSurrogate(pattern[p + 1])) p++;
                }
            }
            else if (!char.IsLowSurrogate(c))
            {
                sb.Append(c);
                remaining--;
            }
        }
        if (remaining != 0)
            sb.Append('A', remaining);

        return sb.ToString();
    }
}

[HideColumns("StdDev", "RatioSD", "Error")]
[MinColumn, MaxColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[ShortRunJob(RuntimeMoniker.Net10_0)]
[MemoryDiagnoser]
[GcServer]
public class StringBenchmark : StringBenchmarkData
{
    // The baseline is the BCL span-based transcoder, not the allocating GetBytes(string).
    [Benchmark(Baseline = true)]
    public int DotNet_Utf8_FromUtf16()
    {
        Utf8.FromUtf16(Text.AsSpan(), EncodeDestination.AsSpan(),
            out _, out int bytesWritten);
        return bytesWritten;
    }

    [Benchmark]
    public int DotNet_Encoding_GetBytes()
        => Encoding.UTF8.GetBytes(Text.AsSpan(), EncodeDestination.AsSpan());

    [Benchmark]
    public int LuminPack_Serialize()
    {
        StringSerializer.Serialize(Text.AsSpan(), EncodeDestination.AsSpan(),
            out _, out int bytesWritten, SerializeMode.Utf8);
        return bytesWritten;
    }
}

[HideColumns("StdDev", "RatioSD", "Error")]
[MinColumn, MaxColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[ShortRunJob(RuntimeMoniker.Net10_0)]
[MemoryDiagnoser]
[GcServer]
public class StringDeserializeBenchmark : StringBenchmarkData
{
    [Benchmark(Baseline = true)]
    public int DotNet_Utf8_ToUtf16()
    {
        Utf8.ToUtf16(Utf8Bytes.AsSpan(), DecodeDestination.AsSpan(),
            out _, out int charsWritten);
        return charsWritten;
    }

    [Benchmark]
    public int DotNet_Encoding_GetChars()
        => Encoding.UTF8.GetChars(Utf8Bytes.AsSpan(), DecodeDestination.AsSpan());

    [Benchmark]
    public int LuminPack_Deserialize()
    {
        StringSerializer.Deserialize(Utf8Bytes.AsSpan(), DecodeDestination.AsSpan(),
            out _, out int charsWritten, SerializeMode.Utf8);
        return charsWritten;
    }
}
