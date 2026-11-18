using System.Text;
using Microsoft.CodeAnalysis;

internal static partial class SerializerIssueRegression
{
    /// <summary>Checks the element layout policy through independent consumers and their collection entry points.</summary>
    /// <param name="references">Platform and runtime references.</param>
    internal static void RunNullableCollectionLayouts(MetadataReference[] references)
    {
        const string source = """
            #nullable enable
            using System;
            using System.Buffers;
            using System.Collections.Concurrent;
            using System.Collections.Generic;
            using System.Collections.Immutable;
            using System.Collections.ObjectModel;
            using System.IO;
            using System.Linq;
            using System.Runtime.CompilerServices;
            using System.Runtime.InteropServices;
            using LuminPack;
            using LuminPack.Attribute;
            using LuminPack.Code;
            using LuminPack.Utility;

            ASSEMBLY_DECLARATION

            [LuminPackable]
            CELL_DECLARATION
            public struct Cell { public byte Code; public long Number; }

            [LuminPackable, LuminPackOption(MaxCollectionLength = 2)]
            public sealed class Envelope { public List<Cell?>? Items; }

            [LuminPackable]
            public sealed class NestedOnly { public List<short?>? Items; }

            public static class Entry
            {
                private delegate int Read<T>(ReadOnlySpan<byte> input, ref T value);
                private sealed class Segment<T> : ReadOnlySequenceSegment<T>
                {
                    public Segment(ReadOnlyMemory<T> memory, long start, Segment<T>? next = null)
                    { Memory = memory; RunningIndex = start; Next = next; }
                }

                private sealed class AliasedMemory(Memory<byte> bytes) : MemoryManager<long?>
                {
                    public override Span<long?> GetSpan() => MemoryMarshal.CreateSpan(
                        ref Unsafe.As<byte, long?>(ref MemoryMarshal.GetReference(bytes.Span)), bytes.Length / Unsafe.SizeOf<long?>());

                    public override MemoryHandle Pin(int elementIndex = 0) => throw new NotSupportedException();
                    public override void Unpin() { }
                    protected override void Dispose(bool disposing) { }
                }

                private static readonly byte[] Count = [2, 0, 0, 0];
                private static readonly byte[] QueueHead = [2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2, 0, 0, 0];
                private static readonly byte[] FirstKey = [1, 0, 0, 0];
                private static readonly byte[] SecondKey = [2, 0, 0, 0];

                public static void Check()
                {
                    const long number = 0x0102030405060708;
                    Cell?[] cells = [new Cell { Code = 0xA1, Number = number }, null];
                    byte[] cellFrame = Convert.FromHexString("00A10807060504030201FF");
                    byte[] cellNative = Native(cells);
                    byte[] cellBlock = Block("CELL_LAYOUT", cellFrame, cellNative, naturalStride: 24);
                    byte[] cellElements = "CELL_LAYOUT" == "Native" ? cellNative : cellFrame;

                    int?[] integers = [31, null];
                    byte[] intFrame = Convert.FromHexString("001F000000FF");
                    byte[] intNative = Native(integers);
                    byte[] intBlock = Block("ASSEMBLY_LAYOUT", intFrame, intNative, naturalStride: 0);
                    byte[] intElements = "ASSEMBLY_LAYOUT" == "Packed" ? intFrame : intNative;

                    long?[] wide = [number, null];
                    byte[] longFrame = Convert.FromHexString("000807060504030201FF");
                    byte[] longNative = Native(wide);
                    byte[] longBlock = Block("ASSEMBLY_LAYOUT", longFrame, longNative, naturalStride: 16);
                    byte[] longElements = "ASSEMBLY_LAYOUT" == "Native" ? longNative : longFrame;

                    CASES

                    var list = new List<Cell?>(cells);
                    byte[] root = LuminPackSerializer.Serialize(list);
                    Require(LuminPackSerializer.Serialize(new List<Cell?>()).AsSpan().SequenceEqual(new byte[4]),
                        "An empty nullable list acquired extra framing.");
                    Require(LuminPackSerializer.Serialize((List<Cell?>?)null).AsSpan().SequenceEqual(new byte[] { 255, 255, 255, 255 }),
                        "The null nullable list marker changed.");
                    byte[] envelope = LuminPackSerializer.Serialize(new Envelope { Items = list });
                    Require(envelope.Length == root.Length + 1 && envelope[0] == 1 && envelope.AsSpan(1).SequenceEqual(root),
                        "The nested List<Cell?> did not use its root layout.");
                    Envelope? result = null;
                    Require(LuminPackSerializer.Deserialize(envelope, ref result) == envelope.Length &&
                        result?.Items is { } items && items.SequenceEqual(cells), "The nested List<Cell?> reader used a different layout.");

                    var oversized = new List<Cell?> { cells[0], cells[1], cells[0] };
                    byte[] rejected = LuminPackSerializer.Serialize(new Envelope { Items = oversized });
                    RequireRejected(rejected, (ReadOnlySpan<byte> input, ref Envelope? value) =>
                        LuminPackSerializer.Deserialize(input, ref value), result, "A nested nullable collection bypassed its count limit.");

                    short?[] shorts = [0x1234, null];
                    byte[] shortPayload = "ASSEMBLY_LAYOUT" == "Packed" ? Convert.FromHexString("003412FF") : Native(shorts);
                    byte[] nestedFixture = [1, .. Count, .. shortPayload];
                    byte[] nestedWire = LuminPackSerializer.Serialize(new NestedOnly { Items = new List<short?>(shorts) });
                    Require(nestedWire.Length == nestedFixture.Length && nestedWire[0] == 1 && nestedWire.AsSpan(1, 4).SequenceEqual(Count),
                        "A nullable collection reached only through a member escaped the layout policy.");
                    NestedOnly? nested = null;
                    Require(LuminPackSerializer.Deserialize(nestedFixture, ref nested) == nestedFixture.Length &&
                        nested?.Items is { } series && series.SequenceEqual(shorts), "The member-only nullable layout fixture was not read.");

                    using var append = new LuminBufferWriter(true);
                    LuminPackSerializer.Serialize(list, append);
                    LuminPackSerializer.Serialize(list, append);
                    Require(append.GetSpan().SequenceEqual(root.Concat(root).ToArray()), "The retained writer changed either nullable list or its boundary.");

                    if ("ASSEMBLY_LAYOUT" != "Packed")
                    {
                        using var destination = new LuminBufferWriter(true);
                        using var source = new AliasedMemory(((IBufferWriter<byte>)destination).GetMemory(2 * Unsafe.SizeOf<long?>())[..(2 * Unsafe.SizeOf<long?>())]);
                        source.GetSpan().Clear();
                        source.GetSpan()[0] = number;
                        ReadOnlyMemory<long?> memory = source.Memory;
                        byte[] unaliased = LuminPackSerializer.Serialize(memory);
                        LuminPackSerializer.Serialize(memory, destination);
                        Require(destination.GetSpan().SequenceEqual(unaliased), "Writing a count or tag clobbered the aliased nullable data.");
                    }

                    if ("CELL_LAYOUT" == "Tagged")
                    {
                        // The second permitted ABI aligns both the value and Int64 to a four-byte boundary.
                        byte[] foreign = Convert.FromHexString("020000000201000000A1000000080706050403020100000000000000000000000000000000");
                        List<Cell?>? imported = null;
                        Require(LuminPackSerializer.Deserialize(foreign, ref imported) == foreign.Length &&
                            imported is not null && imported.SequenceEqual(cells), "The untagged local layout displaced the foreign tagged model.");
                        foreign[4] = 254;
                        RequireRejected(foreign, (ReadOnlySpan<byte> input, ref List<Cell?>? value) =>
                            LuminPackSerializer.Deserialize(input, ref value), imported, "An unknown nullable layout tag was accepted.");
                    }
                }

                private static byte[] Native<T>(T?[] input) where T : unmanaged =>
                    MemoryMarshal.CreateReadOnlySpan(ref Unsafe.As<T?, byte>(ref input[0]), input.Length * Unsafe.SizeOf<T?>()).ToArray();

                private static byte[] Block(string layout, byte[] framed, byte[] native, int naturalStride)
                {
                    if (layout == "Packed")
                        return framed;
                    if (layout == "Native" || naturalStride == 0)
                        return native;
                    byte tag = (byte)(native.Length / 2 == naturalStride ? 1 : 2);
                    return [tag, .. native];
                }

                private static byte[][] EntryBytes(byte[] values, bool native)
                {
                    int width = native ? values.Length / 2 : values.Length - 1;
                    return [values[..width], values[width..]];
                }

                private static void CheckWire<TSequence, T>(string family, TSequence input, T?[] values, byte[] head, byte[] payload,
                    Func<TSequence, byte[]> write, Read<TSequence> read, Func<TSequence, IEnumerable<T?>> items, bool exact, int presence = -1)
                    where T : unmanaged
                {
                    byte[] expected = [.. head, .. payload];
                    byte[] wire = write(input);
                    Require(wire.Length == expected.Length && wire.AsSpan(0, head.Length).SequenceEqual(head),
                        $"{family} in ASSEMBLY_LAYOUT/CELL_LAYOUT wrote {Convert.ToHexString(wire)} with expected width {expected.Length} and prefix {Convert.ToHexString(head)}.");
                    if (exact)
                        Require(wire.AsSpan().SequenceEqual(expected), family + " changed its packed bytes.");
                    if (presence == 1)
                        Require(wire[head.Length] == payload[0], family + " changed its tagged model byte.");
                    byte[] combined = [.. expected, .. expected];
                    TSequence result = default!;
                    int consumed = read(combined, ref result);
                    Require(consumed == expected.Length && items(result).SequenceEqual(values), family + " did not read the independent fixture or preserve its boundary.");
                    result = default!;
                    Require(read(wire, ref result) == wire.Length && items(result).SequenceEqual(values), family + " did not round trip.");
                    if (presence >= 0)
                    {
                        RequireRejected(expected.AsSpan(0, expected.Length - 1), read, result, family + " accepted a truncated nullable block.");
                        expected[head.Length + presence] = 2;
                        RequireRejected(expected, read, result, family + " accepted a noncanonical presence byte.");
                        Require(items(result).SequenceEqual(values), family + " changed the retained items after invalid input.");
                    }
                }

                private static IEnumerable<T?> Iterate<T>(T?[] items) where T : unmanaged
                {
                    foreach (T? item in items)
                        yield return item;
                }

                private static ReadOnlySequence<T> Segmented<T>(T[] items)
                {
                    var last = new Segment<T>(items.AsMemory(1), 1);
                    var first = new Segment<T>(items.AsMemory(0, 1), 0, last);
                    return new ReadOnlySequence<T>(first, 0, last, 1);
                }

                private static void RequireRejected<T>(ReadOnlySpan<byte> input, Read<T> read, T prior, string message)
                {
                    T value = prior;
                    try
                    {
                        read(input, ref value);
                    }
                    catch (Exception error) when (error is InvalidDataException or FormatException ||
                        error.GetType().DeclaringType == typeof(LuminPackExceptionHelper))
                    {
                        if (!typeof(T).IsValueType)
                            Require(ReferenceEquals(value, prior), message + " It also published the failed value.");
                        return;
                    }
                    throw new InvalidOperationException(message);
                }

                private static void Require(bool condition, string message)
                {
                    if (!condition)
                        throw new InvalidOperationException(message);
                }
            }
            """;
        foreach (var (options, declaration, implicitLayout) in new[]
        {
            ("Tagged", "Packed", false), ("Packed", "Native", false), ("Native", "Tagged", false), ("Tagged", "Tagged", true)
        })
        {
            string consumer = source.Replace("CASES", NullableCollectionLayoutCases(), StringComparison.Ordinal)
                .Replace("ASSEMBLY_DECLARATION", implicitLayout ? "" : $"[assembly: LuminPackOption(StructLayout = LuminPackStructLayout.{options})]", StringComparison.Ordinal)
                .Replace("CELL_DECLARATION", implicitLayout ? "" : $"[LuminPackOption(StructLayout = LuminPackStructLayout.{declaration})]", StringComparison.Ordinal)
                .Replace("ASSEMBLY_LAYOUT", options, StringComparison.Ordinal)
                .Replace("CELL_LAYOUT", declaration, StringComparison.Ordinal);
            Execute(references, consumer, "NullableCollectionLayouts" + (implicitLayout ? "Implicit" : options));
        }
        Console.WriteLine("Passed unified nullable collection layout fixtures, checked flags, nested graphs and retained writes.");
    }

