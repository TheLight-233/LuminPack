using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;

namespace LuminPackBenchmark;

using System;
using System.Buffers;
using System.Text;
using System.Text.Unicode;
using BenchmarkDotNet.Attributes;
using LuminPack.Utility;

/// <summary>Provides string benchmark data functionality.</summary>
public abstract class StringBenchmarkData
{
    /// <summary>Gets or sets the length.</summary>
    [Params(4096)]
    public int Length { get; set; }

    /// <summary>Gets or sets the character set.</summary>
    [Params("ASCII", "Latin2", "CJK3", "Emoji4", "Mixed", "CJK_SparseASCII")]
    public string CharacterSet { get; set; } = "ASCII";

    /// <summary>Stores the text.</summary>
    protected string Text = string.Empty;
    /// <summary>Stores the utf8 bytes.</summary>
    protected byte[] Utf8Bytes = Array.Empty<byte>();
    /// <summary>Stores the encode destination.</summary>
    protected byte[] EncodeDestination = Array.Empty<byte>();
    /// <summary>Stores the decode destination.</summary>
    protected char[] DecodeDestination = Array.Empty<char>();

    /// <summary>Updates the up used by the current operation.</summary>
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

    /// <summary>Creates input from the supplied input.</summary>
    /// <param name="kind">The kind used by this operation.</param>
    /// <param name="length">The number of bytes or elements to process.</param>
    /// <returns>The result of the operation.</returns>
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
            "ASCII" => "abcdefghijklmnopqrstuvwxyz0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ",
            "Latin2" => "ПриветмирЖизньДанныеТест", // U+0080..U+07FF -> 2 UTF-8 bytes
            "CJK3" => "\u4E2D\u6587\u4E16\u754C\u9AD8\u6027\u80FD\u5B57\u7B26\u4E32\u5E8F\u5217\u5316\u57FA\u51C6\u6D4B\u8BD5", // BMP -> 3 UTF-8 bytes
            "Emoji4" => "😀🚀🎮🔥🧠🌟💡🤖", // surrogate pairs -> 4 UTF-8 bytes
            "Mixed" => "Hello\u4E2D\u6587\u041F\u0440\u0438\u0432\u0435\u0442\uD83D\uDE80World\u6D4B\u8BD5123!",
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

/// <summary>Provides string benchmark functionality.</summary>
[HideColumns("StdDev", "RatioSD", "Error")]
[MinColumn, MaxColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[ShortRunJob(RuntimeMoniker.Net10_0)]
[MemoryDiagnoser]
[GcServer]
public class StringBenchmark : StringBenchmarkData
{
    // The baseline is the BCL span-based transcoder, not the allocating GetBytes(string).
    /// <summary>Measures dot net utf8 from utf16 for the configured benchmark input.</summary>
    /// <returns>The result of the operation.</returns>
    [Benchmark(Baseline = true)]
    public int DotNet_Utf8_FromUtf16()
    {
        Utf8.FromUtf16(Text.AsSpan(), EncodeDestination.AsSpan(),
            out _, out int bytesWritten);
        return bytesWritten;
    }

    /// <summary>Measures dot net encoding get bytes for the configured benchmark input.</summary>
    /// <returns>The result of the operation.</returns>
    [Benchmark]
    public int DotNet_Encoding_GetBytes()
        => Encoding.UTF8.GetBytes(Text.AsSpan(), EncodeDestination.AsSpan());

    /// <summary>Measures lumin pack serialize for the configured benchmark input.</summary>
    /// <returns>The result of the operation.</returns>
    [Benchmark]
    public int LuminPack_Serialize()
    {
        StringSerializer.Serialize(Text.AsSpan(), EncodeDestination.AsSpan(),
            out _, out int bytesWritten, SerializeMode.Utf8);
        return bytesWritten;
    }
}

/// <summary>Provides string deserialize benchmark functionality.</summary>
[HideColumns("StdDev", "RatioSD", "Error")]
[MinColumn, MaxColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[ShortRunJob(RuntimeMoniker.Net10_0)]
[MemoryDiagnoser]
[GcServer]
public class StringDeserializeBenchmark : StringBenchmarkData
{
    /// <summary>Measures dot net utf8 to utf16 for the configured benchmark input.</summary>
    /// <returns>The result of the operation.</returns>
    [Benchmark(Baseline = true)]
    public int DotNet_Utf8_ToUtf16()
    {
        Utf8.ToUtf16(Utf8Bytes.AsSpan(), DecodeDestination.AsSpan(),
            out _, out int charsWritten);
        return charsWritten;
    }

    /// <summary>Measures dot net encoding get chars for the configured benchmark input.</summary>
    /// <returns>The result of the operation.</returns>
    [Benchmark]
    public int DotNet_Encoding_GetChars()
        => Encoding.UTF8.GetChars(Utf8Bytes.AsSpan(), DecodeDestination.AsSpan());

    /// <summary>Measures lumin pack deserialize for the configured benchmark input.</summary>
    /// <returns>The result of the operation.</returns>
    [Benchmark]
    public int LuminPack_Deserialize()
    {
        StringSerializer.Deserialize(Utf8Bytes.AsSpan(), DecodeDestination.AsSpan(),
            out _, out int charsWritten, SerializeMode.Utf8);
        return charsWritten;
    }
}
