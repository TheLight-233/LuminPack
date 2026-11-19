using System.Buffers;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using LuminPack.Code;
using LuminPack.Option;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Exercises registration snapshots inside the isolated registry worker.</summary>
internal static class FormatterRegistryCacheTest
{
    /// <summary>Creates an observable registration token for publication and invalidation checks.</summary>
    /// <param name="value">The token identity.</param>
    /// <returns>A delegate that preserves the token.</returns>
    private static Delegate Callback(nint value) => (Func<nint>)(() => value);

    /// <summary>Checks late registration, invalidation, complete publication and collectible payload lifetime.</summary>
    internal static void Run()
    {
        Assert.False(LuminPackFormatterRegistry<Profile>.TryGetCachedValue<Payload>(out _));
        LuminPackFormatterRegistry.Register(typeof(Payload), Callback(1), Callback(2), Callback(3), Callback(4));
        Assert.True(LuminPackFormatterRegistry<Profile>.TryGetCachedValue<Payload>(out var entry));
        CheckEntry(entry, 0);
        Assert.True(LuminPackFormatterRegistry<Profile>.TryGetCachedJsonValue<Payload>(out entry));
        CheckEntry(entry, 0);

        LuminPackFormatterRegistry<Profile>.Register(typeof(Payload), Callback(11), Callback(12), Callback(13), Callback(14));
        Assert.True(LuminPackFormatterRegistry<Profile>.TryGetCachedValue<Payload>(out entry));
        CheckEntry(entry, 10);
        Assert.True(LuminPackFormatterRegistry<EquivalentProfile>.TryGetCachedValue<Payload>(out entry));
        CheckEntry(entry, 10);
        Assert.True(LuminPackFormatterRegistry<Utf8Profile>.TryGetCachedValue<Payload>(out entry));
        CheckEntry(entry, 0);
        Assert.False(LuminPackFormatterRegistry<Utf8Profile>.TryGetCachedJsonValue<Payload>(out _));
        LuminPackFormatterRegistry<Utf8Profile>.Register(typeof(Payload), Callback(21), Callback(22), Callback(23), Callback(24));
        Assert.True(LuminPackFormatterRegistry<Utf8Profile>.TryGetCachedJsonValue<Payload>(out entry));
        CheckEntry(entry, 20);

        Assert.Throws<ArgumentException>(() => LuminPackFormatterRegistry<Profile>.Register(typeof(Payload), Callback(31), Callback(32)));
        Assert.True(LuminPackFormatterRegistry<Profile>.TryGetCachedValue<Payload>(out entry));
        CheckEntry(entry, 10);
        CheckConcurrentInvalidation();

        LuminPackFormatterRegistry.Register(typeof(ColdMarker), Callback(101), Callback(102));
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        Assert.True(LuminPackFormatterRegistry<Profile>.TryGetCachedValue<Payload>(out entry));
        Assert.True(GC.GetAllocatedBytesForCurrentThread() > allocated);
        for (int index = 0; index < 100_000; index++) LuminPackFormatterRegistry<Profile>.TryGetCachedValue<Payload>(out entry);
        allocated = GC.GetAllocatedBytesForCurrentThread();
        for (int index = 0; index < 100_000; index++) LuminPackFormatterRegistry<Profile>.TryGetCachedValue<Payload>(out entry);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - allocated);

