using System.Text;
using LuminPack;
using LuminPack.Fuzzing;
using LuminPack.Utility;

namespace Fuzzing;

/// <summary>Reads a concrete generated model without registration-based generic dispatch.</summary>
/// <param name="input">The encoded input.</param>
/// <param name="value">The fresh or reusable graph.</param>
/// <returns>The number of consumed bytes.</returns>
internal delegate int ReadRecord(ReadOnlySpan<byte> input, ref FuzzRecord value);

/// <summary>Writes a concrete generated model in one target's encoding.</summary>
/// <param name="value">The graph to encode.</param>
/// <returns>The encoded bytes.</returns>
internal delegate byte[] WriteRecord(FuzzRecord? value);

/// <summary>Defines a parser target and the invariants checked after successful parsing.</summary>
internal sealed record FuzzTarget(string Name, ReadRecord Read, WriteRecord Write, bool Json = false, Func<FuzzRecord>? Create = null) : IFuzzTarget
{
    internal const int MaxInputBytes = 16384;

    internal static readonly FuzzTarget[] All =
    [
        new("binary", static (ReadOnlySpan<byte> input, ref FuzzRecord value) => LuminPackSerializer.Deserialize(input, ref value),
            static value => LuminPackSerializer.Serialize(value!)),
        new("binary-utf8-varint", ReadVarInt,
            static value => LuminPackSerializer.Serialize(Copy<FuzzVarIntRecord>(value)!), Create: static () => new FuzzVarIntRecord()),
        new("json-utf8", ReadUtf8Json,
            static value => WriteJson(value, utf16: false), Json: true, Create: static () => new FuzzUtf8Record()),
        new("json-utf16", static (ReadOnlySpan<byte> input, ref FuzzRecord value) => LuminPackSerializer.DeserializeJson(input, ref value),
            static value => WriteJson(value, utf16: true), Json: true)
    ];

    /// <summary>Verifies that a positive seed preserves its source graph, then checks the mutation target invariants.</summary>
    /// <param name="value">The known graph to encode.</param>
    /// <returns>The verified encoded bytes.</returns>
    internal byte[] Seed(FuzzRecord? value)
    {
        byte[] bytes = Write(value);
        FuzzRecord copy = null!;
        Require(Read(bytes, ref copy) == bytes.Length && Equivalent(value, copy), $"Positive seed did not preserve its graph in {Name}.");
        Require(Run(bytes), $"Positive seed was rejected by {Name}.");
        return bytes;
    }

    /// <inheritdoc/>
    public IEnumerable<(string Name, byte[] Bytes)> Seeds()
    {
        foreach (var (name, value) in Corpus.Values())
            yield return (name, Seed(value));
    }

    /// <inheritdoc/>
    public IEnumerable<(string Name, byte[] Bytes)> Malformed()
    {
        var encoding = Name == "json-utf16" ? System.Text.Encoding.Unicode : System.Text.Encoding.UTF8;
        yield return ("invalid", Json ? encoding.GetBytes("{\"Bits\":[true,],\"Text\":\"\\uZZZZ\"}") : [250, 255, 255, 255, 127]);
        if (Json) yield return ("wrong-type", encoding.GetBytes("{\"Text\":17}"));
    }

    /// <inheritdoc/>
    public bool Run(ReadOnlySpan<byte> input)
    {
        if (input.Length > MaxInputBytes) return false;
        FuzzRecord fresh = null!;
        int consumed;
        try
        {
            consumed = Read(input, ref fresh);
        }
        catch (Exception exception) when (IsRejection(exception, Json))
        {
            return false;
        }

        Require(consumed >= 0 && consumed <= input.Length, "The reader reported an invalid consumed length.");
        byte[] canonical = Write(fresh);
        FuzzRecord reused = Create?.Invoke() ?? new FuzzRecord();
        reused.Number = -1;
        reused.Flag = true;
        reused.Stamp = -1;
        reused.Optional = -1;
        reused.Text = "reusable";
        reused.Numbers = [1, 2];
        reused.Bits = [true, false];
        reused.Flags = [true];
        reused.Items = [new FuzzChild { Text = "old" }];
        reused.Map = new() { [-1] = "old" };
        reused.Child = new FuzzChild();
        FuzzRecord original = reused;
        byte[] beforeReuse = Write(reused);
        Require(Read(input, ref reused) == consumed, "Fresh and reuse reads consumed different lengths.");
        // Binary null roots intentionally preserve an existing target; JSON null clears it.
        if (!Json && fresh is null)
            Require(ReferenceEquals(original, reused) && beforeReuse.AsSpan().SequenceEqual(Write(reused)), "A binary null root changed the existing target.");
        else
            Require(Equivalent(fresh, reused), "Fresh and reuse reads produced different values.");

        // Canonical JSON can add omitted members and exceed the original document's input cap.
        if (canonical.Length > MaxInputBytes) return true;
        FuzzRecord roundTrip = null!;
        Require(Read(canonical, ref roundTrip) == canonical.Length, "A canonical record was not fully consumed.");
        Require(canonical.AsSpan().SequenceEqual(Write(roundTrip)), "Canonical reserialization changed the record.");
        return true;
    }

