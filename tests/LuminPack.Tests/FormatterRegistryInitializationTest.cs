using System.Buffers;
using System.Diagnostics;
using System.Reflection;
using LuminPack.Code;
using LuminPack.Option;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Checks concurrent profile initialization without leaving a deadlocked registry in the test host.</summary>
public sealed class FormatterRegistryInitializationTest
{
    /// <summary>Creates an observable registration token for publication and invalidation checks.</summary>
    /// <param name="value">The token identity.</param>
    /// <returns>A delegate that preserves the token.</returns>
    private static Delegate Callback(nint value) => (Func<nint>)(() => value);

    /// <summary>Runs the controlled constructor/register race in a disposable child process.</summary>
    [Fact]
    public void ConcurrentInitializationAndPublicationRemainConsistent()
    {
        // The SDK host can be x64 even when VSTest runs a self-contained x86 assembly.
        // Use this assembly's apphost so the worker retains the tested architecture.
        string assemblyPath = typeof(FormatterRegistryInitializationTest).Assembly.Location;
        string appHost = Path.ChangeExtension(assemblyPath, OperatingSystem.IsWindows() ? ".exe" : null);
        Assert.True(File.Exists(appHost), "The isolated registry worker apphost is missing: " + appHost);
        var start = new ProcessStartInfo(appHost)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        start.ArgumentList.Add("--registry-initialization-worker");
        using var worker = Process.Start(start)!;
        var output = worker.StandardOutput.ReadToEndAsync();
        var error = worker.StandardError.ReadToEndAsync();
        bool finished = worker.WaitForExit(45_000);
        if (!finished)
        {
            worker.Kill(entireProcessTree: true);
            worker.WaitForExit();
        }
        Assert.True(finished, "The isolated registry initialization worker did not complete.");
        Assert.True(worker.ExitCode == 0, "Worker exit code " + worker.ExitCode + ": " + output.GetAwaiter().GetResult() + error.GetAwaiter().GetResult());
    }