        WeakReference context = CheckCollectible();
        for (int index = 0; index < 30 && context.IsAlive; index++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            Thread.Sleep(10);
        }
        Assert.False(context.IsAlive, "A registered snapshot retained its collectible payload context.");
    }

    /// <summary>Checks that all four delegate tokens come from one immutable registration result.</summary>
    /// <param name="entry">The observed entry.</param>
    /// <param name="basis">The common token prefix.</param>
    private static void CheckEntry(LuminPackFormatter entry, int basis)
    {
        Assert.Equal((nint)(basis + 1), ((Func<nint>)entry.WriteValue!)());
        Assert.Equal((nint)(basis + 2), ((Func<nint>)entry.ReadValue!)());
        Assert.Equal((nint)(basis + 3), ((Func<nint>)entry.WriteValueJson!)());
        Assert.Equal((nint)(basis + 4), ((Func<nint>)entry.ReadValueJson!)());
    }

    /// <summary>Invalidates warm snapshots while multiple threads read complete binary and JSON entries.</summary>
    private static void CheckConcurrentInvalidation()
    {
        Type[] arguments = [typeof(byte), typeof(short), typeof(int), typeof(long), typeof(string), typeof(Guid), typeof(object), typeof(DateTime)];
        var payloads = arguments.SelectMany(left => arguments.Select(right => typeof(Other<,>).MakeGenericType(left, right))).ToArray();
        using var ready = new CountdownEvent(3);
        using var start = new ManualResetEventSlim();
        int stopped = 0;
        Exception? failure = null;
        var readers = Enumerable.Range(0, 3).Select(readerIndex => new Thread(() =>
        {
            try
            {
                Assert.True(LuminPackFormatterRegistry<Profile>.TryGetCachedValue<Payload>(out _));
                ready.Signal();
                start.Wait();
                do
                {
                    Assert.True(LuminPackFormatterRegistry<Profile>.TryGetCachedValue<Payload>(out var entry));
                    CheckEntry(entry, 10);
                    Assert.True(LuminPackFormatterRegistry<Profile>.TryGetCachedJsonValue<Payload>(out entry));
                    CheckEntry(entry, 10);
                } while (Volatile.Read(ref stopped) == 0);
            }
            catch (Exception exception) { Interlocked.CompareExchange(ref failure, exception, null); }
        }) { IsBackground = true }).ToArray();
        foreach (var reader in readers) reader.Start();
        Assert.True(ready.Wait(10_000));
        start.Set();
        try
        {
            foreach (Type payload in payloads)
            {
                LuminPackFormatterRegistry.Register(payload, Callback(201), Callback(202));
                Thread.Yield();
            }
        }
        finally
        {
            Volatile.Write(ref stopped, 1);
            foreach (var reader in readers) Assert.True(reader.Join(10_000));
        }
        Assert.Null(failure);
        GC.KeepAlive(payloads);
    }

    /// <summary>Warms the typed snapshot for a payload loaded in a disposable context.</summary>
    /// <returns>A weak reference that must not survive collection.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference CheckCollectible()
    {
        var context = new AssemblyLoadContext("RegistryCachePayload", isCollectible: true);
        Type payload = context.LoadFromAssemblyPath(typeof(FormatterRegistryCacheTest).Assembly.Location)
            .GetType(typeof(CollectiblePayload).FullName!, throwOnError: true)!;
        var callback = payload.GetMethod(nameof(CollectiblePayload.Callback), BindingFlags.Public | BindingFlags.Static)!
            .CreateDelegate(typeof(Func<nint>));
        LuminPackFormatterRegistry.Register(payload, callback, callback);
        MethodInfo method = typeof(LuminPackFormatterRegistry<Profile>).GetMethod("TryGetCachedValue")!.MakeGenericMethod(payload);
        Assert.True((bool)method.Invoke(null, [null])!);
        var weak = new WeakReference(context);
        context.Unload();
        return weak;
    }

    /// <summary>Supplies a unique key for late profile registration.</summary>
    private sealed class Payload { }
    /// <summary>Supplies a final invalidation before the allocation assertion.</summary>
    private sealed class ColdMarker { }
    /// <summary>Supplies a payload owned by a collectible assembly.</summary>
    private sealed class CollectiblePayload
    {
        /// <summary>Supplies a delegate whose code belongs to the collectible payload assembly.</summary>
        /// <returns>A stable observable value.</returns>
        public static nint Callback() => 301;
    }
    /// <summary>Supplies distinct keys for concurrent registration.</summary>
    /// <typeparam name="TLeft">The first identity component.</typeparam>
    /// <typeparam name="TRight">The second identity component.</typeparam>
    private sealed class Other<TLeft, TRight> { }

    /// <summary>Provides one distinct complete profile for the snapshot checks.</summary>
    private readonly struct Profile : ILuminPackOptions
    {
        /// <inheritdoc/>
        public int RecordBound(string? value) =>
            default(global::LuminPack.Option.LuminPackUtf16LengthStrings).RecordBound(value);

        /// <inheritdoc/>
        public ref byte WriteRecord(scoped ref global::LuminPack.Core.LuminPackWriter writer, ref byte cursor, string? value) =>
            ref default(global::LuminPack.Option.LuminPackUtf16LengthStrings).WriteRecord(ref writer, ref cursor, value);

        /// <inheritdoc/>
        public void Write(scoped ref global::LuminPack.Core.LuminPackWriter writer, string? value) =>
            default(global::LuminPack.Option.LuminPackUtf16LengthStrings).Write(ref writer, value);

        /// <inheritdoc/>
        public string? Read(scoped ref global::LuminPack.Core.LuminPackReader reader, string? current) =>
            default(global::LuminPack.Option.LuminPackUtf16LengthStrings).Read(ref reader, current);

        /// <inheritdoc/>
        public void ReadPair(scoped ref global::LuminPack.Core.LuminPackReader reader, scoped ref string? first, scoped ref string? second) =>
            default(global::LuminPack.Option.LuminPackUtf16LengthStrings).ReadPair(ref reader, ref first, ref second);

        /// <inheritdoc/>
        public void Skip(scoped ref global::LuminPack.Core.LuminPackReader reader, int maxStringLength) =>
            default(global::LuminPack.Option.LuminPackUtf16LengthStrings).Skip(ref reader, maxStringLength);

        /// <inheritdoc/>
        public void ReadSlots(scoped ref global::LuminPack.Core.LuminPackReader reader, scoped global::System.Span<string?> slots) =>
            default(global::LuminPack.Option.LuminPackUtf16LengthStrings).ReadSlots(ref reader, slots);

        /// <inheritdoc/>
        public global::System.Buffers.OperationStatus Encode(global::System.ReadOnlySpan<char> source, global::System.Span<byte> destination, out int charsRead, out int bytesWritten, bool replaceInvalidSequences, bool isFinalBlock) =>
            default(global::LuminPack.Option.LuminPackUtf16LengthStrings).Encode(source, destination, out charsRead, out bytesWritten, replaceInvalidSequences, isFinalBlock);

        /// <inheritdoc/>
        public int Measure(string? value) =>
            default(global::LuminPack.Option.LuminPackUtf16LengthStrings).Measure(value);

        /// <inheritdoc/>
        public void WriteReserved(scoped ref global::LuminPack.Core.LuminPackWriter writer, string? value) =>
            default(global::LuminPack.Option.LuminPackUtf16LengthStrings).WriteReserved(ref writer, value);

        /// <inheritdoc/>
        public void WriteText(scoped ref global::LuminPack.Core.LuminPackWriter writer, scoped global::System.ReadOnlySpan<char> text) =>
            default(global::LuminPack.Option.LuminPackUtf16LengthStrings).WriteText(ref writer, text);

        /// <inheritdoc/>
        public global::System.Buffers.OperationStatus Decode(global::System.ReadOnlySpan<byte> source, global::System.Span<char> destination, out int bytesRead, out int charsWritten, bool replaceInvalidSequences, bool isFinalBlock) =>
            default(global::LuminPack.Option.LuminPackUtf16LengthStrings).Decode(source, destination, out bytesRead, out charsWritten, replaceInvalidSequences, isFinalBlock);

        /// <inheritdoc/>
        public bool SupportsUtf16Runs() =>
            default(global::LuminPack.Option.LuminPackUtf16LengthStrings).SupportsUtf16Runs();

        /// <inheritdoc/>
        public void WriteSlots(scoped ref global::LuminPack.Core.LuminPackWriter writer, scoped global::System.ReadOnlySpan<string?> values) =>
            default(global::LuminPack.Option.LuminPackUtf16LengthStrings).WriteSlots(ref writer, values);

        /// <inheritdoc/>
        public void WriteCheckedSlots(scoped ref global::LuminPack.Core.LuminPackWriter writer, scoped global::System.ReadOnlySpan<string?> values) =>
            default(global::LuminPack.Option.LuminPackUtf16LengthStrings).WriteCheckedSlots(ref writer, values);

        /// <inheritdoc/>
        public void WritePairs(scoped ref global::LuminPack.Core.LuminPackWriter writer, scoped global::System.ReadOnlySpan<string?> keys, scoped global::System.ReadOnlySpan<string?> values) =>
            default(global::LuminPack.Option.LuminPackUtf16LengthStrings).WritePairs(ref writer, keys, values);

        /// <inheritdoc/>
        public void WritePairRuns(scoped ref global::LuminPack.Core.LuminPackWriter writer, scoped global::System.ReadOnlySpan<string?> keys, scoped global::System.ReadOnlySpan<string?> values, bool allowRuns) =>
            default(global::LuminPack.Option.LuminPackUtf16LengthStrings).WritePairRuns(ref writer, keys, values, allowRuns);

        /// <inheritdoc/>
        public ref byte WriteAtCursor(scoped ref global::LuminPack.Core.LuminPackWriter writer, ref byte cursor, string? value) =>
            ref default(global::LuminPack.Option.LuminPackUtf16LengthStrings).WriteAtCursor(ref writer, ref cursor, value);

        /// <inheritdoc/>
        public int WriteSlotPrefix(scoped ref global::LuminPack.Core.LuminPackWriter writer, scoped global::System.ReadOnlySpan<string?> values) =>
            default(global::LuminPack.Option.LuminPackUtf16LengthStrings).WriteSlotPrefix(ref writer, values);

        /// <inheritdoc/>
        public int WritePairPrefix(scoped ref global::LuminPack.Core.LuminPackWriter writer, scoped global::System.ReadOnlySpan<global::System.Collections.Generic.KeyValuePair<string, string?>> pairs) =>
            default(global::LuminPack.Option.LuminPackUtf16LengthStrings).WritePairPrefix(ref writer, pairs);

        /// <inheritdoc/>
        public bool TryPeekCharacterCount(scoped ref global::LuminPack.Core.LuminPackReader reader, out int count) =>
            default(global::LuminPack.Option.LuminPackUtf16LengthStrings).TryPeekCharacterCount(ref reader, out count);

        /// <inheritdoc/>
        public bool TryPeekUtf16Payload(scoped ref global::LuminPack.Core.LuminPackReader reader, int length, out int bytes, out bool latin1) =>
            default(global::LuminPack.Option.LuminPackUtf16LengthStrings).TryPeekUtf16Payload(ref reader, length, out bytes, out latin1);

        /// <inheritdoc/>
        public global::LuminPack.Option.LuminPackStringEncoding Encoding => default(global::LuminPack.Option.LuminPackUtf16LengthStrings).Encoding;

        /// <inheritdoc/>
        public global::LuminPack.Option.LuminPackStringRecording Recording => default(global::LuminPack.Option.LuminPackUtf16LengthStrings).Recording;

        /// <inheritdoc/>
        public byte BinaryProfile => default(global::LuminPack.Option.LuminPackUtf16LengthStrings).BinaryProfile;

        /// <inheritdoc/>
        public LuminPackStringEncoding StringEncoding => LuminPackStringEncoding.UTF16;
        /// <inheritdoc/>
        public LuminPackStringRecording StringRecording => LuminPackStringRecording.Length;
        /// <inheritdoc/>
        public StandardFormat StandardFormat => new('G');
        /// <inheritdoc/>
        public int MaxJsonDepth => 31417;
        /// <inheritdoc/>
        public bool ValidateJsonSyntax => false;
    }

    /// <summary>Uses the same constants through a different CLR profile identity.</summary>
    private readonly struct EquivalentProfile : ILuminPackOptions
    {
        /// <inheritdoc/>
        public int RecordBound(string? value) =>
            default(global::LuminPack.Option.LuminPackUtf16LengthStrings).RecordBound(value);

        /// <inheritdoc/>
        public ref byte WriteRecord(scoped ref global::LuminPack.Core.LuminPackWriter writer, ref byte cursor, string? value) =>
            ref default(global::LuminPack.Option.LuminPackUtf16LengthStrings).WriteRecord(ref writer, ref cursor, value);

        /// <inheritdoc/>
        public void Write(scoped ref global::LuminPack.Core.LuminPackWriter writer, string? value) =>
            default(global::LuminPack.Option.LuminPackUtf16LengthStrings).Write(ref writer, value);

        /// <inheritdoc/>
        public string? Read(scoped ref global::LuminPack.Core.LuminPackReader reader, string? current) =>
            default(global::LuminPack.Option.LuminPackUtf16LengthStrings).Read(ref reader, current);

        /// <inheritdoc/>
        public void ReadPair(scoped ref global::LuminPack.Core.LuminPackReader reader, scoped ref string? first, scoped ref string? second) =>
            default(global::LuminPack.Option.LuminPackUtf16LengthStrings).ReadPair(ref reader, ref first, ref second);

        /// <inheritdoc/>
        public void Skip(scoped ref global::LuminPack.Core.LuminPackReader reader, int maxStringLength) =>
            default(global::LuminPack.Option.LuminPackUtf16LengthStrings).Skip(ref reader, maxStringLength);

        /// <inheritdoc/>
        public void ReadSlots(scoped ref global::LuminPack.Core.LuminPackReader reader, scoped global::System.Span<string?> slots) =>
            default(global::LuminPack.Option.LuminPackUtf16LengthStrings).ReadSlots(ref reader, slots);

        /// <inheritdoc/>
        public global::System.Buffers.OperationStatus Encode(global::System.ReadOnlySpan<char> source, global::System.Span<byte> destination, out int charsRead, out int bytesWritten, bool replaceInvalidSequences, bool isFinalBlock) =>
            default(global::LuminPack.Option.LuminPackUtf16LengthStrings).Encode(source, destination, out charsRead, out bytesWritten, replaceInvalidSequences, isFinalBlock);

        /// <inheritdoc/>
        public int Measure(string? value) =>
            default(global::LuminPack.Option.LuminPackUtf16LengthStrings).Measure(value);

        /// <inheritdoc/>
        public void WriteReserved(scoped ref global::LuminPack.Core.LuminPackWriter writer, string? value) =>
            default(global::LuminPack.Option.LuminPackUtf16LengthStrings).WriteReserved(ref writer, value);

        /// <inheritdoc/>
        public void WriteText(scoped ref global::LuminPack.Core.LuminPackWriter writer, scoped global::System.ReadOnlySpan<char> text) =>
            default(global::LuminPack.Option.LuminPackUtf16LengthStrings).WriteText(ref writer, text);

        /// <inheritdoc/>
        public global::System.Buffers.OperationStatus Decode(global::System.ReadOnlySpan<byte> source, global::System.Span<char> destination, out int bytesRead, out int charsWritten, bool replaceInvalidSequences, bool isFinalBlock) =>
            default(global::LuminPack.Option.LuminPackUtf16LengthStrings).Decode(source, destination, out bytesRead, out charsWritten, replaceInvalidSequences, isFinalBlock);

        /// <inheritdoc/>
        public bool SupportsUtf16Runs() =>
            default(global::LuminPack.Option.LuminPackUtf16LengthStrings).SupportsUtf16Runs();

        /// <inheritdoc/>
        public void WriteSlots(scoped ref global::LuminPack.Core.LuminPackWriter writer, scoped global::System.ReadOnlySpan<string?> values) =>
            default(global::LuminPack.Option.LuminPackUtf16LengthStrings).WriteSlots(ref writer, values);

        /// <inheritdoc/>
        public void WriteCheckedSlots(scoped ref global::LuminPack.Core.LuminPackWriter writer, scoped global::System.ReadOnlySpan<string?> values) =>
            default(global::LuminPack.Option.LuminPackUtf16LengthStrings).WriteCheckedSlots(ref writer, values);

        /// <inheritdoc/>
        public void WritePairs(scoped ref global::LuminPack.Core.LuminPackWriter writer, scoped global::System.ReadOnlySpan<string?> keys, scoped global::System.ReadOnlySpan<string?> values) =>
            default(global::LuminPack.Option.LuminPackUtf16LengthStrings).WritePairs(ref writer, keys, values);

        /// <inheritdoc/>
        public void WritePairRuns(scoped ref global::LuminPack.Core.LuminPackWriter writer, scoped global::System.ReadOnlySpan<string?> keys, scoped global::System.ReadOnlySpan<string?> values, bool allowRuns) =>
            default(global::LuminPack.Option.LuminPackUtf16LengthStrings).WritePairRuns(ref writer, keys, values, allowRuns);

        /// <inheritdoc/>
        public ref byte WriteAtCursor(scoped ref global::LuminPack.Core.LuminPackWriter writer, ref byte cursor, string? value) =>
            ref default(global::LuminPack.Option.LuminPackUtf16LengthStrings).WriteAtCursor(ref writer, ref cursor, value);

        /// <inheritdoc/>
        public int WriteSlotPrefix(scoped ref global::LuminPack.Core.LuminPackWriter writer, scoped global::System.ReadOnlySpan<string?> values) =>
            default(global::LuminPack.Option.LuminPackUtf16LengthStrings).WriteSlotPrefix(ref writer, values);

        /// <inheritdoc/>
        public int WritePairPrefix(scoped ref global::LuminPack.Core.LuminPackWriter writer, scoped global::System.ReadOnlySpan<global::System.Collections.Generic.KeyValuePair<string, string?>> pairs) =>
            default(global::LuminPack.Option.LuminPackUtf16LengthStrings).WritePairPrefix(ref writer, pairs);

        /// <inheritdoc/>
        public bool TryPeekCharacterCount(scoped ref global::LuminPack.Core.LuminPackReader reader, out int count) =>
            default(global::LuminPack.Option.LuminPackUtf16LengthStrings).TryPeekCharacterCount(ref reader, out count);

        /// <inheritdoc/>
        public bool TryPeekUtf16Payload(scoped ref global::LuminPack.Core.LuminPackReader reader, int length, out int bytes, out bool latin1) =>
            default(global::LuminPack.Option.LuminPackUtf16LengthStrings).TryPeekUtf16Payload(ref reader, length, out bytes, out latin1);

        /// <inheritdoc/>
        public global::LuminPack.Option.LuminPackStringEncoding Encoding => default(global::LuminPack.Option.LuminPackUtf16LengthStrings).Encoding;

        /// <inheritdoc/>
        public global::LuminPack.Option.LuminPackStringRecording Recording => default(global::LuminPack.Option.LuminPackUtf16LengthStrings).Recording;

        /// <inheritdoc/>
        public byte BinaryProfile => default(global::LuminPack.Option.LuminPackUtf16LengthStrings).BinaryProfile;

        /// <inheritdoc/>
        public LuminPackStringEncoding StringEncoding => LuminPackStringEncoding.UTF16;
        /// <inheritdoc/>
        public LuminPackStringRecording StringRecording => LuminPackStringRecording.Length;
        /// <inheritdoc/>
        public StandardFormat StandardFormat => new('G');
        /// <inheritdoc/>
        public int MaxJsonDepth => 31417;
        /// <inheritdoc/>
        public bool ValidateJsonSyntax => false;
    }

    /// <summary>Requires a compatible closed registration for UTF-8 JSON.</summary>
    private readonly struct Utf8Profile : ILuminPackOptions
    {
        /// <inheritdoc/>
        public int RecordBound(string? value) =>
            default(global::LuminPack.Option.LuminPackUtf8LengthStrings).RecordBound(value);

        /// <inheritdoc/>
        public ref byte WriteRecord(scoped ref global::LuminPack.Core.LuminPackWriter writer, ref byte cursor, string? value) =>
            ref default(global::LuminPack.Option.LuminPackUtf8LengthStrings).WriteRecord(ref writer, ref cursor, value);

        /// <inheritdoc/>
        public void Write(scoped ref global::LuminPack.Core.LuminPackWriter writer, string? value) =>
            default(global::LuminPack.Option.LuminPackUtf8LengthStrings).Write(ref writer, value);

        /// <inheritdoc/>
        public string? Read(scoped ref global::LuminPack.Core.LuminPackReader reader, string? current) =>
            default(global::LuminPack.Option.LuminPackUtf8LengthStrings).Read(ref reader, current);

        /// <inheritdoc/>
        public void ReadPair(scoped ref global::LuminPack.Core.LuminPackReader reader, scoped ref string? first, scoped ref string? second) =>
            default(global::LuminPack.Option.LuminPackUtf8LengthStrings).ReadPair(ref reader, ref first, ref second);

        /// <inheritdoc/>
        public void Skip(scoped ref global::LuminPack.Core.LuminPackReader reader, int maxStringLength) =>
            default(global::LuminPack.Option.LuminPackUtf8LengthStrings).Skip(ref reader, maxStringLength);

        /// <inheritdoc/>
        public void ReadSlots(scoped ref global::LuminPack.Core.LuminPackReader reader, scoped global::System.Span<string?> slots) =>
            default(global::LuminPack.Option.LuminPackUtf8LengthStrings).ReadSlots(ref reader, slots);

        /// <inheritdoc/>
        public global::System.Buffers.OperationStatus Encode(global::System.ReadOnlySpan<char> source, global::System.Span<byte> destination, out int charsRead, out int bytesWritten, bool replaceInvalidSequences, bool isFinalBlock) =>
            default(global::LuminPack.Option.LuminPackUtf8LengthStrings).Encode(source, destination, out charsRead, out bytesWritten, replaceInvalidSequences, isFinalBlock);

        /// <inheritdoc/>
        public int Measure(string? value) =>
            default(global::LuminPack.Option.LuminPackUtf8LengthStrings).Measure(value);

        /// <inheritdoc/>
        public void WriteReserved(scoped ref global::LuminPack.Core.LuminPackWriter writer, string? value) =>
            default(global::LuminPack.Option.LuminPackUtf8LengthStrings).WriteReserved(ref writer, value);

        /// <inheritdoc/>
        public void WriteText(scoped ref global::LuminPack.Core.LuminPackWriter writer, scoped global::System.ReadOnlySpan<char> text) =>
            default(global::LuminPack.Option.LuminPackUtf8LengthStrings).WriteText(ref writer, text);

        /// <inheritdoc/>
        public global::System.Buffers.OperationStatus Decode(global::System.ReadOnlySpan<byte> source, global::System.Span<char> destination, out int bytesRead, out int charsWritten, bool replaceInvalidSequences, bool isFinalBlock) =>
            default(global::LuminPack.Option.LuminPackUtf8LengthStrings).Decode(source, destination, out bytesRead, out charsWritten, replaceInvalidSequences, isFinalBlock);

        /// <inheritdoc/>
        public bool SupportsUtf16Runs() =>
            default(global::LuminPack.Option.LuminPackUtf8LengthStrings).SupportsUtf16Runs();

        /// <inheritdoc/>
        public void WriteSlots(scoped ref global::LuminPack.Core.LuminPackWriter writer, scoped global::System.ReadOnlySpan<string?> values) =>
            default(global::LuminPack.Option.LuminPackUtf8LengthStrings).WriteSlots(ref writer, values);

        /// <inheritdoc/>
        public void WriteCheckedSlots(scoped ref global::LuminPack.Core.LuminPackWriter writer, scoped global::System.ReadOnlySpan<string?> values) =>
            default(global::LuminPack.Option.LuminPackUtf8LengthStrings).WriteCheckedSlots(ref writer, values);

        /// <inheritdoc/>
        public void WritePairs(scoped ref global::LuminPack.Core.LuminPackWriter writer, scoped global::System.ReadOnlySpan<string?> keys, scoped global::System.ReadOnlySpan<string?> values) =>
            default(global::LuminPack.Option.LuminPackUtf8LengthStrings).WritePairs(ref writer, keys, values);

        /// <inheritdoc/>
        public void WritePairRuns(scoped ref global::LuminPack.Core.LuminPackWriter writer, scoped global::System.ReadOnlySpan<string?> keys, scoped global::System.ReadOnlySpan<string?> values, bool allowRuns) =>
            default(global::LuminPack.Option.LuminPackUtf8LengthStrings).WritePairRuns(ref writer, keys, values, allowRuns);

        /// <inheritdoc/>
        public ref byte WriteAtCursor(scoped ref global::LuminPack.Core.LuminPackWriter writer, ref byte cursor, string? value) =>
            ref default(global::LuminPack.Option.LuminPackUtf8LengthStrings).WriteAtCursor(ref writer, ref cursor, value);

        /// <inheritdoc/>
        public int WriteSlotPrefix(scoped ref global::LuminPack.Core.LuminPackWriter writer, scoped global::System.ReadOnlySpan<string?> values) =>
            default(global::LuminPack.Option.LuminPackUtf8LengthStrings).WriteSlotPrefix(ref writer, values);

        /// <inheritdoc/>
        public int WritePairPrefix(scoped ref global::LuminPack.Core.LuminPackWriter writer, scoped global::System.ReadOnlySpan<global::System.Collections.Generic.KeyValuePair<string, string?>> pairs) =>
            default(global::LuminPack.Option.LuminPackUtf8LengthStrings).WritePairPrefix(ref writer, pairs);

        /// <inheritdoc/>
        public bool TryPeekCharacterCount(scoped ref global::LuminPack.Core.LuminPackReader reader, out int count) =>
            default(global::LuminPack.Option.LuminPackUtf8LengthStrings).TryPeekCharacterCount(ref reader, out count);

        /// <inheritdoc/>
        public bool TryPeekUtf16Payload(scoped ref global::LuminPack.Core.LuminPackReader reader, int length, out int bytes, out bool latin1) =>
            default(global::LuminPack.Option.LuminPackUtf8LengthStrings).TryPeekUtf16Payload(ref reader, length, out bytes, out latin1);

        /// <inheritdoc/>
        public global::LuminPack.Option.LuminPackStringEncoding Encoding => default(global::LuminPack.Option.LuminPackUtf8LengthStrings).Encoding;

        /// <inheritdoc/>
        public global::LuminPack.Option.LuminPackStringRecording Recording => default(global::LuminPack.Option.LuminPackUtf8LengthStrings).Recording;

        /// <inheritdoc/>
        public byte BinaryProfile => default(global::LuminPack.Option.LuminPackUtf8LengthStrings).BinaryProfile;

        /// <inheritdoc/>
        public LuminPackStringEncoding StringEncoding => LuminPackStringEncoding.UTF8;
        /// <inheritdoc/>
        public LuminPackStringRecording StringRecording => LuminPackStringRecording.Length;
        /// <inheritdoc/>
        public StandardFormat StandardFormat => new('G');
        /// <inheritdoc/>
        public int MaxJsonDepth => 31417;
        /// <inheritdoc/>
        public bool ValidateJsonSyntax => false;
    }
}