    /// <summary>Specifies matching concrete serializer calls for each independently framed collection.</summary>
    /// <returns>The checks inserted in each generated consumer.</returns>
    private static string NullableCollectionLayoutCases()
    {
        var result = new StringBuilder();
        AddCases("Cell", "cells", "cell", "CELL_LAYOUT", wide: true);
        AddCases("int", "integers", "int", "ASSEMBLY_LAYOUT", wide: false);
        AddCases("long", "wide", "long", "ASSEMBLY_LAYOUT", wide: true);
        return result.ToString();

        void AddCases(string element, string values, string fixture, string layout, bool wide)
        {
            string mode = "\"" + layout + "\"";
            string optional = element + "?";
            string blockPresence = $"{mode} == \"Packed\" ? -1 : {(wide ? $"{mode} == \"Tagged\" ? 1 : 0" : "0")}";
            Add("Vector", optional + "[]", values, "Count", fixture + "Block", "items", blockPresence);
            Add("List", $"List<{optional}>?", $"new List<{optional}>({values})", "Count", fixture + "Block", "items!", blockPresence);
            Add("Memory", $"Memory<{optional}>", $"new Memory<{optional}>({values})", "Count", fixture + "Block", "items.ToArray()", blockPresence);
            Add("ReadOnlyMemory", $"ReadOnlyMemory<{optional}>", $"new ReadOnlyMemory<{optional}>({values})", "Count", fixture + "Block", "items.ToArray()", blockPresence);
            Add("ImmutableArray", $"ImmutableArray<{optional}>", $"ImmutableArray.CreateRange({values})", "Count", fixture + "Block", "items", blockPresence);
            Add("ArraySegment", $"ArraySegment<{optional}>", $"new ArraySegment<{optional}>([null, .. {values}, null], 1, 2)", "Count", fixture + "Block", "items", blockPresence);
            Add("Collection", $"Collection<{optional}>?", $"new Collection<{optional}>(new List<{optional}>({values}))", "Count", fixture + "Block", "items!", blockPresence);
            Add("ObservableCollection", $"ObservableCollection<{optional}>?", $"new ObservableCollection<{optional}>({values})", "Count", fixture + "Block", "items!", blockPresence);
            Add("ReadOnlyCollection", $"ReadOnlyCollection<{optional}>?", $"new ReadOnlyCollection<{optional}>(new List<{optional}>({values}))", "Count", fixture + "Block", "items!", blockPresence);
            Add("ReadOnlyObservableCollection", $"ReadOnlyObservableCollection<{optional}>?", $"new ReadOnlyObservableCollection<{optional}>(new ObservableCollection<{optional}>({values}))", "Count", fixture + "Block", "items!", blockPresence);
            Add("Queue", $"Queue<{optional}>?", $"new Queue<{optional}>({values})", "QueueHead", fixture + "Elements", "items!");
            Add("ConcurrentQueue", $"ConcurrentQueue<{optional}>?", $"new ConcurrentQueue<{optional}>({values})", "Count", fixture + "Elements", "items!");
            Add("HashSet", $"HashSet<{optional}>?", $"new HashSet<{optional}>({values})", "Count", fixture + "Elements", "items!");
            Add("Enumerable/List", $"IEnumerable<{optional}>?", $"new List<{optional}>({values})", "Count", fixture + "Elements", "items!");
            Add("Enumerable/Iterator", $"IEnumerable<{optional}>?", $"Iterate({values})", "Count", fixture + "Elements", "items!");
            Add("IList/List", $"IList<{optional}>?", $"new List<{optional}>({values})", "Count", fixture + "Elements", "items!");
            Add("ReadOnlySequence", $"ReadOnlySequence<{optional}>", $"Segmented({values})", "Count", fixture + "Elements", "items.ToArray()");

