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
    /// <summary>Checks late registration, invalidation, complete publication and collectible payload lifetime.</summary>
    internal static void Run()
    {
        Assert.False(LuminPackFormatterRegistry<Profile>.TryGetCachedValue<Payload>(out _));
        LuminPackFormatterRegistry.Register(typeof(Payload), 1, 2, 3, 4, 5);
        Assert.True(LuminPackFormatterRegistry<Profile>.TryGetCachedValue<Payload>(out var entry));
        CheckEntry(entry, 0);
        Assert.True(LuminPackFormatterRegistry<Profile>.TryGetCachedJsonValue<Payload>(out entry));
        CheckEntry(entry, 0);

        LuminPackFormatterRegistry<Profile>.Register(typeof(Payload), 11, 12, 13, 14, 15);
        Assert.True(LuminPackFormatterRegistry<Profile>.TryGetCachedValue<Payload>(out entry));
        CheckEntry(entry, 10);
        Assert.True(LuminPackFormatterRegistry<EquivalentProfile>.TryGetCachedValue<Payload>(out entry));
        CheckEntry(entry, 10);
        Assert.True(LuminPackFormatterRegistry<Utf8Profile>.TryGetCachedValue<Payload>(out entry));
        CheckEntry(entry, 0);
        Assert.False(LuminPackFormatterRegistry<Utf8Profile>.TryGetCachedJsonValue<Payload>(out _));
        LuminPackFormatterRegistry<Utf8Profile>.Register(typeof(Payload), 21, 22, 23, 24, 25);
        Assert.True(LuminPackFormatterRegistry<Utf8Profile>.TryGetCachedJsonValue<Payload>(out entry));
        CheckEntry(entry, 20);

        Assert.Throws<ArgumentException>(() => LuminPackFormatterRegistry<Profile>.Register(typeof(Payload), 31, 32));
        Assert.True(LuminPackFormatterRegistry<Profile>.TryGetCachedValue<Payload>(out entry));
        CheckEntry(entry, 10);
        CheckConcurrentInvalidation();

        LuminPackFormatterRegistry.Register(typeof(ColdMarker), 101, 102);
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

    /// <summary>Checks that all five addresses come from one immutable registration result.</summary>
    /// <param name="entry">The observed entry.</param>
    /// <param name="basis">The common address prefix.</param>
    private static void CheckEntry(LuminPackFormatter entry, int basis)
    {
        Assert.Equal((nint)(basis + 1), entry.WriteValue);
        Assert.Equal((nint)(basis + 2), entry.ReadValue);
        Assert.Equal((nint)(basis + 3), entry.WriteValueJson);
        Assert.Equal((nint)(basis + 4), entry.ReadValueJson);
        Assert.Equal((nint)(basis + 5), entry.CalculateOffset);
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
                LuminPackFormatterRegistry.Register(payload, 201, 202);
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
        LuminPackFormatterRegistry.Register(payload, 301, 302);
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
    private sealed class CollectiblePayload { }
    /// <summary>Supplies distinct keys for concurrent registration.</summary>
    /// <typeparam name="TLeft">The first identity component.</typeparam>
    /// <typeparam name="TRight">The second identity component.</typeparam>
    private sealed class Other<TLeft, TRight> { }

    /// <summary>Provides one distinct complete profile for the snapshot checks.</summary>
    private readonly struct Profile : ILuminPackOptions
    {
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