    /// <summary>Coordinates the first lookup and registration for one fresh closed profile.</summary>
    /// <returns>Zero on success, or a diagnostic failure code for the isolated host.</returns>
    internal static int RunWorker()
    {
        try
        {
            nint key = LuminPackMarshal.GetMethodTable(typeof(Payload));
            Exception? lookupError = null, registrationError = null;
            var lookup = new Thread(() =>
            {
                try { LuminPackFormatterRegistry<PausedProfile>.TryGetValue(in key, out _); }
                catch (Exception exception) { lookupError = exception; }
            }) { IsBackground = true };
            var registration = new Thread(() =>
            {
                RegistrationEntered.Set();
                try { RegisterPausedProfile(); }
                catch (Exception exception) { registrationError = exception; }
            }) { IsBackground = true };
            lookup.Start();
            if (!InitializationEntered.Wait(10_000)) return 11;
            registration.Start();
            if (!RegistrationEntered.Wait(10_000)) return 12;
            if (!registration.IsAlive) return 13;
            var unrelatedLookup = Task.Run(() => LuminPackFormatterRegistry<LuminPackUtf8LengthOptions>.TryGetValue(in key, out _));
            if (!unrelatedLookup.Wait(5_000))
            {
                Console.Error.WriteLine("An unrelated lookup blocked behind a paused profile initializer.");
                return 14;
            }
            InitializationReleased.Set();
            if (!lookup.Join(10_000) || !registration.Join(10_000)) return 15;
            if (lookupError != null || registrationError != null)
            {
                Console.Error.WriteLine(lookupError?.ToString() ?? registrationError!.ToString());
                return 16;
            }
            if (!LuminPackFormatterRegistry<PausedProfile>.TryGetValue(in key, out var entry) || ((Func<nint>)entry.WriteValue!)() != 1 || ((Func<nint>)entry.ReadValue!)() != 2)
                return 17;
            if (!LuminPackFormatterRegistry<EquivalentProfile>.TryGetValue(in key, out entry) || ((Func<nint>)entry.WriteValue!)() != 1 || ((Func<nint>)entry.ReadValue!)() != 2)
                return 18;
            if (!CheckPublication(compiled: true) || !CheckPublication(compiled: false)) return 20;
            FormatterRegistryCacheTest.Run();
            Console.WriteLine("Concurrent initialization and equivalent-profile registration sharing passed.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 19;
        }
        finally
        {
            InitializationReleased.Set();
        }
    }

    private static readonly ManualResetEventSlim InitializationEntered = new();
    private static readonly ManualResetEventSlim InitializationReleased = new();
    private static readonly ManualResetEventSlim RegistrationEntered = new();

    /// <summary>Keeps early generic initialization from moving ahead of the registration-thread handshake.</summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static void RegisterPausedProfile() => LuminPackFormatterRegistry<PausedProfile>.Register(typeof(Payload), Callback(1), Callback(2));

    /// <summary>Checks that inserting and rehashing registrations cannot hide existing entries or tear delegate tokens.</summary>
    /// <param name="compiled">Whether to exercise the profile-specific or declaration-owned registry.</param>
    /// <returns>Whether every concurrent lookup retained the already registered delegate tokens.</returns>
    private static bool CheckPublication(bool compiled)
    {
        Type[] arguments = [typeof(byte), typeof(short), typeof(int), typeof(long), typeof(float), typeof(double), typeof(string), typeof(Guid),
            typeof(DateTime), typeof(TimeSpan), typeof(decimal), typeof(object), typeof(Version), typeof(int[]), typeof(uint), typeof(bool)];
        var payloads = arguments.SelectMany(left => arguments.Select(right => typeof(PublicationPayload<,>).MakeGenericType(left, right))).ToArray();
        var keys = payloads.Select(type => (nint)LuminPackMarshal.GetMethodTable(type)).ToArray();
        for (int index = 0; index < 32; index++) RegisterPublication(payloads[index], index, compiled);
        using var ready = new CountdownEvent(3);
        using var start = new ManualResetEventSlim();
        int stopped = 0;
        string? failure = null;
        var readers = Enumerable.Range(0, 3).Select(readerIndex => new Thread(() =>
        {
            ready.Signal();
            start.Wait();
            int index = readerIndex;
            while (Volatile.Read(ref stopped) == 0)
            {
                try
                {
                    bool found = compiled ? LuminPackFormatterRegistry<PublicationProfile>.TryGetValue(in keys[index], out var foundEntry) :
                        LuminPackFormatterRegistry.TryGetValue(in keys[index], out foundEntry);
                    nint expected = (index + 1) * 16;
                    if (!found || ((Func<nint>)foundEntry.WriteValue!)() != expected + 1 || ((Func<nint>)foundEntry.ReadValue!)() != expected + 2 ||
                        ((Func<nint>)foundEntry.WriteValueJson!)() != expected + 3 || ((Func<nint>)foundEntry.ReadValueJson!)() != expected + 4)
                        Interlocked.CompareExchange(ref failure, "A concurrent registry lookup lost or mixed delegate tokens.", null);
                }
                catch (Exception exception) { Interlocked.CompareExchange(ref failure, exception.ToString(), null); }
                index = (index + 1) & 31;
            }
        }) { IsBackground = true }).ToArray();
        foreach (var reader in readers) reader.Start();
        ready.Wait();
        start.Set();
        try
        {
            for (int index = 32; index < payloads.Length; index++)
            {
                RegisterPublication(payloads[index], index, compiled);
                Thread.Yield();
            }
        }
        finally
        {
            Volatile.Write(ref stopped, 1);
            foreach (var reader in readers) reader.Join();
        }
        GC.KeepAlive(payloads);
        if (failure == null) return true;
        Console.Error.WriteLine((compiled ? "Compiled: " : "Declaration: ") + failure);
        return false;
    }

    /// <summary>Publishes distinct delegate tokens without invoking them.</summary>
    /// <param name="payload">The unique closed payload type.</param>
    /// <param name="index">The token identity.</param>
    /// <param name="compiled">The registry family under test.</param>
    private static void RegisterPublication(Type payload, int index, bool compiled)
    {
        nint value = (index + 1) * 16;
        if (compiled) LuminPackFormatterRegistry<PublicationProfile>.Register(payload, Callback(value + 1), Callback(value + 2), Callback(value + 3), Callback(value + 4));
        else LuminPackFormatterRegistry.Register(payload, Callback(value + 1), Callback(value + 2), Callback(value + 3), Callback(value + 4));
    }

    /// <summary>Provides a unique key without executing the placeholder delegate tokens.</summary>
    private sealed class Payload { }

    /// <summary>Supplies unique runtime keys for the registration publication test.</summary>
    /// <typeparam name="TLeft">The first identity component.</typeparam>
    /// <typeparam name="TRight">The second identity component.</typeparam>
    private sealed class PublicationPayload<TLeft, TRight> { }

    /// <summary>Separates concurrent publication entries from the initialization race.</summary>
    private readonly struct PublicationProfile : ILuminPackOptions
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
        public int MaxJsonDepth => 31416;
        /// <inheritdoc/>
        public bool ValidateJsonSyntax => false;
    }

    /// <summary>Exposes constant options while allowing the test to pause the first table construction.</summary>
    private readonly struct PausedProfile : ILuminPackOptions
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
        public LuminPackStringEncoding StringEncoding
        {
            get
            {
                InitializationEntered.Set();
                if (!InitializationReleased.Wait(30_000)) throw new TimeoutException("The registry worker did not release initialization.");
                return LuminPackStringEncoding.UTF8;
            }
        }
        /// <inheritdoc/>
        public LuminPackStringRecording StringRecording => LuminPackStringRecording.Length;
        /// <inheritdoc/>
        public StandardFormat StandardFormat => new('G');
        /// <inheritdoc/>
        public int MaxJsonDepth => 31415;
        /// <inheritdoc/>
        public bool ValidateJsonSyntax => false;
    }

    /// <summary>Checks that the repaired ordering retains value-based profile table sharing.</summary>
    private readonly struct EquivalentProfile : ILuminPackOptions
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
        public int MaxJsonDepth => 31415;
        /// <inheritdoc/>
        public bool ValidateJsonSyntax => false;
    }
}