    /// <summary>Compares graph values while respecting unordered dictionary semantics.</summary>
    /// <param name="left">The fresh graph.</param>
    /// <param name="right">The reused graph.</param>
    /// <returns>Whether every serialized value agrees.</returns>
    private static bool Equivalent(FuzzRecord? left, FuzzRecord? right)
    {
        if (left is null || right is null) return left is null && right is null;
        return left.Number == right.Number && left.Flag == right.Flag && left.Stamp == right.Stamp &&
            left.Optional == right.Optional && left.Text == right.Text && Sequence(left.Numbers, right.Numbers) &&
            Sequence(left.Bits, right.Bits) && Sequence(left.Flags, right.Flags) &&
            Sequence(left.Map?.OrderBy(pair => pair.Key), right.Map?.OrderBy(pair => pair.Key)) &&
            ChildEquals(left.Child, right.Child) &&
            ((left.Items is null || right.Items is null) ? left.Items is null && right.Items is null :
                left.Items.Count == right.Items.Count && left.Items.Zip(right.Items).All(pair => ChildEquals(pair.First, pair.Second)));
    }

    /// <summary>Compares nullable sequences without conflating null and empty.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="left">The first sequence.</param>
    /// <param name="right">The second sequence.</param>
    /// <returns>Whether both sequences contain the same values.</returns>
    private static bool Sequence<T>(IEnumerable<T>? left, IEnumerable<T>? right)
        => left is null || right is null ? left is null && right is null : left.SequenceEqual(right);

    /// <summary>Compares the bounded recursive child graphs.</summary>
    /// <param name="left">The first child.</param>
    /// <param name="right">The second child.</param>
    /// <returns>Whether the child values agree.</returns>
    private static bool ChildEquals(FuzzChild? left, FuzzChild? right)
        => left is null || right is null ? left is null && right is null :
            left.Number == right.Number && left.Flag == right.Flag && left.Text == right.Text && ChildEquals(left.Next, right.Next);

    /// <summary>Recognizes the documented parser rejection types without hiding programmer or resource failures.</summary>
    /// <param name="exception">The exception raised during the initial read.</param>
    /// <param name="json">Whether JSON token mismatch errors are expected.</param>
    /// <returns>Whether the input is an ordinary malformed record.</returns>
    internal static bool IsRejection(Exception exception, bool json)
    {
        // A tagged integer outside its declared range keeps the checked conversion's OverflowException.
        if (exception is InvalidDataException or FormatException or DecoderFallbackException or EndOfStreamException or OverflowException)
            return true;
        if (exception.GetType().FullName == "LuminPack.Code.LuminPackExceptionHelper+LuminPackException")
            return true;
        // Dictionary.Add reports duplicate keys as exactly ArgumentException, never a derived bounds exception.
        if (exception.GetType() == typeof(ArgumentException)) return true;
        return json && exception is InvalidOperationException && exception.Message is
            "Current token is not a boolean" or "Expected JSON object" or "Expected JSON array" or "Expected number" or "Expected string" or "Expected string token";
    }

    /// <summary>Fails an invariant outside the malformed-input exception filter.</summary>
    /// <param name="condition">The required invariant.</param>
    /// <param name="message">The failure description.</param>
    internal static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    /// <summary>Writes JSON bytes directly in the selected encoding.</summary>
    /// <param name="value">The graph to encode.</param>
    /// <param name="utf16">Whether to use little-endian UTF-16 instead of UTF-8.</param>
    /// <returns>The encoded JSON without a byte-order mark.</returns>
    private static byte[] WriteJson(FuzzRecord? value, bool utf16)
    {
        using var buffer = new LuminBufferWriter(true);
        if (utf16) LuminPackSerializer.SerializeJson(value!, buffer);
        else LuminPackSerializer.SerializeJson(Copy<FuzzUtf8Record>(value)!, buffer);
        return buffer.GetSpan().ToArray();
    }

    /// <summary>Reads the independently declared UTF-8 VarInt model through its concrete generated overload.</summary>
    /// <param name="input">The binary bytes.</param>
    /// <param name="value">The fresh or reusable target of the correct concrete type.</param>
    /// <returns>The consumed byte count.</returns>
    private static int ReadVarInt(ReadOnlySpan<byte> input, ref FuzzRecord value)
    {
        var typed = (FuzzVarIntRecord)value;
        int consumed = LuminPackSerializer.Deserialize(input, ref typed);
        value = typed;
        return consumed;
    }

    /// <summary>Reads the independently declared UTF-8 JSON model through its concrete generated overload.</summary>
    /// <param name="input">The JSON bytes.</param>
    /// <param name="value">The fresh or reusable target of the correct concrete type.</param>
    /// <returns>The consumed byte count.</returns>
    private static int ReadUtf8Json(ReadOnlySpan<byte> input, ref FuzzRecord value)
    {
        var typed = (FuzzUtf8Record)value;
        int consumed = LuminPackSerializer.DeserializeJson(input, ref typed);
        value = typed;
        return consumed;
    }

    /// <summary>Copies seed values into the declaration-owned target contract without changing nested values.</summary>
    /// <typeparam name="T">The concrete target contract.</typeparam>
    /// <param name="value">The source graph, or null.</param>
    /// <returns>The target model carrying the same serialized values.</returns>
    private static T? Copy<T>(FuzzRecord? value) where T : FuzzRecord, new()
        => value is null ? null : value as T ?? new T
        {
            Number = value.Number, Flag = value.Flag, Stamp = value.Stamp, Optional = value.Optional, Text = value.Text,
            Numbers = value.Numbers, Bits = value.Bits, Flags = value.Flags, Items = value.Items, Map = value.Map, Child = value.Child
        };
}