            string entries = $"{fixture}Entries";
            string nativeElements = wide ? $"{mode} == \"Native\"" : $"{mode} != \"Packed\"";
            result.AppendLine($"byte[][] {entries} = EntryBytes({fixture}Elements, {nativeElements});");
            Add("Dictionary", $"Dictionary<int, {optional}>?", $"new Dictionary<int, {optional}> {{ [1] = {values}[0], [2] = {values}[1] }}",
                "Count", $"[.. FirstKey, .. {entries}[0], .. SecondKey, .. {entries}[1]]", "items!.Values");
            Add("SortedList", $"SortedList<int, {optional}>?", $"new SortedList<int, {optional}> {{ [1] = {values}[0], [2] = {values}[1] }}",
                "Count", $"[.. FirstKey, .. {entries}[0], .. SecondKey, .. {entries}[1]]", "items!.Values");

            void Add(string name, string type, string creation, string head, string payload, string items, string presence = "-1") =>
                result.AppendLine($"""
                    CheckWire<{type}, {element}>("{name}<{element}?>", {creation}, {values}, {head}, {payload},
                        static value => LuminPackSerializer.Serialize(value),
                        static (ReadOnlySpan<byte> input, ref {type} value) => LuminPackSerializer.Deserialize(input, ref value),
                        static items => {items}, {mode} == "Packed", {presence});
                    """);
        }
    }
}
