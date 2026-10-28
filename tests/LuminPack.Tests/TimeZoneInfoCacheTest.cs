using System.Buffers.Binary;
using System.Reflection;
using System.Runtime.CompilerServices;
using LuminPack;
using LuminPack.Code;
using LuminPack.Core;
using LuminPack.Option;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Preserves complete time-zone snapshot semantics and bounded immutable cache ownership.</summary>
[Collection(nameof(RuntimeStateCollection))]
public class TimeZoneInfoCacheTest
{
    /// <summary>Retains the original complete UTF-16 record and exact evaluator width.</summary>
    [Fact]
    public void CompleteSnapshotBytesRemainUnchanged()
    {
        foreach (var zone in new[] { TimeZoneInfo.Local, TimeZoneInfo.Utc, CreateZone("Bytes"), CreateDaylightZone() })
        {
            string text = zone.ToSerializedString();
            // A raw UTF-16 snapshot written before the compact Latin-1 form must stay readable.
            byte[] raw = new byte[5 + text.Length * sizeof(char)];
            BinaryPrimitives.WriteInt32LittleEndian(raw.AsSpan(1), text.Length * sizeof(char));
            for (int i = 0; i < text.Length; i++) BinaryPrimitives.WriteUInt16LittleEndian(raw.AsSpan(5 + i * 2), text[i]);
            TimeZoneInfo? fromRaw = null;
            Assert.Equal(raw.Length, LuminPackSerializer.Deserialize(raw.AsSpan(), ref fromRaw));
            Assert.Equal(text, fromRaw!.ToSerializedString());
            byte[] expected = raw;
            if (text.All(character => character <= 'ÿ'))
            {
                expected = new byte[5 + text.Length];
                BinaryPrimitives.WriteInt32LittleEndian(expected.AsSpan(1), (text.Length << 1) | 1);
                for (int i = 0; i < text.Length; i++) expected[5 + i] = (byte)text[i];
            }
            Assert.Equal(expected, LuminPackSerializer.Serialize(zone));
            Assert.Equal(expected.Length, LuminPackSerializer.Sizeof(zone));
            TimeZoneInfo? copy = null;
            Assert.Equal(expected.Length, LuminPackSerializer.Deserialize(expected.AsSpan(), ref copy));
            Assert.Equal(text, copy!.ToSerializedString());
            Assert.True(copy.HasSameRules(zone));
        }
    }

    /// <summary>Reads all four binary profiles and both JSON document encodings without changing the sender's rules.</summary>
    [Fact]
    public void ExplicitProfilesPreserveFullRules()
    {
        var zone = CreateDaylightZone();
        VerifyProfile<LuminPackUtf16LengthOptions>(zone);
        VerifyProfile<LuminPackUtf16TokenOptions>(zone);
        VerifyProfile<LuminPackUtf8LengthOptions>(zone);
        VerifyProfile<LuminPackUtf8TokenOptions>(zone);
    }

    /// <summary>Distinguishes same-ID snapshots with different offsets and names, including equal-length collisions.</summary>
    [Fact]
    public void CompleteContentDeterminesCacheIdentity()
    {
        string id = "Cache/SameId";
        var first = TimeZoneInfo.CreateCustomTimeZone(id, TimeSpan.FromHours(2), "Display A", "Standard A");
        var second = TimeZoneInfo.CreateCustomTimeZone(id, TimeSpan.FromHours(3), "Display B", "Standard B");
        Assert.Equal(first.ToSerializedString().Length, second.ToSerializedString().Length);
        byte[] a = LuminPackSerializer.Serialize(first), b = LuminPackSerializer.Serialize(second);
        for (int i = 0; i < 20; i++)
        {
            TimeZoneInfo? copy = null;
            LuminPackSerializer.Deserialize(a.AsSpan(), ref copy);
            Assert.Equal(first.ToSerializedString(), copy!.ToSerializedString());
            LuminPackSerializer.Deserialize(b.AsSpan(), ref copy);
            Assert.Equal(second.ToSerializedString(), copy!.ToSerializedString());
        }
    }

    /// <summary>Checks cached publication after every truncated prefix and malformed snapshot.</summary>
    [Fact]
    public void TruncatedRecordsNeverPublishCachedValues()
    {
        var zone = CreateZone("Truncated");
        byte[] bytes = LuminPackSerializer.Serialize(zone);
        TimeZoneInfo? warm = null;
        LuminPackSerializer.Deserialize(bytes.AsSpan(), ref warm);
        var sentinel = CreateZone("Sentinel");
        for (int length = 0; length < bytes.Length; length++)
        {
            TimeZoneInfo? destination = sentinel;
            Assert.ThrowsAny<Exception>(() => LuminPackSerializer.Deserialize(bytes.AsSpan(0, length), ref destination));
            Assert.Same(sentinel, destination);
        }
        byte[] malformed = [0, 3, 0, 0, 0, 1, 2, 3];
        TimeZoneInfo? result = sentinel;
        Assert.ThrowsAny<Exception>(() => LuminPackSerializer.Deserialize(malformed.AsSpan(), ref result));
        Assert.Same(sentinel, result);
        for (int i = 0; i < 2; i++)
            Assert.Throws<System.Runtime.Serialization.SerializationException>(() => LuminPackTimeZoneInfoCache.Parse("invalid snapshot"));
    }

    /// <summary>Retains null framing, null publication and a present object's immutable sharing.</summary>
    [Fact]
    public void NullAndImmutableReuseRemainDistinct()
    {
        TimeZoneInfo? absent = null;
        Assert.Equal(new byte[] { 255 }, LuminPackSerializer.Serialize(absent!));
        TimeZoneInfo? destination = TimeZoneInfo.Utc;
        Assert.Equal(1, LuminPackSerializer.Deserialize(new byte[] { 255 }.AsSpan(), ref destination));
        Assert.Null(destination);
        var zone = CreateZone("Shared");
        byte[] bytes = LuminPackSerializer.Serialize(zone);
        LuminPackSerializer.Deserialize(bytes.AsSpan(), ref destination);
        TimeZoneInfo? second = null;
        LuminPackSerializer.Deserialize(bytes.AsSpan(), ref second);
        Assert.Same(destination, second);
        string json = LuminPackSerializer.SerializeJson(absent!);
        LuminPackSerializer.DeserializeJson(json, ref second);
        Assert.Null(second);
    }

    /// <summary>Parses oversized values completely while keeping their text out of the bounded read cache.</summary>
    [Fact]
    public void OversizedSnapshotsAreNotAdmitted()
    {
        var zone = TimeZoneInfo.CreateCustomTimeZone("Cache/Large", TimeSpan.Zero, new string('x', 9000), "Standard");
        byte[] bytes = LuminPackSerializer.Serialize(zone);
        TimeZoneInfo? first = null, second = null;
        LuminPackSerializer.Deserialize(bytes.AsSpan(), ref first);
        LuminPackSerializer.Deserialize(bytes.AsSpan(), ref second);
        Assert.NotSame(first, second);
        Assert.Equal(zone.ToSerializedString(), second!.ToSerializedString());
        var entries = (Array)typeof(LuminPackTimeZoneInfoCache).GetField("Entries", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
        Assert.Equal(8, entries.Length);
        foreach (LuminPackTimeZoneInfoCache.Entry? entry in entries) if (entry is not null) Assert.InRange(entry.Text.Length, 0, 256);
        Assert.DoesNotContain(typeof(LuminPackTimeZoneInfoCache).GetFields(BindingFlags.Static | BindingFlags.NonPublic),
            field => field.IsDefined(typeof(ThreadStaticAttribute), false));
    }

    /// <summary>Does not keep a source instance alive through its writer-text cache.</summary>
    [Fact]
    public void WriterKeysAreWeak()
    {
        WeakReference source = CreateWeakWriterKey();
        for (int i = 0; i < 10 && source.IsAlive; i++) { GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); }
        Assert.False(source.IsAlive);
    }

    /// <summary>Delays weak-table admission without trusting a fingerprint as snapshot identity.</summary>
    [Fact]
    public void WriterAdmissionDoesNotTrustFingerprints()
    {
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Static;
        var table = (ConditionalWeakTable<TimeZoneInfo, string>)typeof(LuminPackTimeZoneInfoCache).GetField("Texts", flags)!.GetValue(null)!;
        var fingerprints = (int[])typeof(LuminPackTimeZoneInfoCache).GetField("WriterAdmissionFingerprints", flags)!.GetValue(null)!;
        Assert.Equal(64, fingerprints.Length);
        var first = CreateZone("Admission");
        int hash = RuntimeHelpers.GetHashCode(first);
        fingerprints[hash & 63] = hash ^ 1;
        byte[] once = LuminPackSerializer.Serialize(first);
        Assert.False(table.TryGetValue(first, out _));
        Assert.Equal(once, LuminPackSerializer.Serialize(first));
        Assert.True(table.TryGetValue(first, out string? stored));
        Assert.Same(stored, LuminPackTimeZoneInfoCache.GetText(first));
        var other = CreateZone("OtherAdmission");
        int otherHash = RuntimeHelpers.GetHashCode(other);
        fingerprints[otherHash & 63] = otherHash;
        Assert.Equal(other.ToSerializedString(), LuminPackTimeZoneInfoCache.GetText(other));
        Assert.True(table.TryGetValue(other, out _));
    }

    /// <summary>Publishes complete entries under concurrent misses, eviction and repeated reads.</summary>
    [Fact]
    public void ConcurrentEvictionPreservesExactSnapshots()
    {
        Parallel.For(0, 8, worker =>
        {
            var zone = CreateZone("Worker" + worker);
            byte[] record = LuminPackSerializer.Serialize(zone);
            string expected = zone.ToSerializedString();
            for (int i = 0; i < 500; i++)
            {
                TimeZoneInfo? value = null;
                Assert.Equal(record.Length, LuminPackSerializer.Deserialize(record.AsSpan(), ref value));
                Assert.Equal(expected, value!.ToSerializedString());
            }
        });
    }

    /// <summary>Creates a custom snapshot distinct from the platform's registered zones.</summary>
    /// <param name="suffix">The unique fixture identity.</param>
    /// <returns>An immutable custom zone.</returns>
    private static TimeZoneInfo CreateZone(string suffix)
        => TimeZoneInfo.CreateCustomTimeZone("Cache/" + suffix, TimeSpan.FromMinutes(345), "Custom Display", "Custom Standard");

    /// <summary>Creates a snapshot containing a floating transition rule and a non-hour daylight delta.</summary>
    /// <returns>An immutable custom daylight-saving zone.</returns>
    private static TimeZoneInfo CreateDaylightZone()
    {
        var start = TimeZoneInfo.TransitionTime.CreateFloatingDateRule(new DateTime(1, 1, 1, 2, 0, 0), 3, 2, DayOfWeek.Sunday);
        var end = TimeZoneInfo.TransitionTime.CreateFloatingDateRule(new DateTime(1, 1, 1, 3, 0, 0), 11, 1, DayOfWeek.Sunday);
        var rule = TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(new DateTime(2000, 1, 1), new DateTime(2099, 12, 31), TimeSpan.FromMinutes(45), start, end);
        return TimeZoneInfo.CreateCustomTimeZone("Cache/DST", TimeSpan.FromHours(2), "Display", "Standard", "Daylight", [rule]);
    }

    /// <summary>Warms a weak writer key outside the collecting test's stack frame.</summary>
    /// <returns>The source key's weak observation.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference CreateWeakWriterKey()
    {
        var zone = CreateZone("Weak");
        for (int i = 0; i < 3; i++) _ = LuminPackSerializer.Serialize(zone);
        return new WeakReference(zone);
    }

    /// <summary>Verifies a complete snapshot through a stateless binary and JSON profile.</summary>
    /// <typeparam name="TProfile">The compile-time encoding and recording policy.</typeparam>
    /// <param name="zone">The complete source snapshot.</param>
    private static void VerifyProfile<TProfile>(TimeZoneInfo zone) where TProfile : struct, ILuminPackOptions
    {
        byte[] bytes = LuminPackSerializer.Serialize<TProfile>(zone);
        TimeZoneInfo? copy = null;
        Assert.Equal(bytes.Length, LuminPackSerializer.Deserialize<TProfile>(bytes.AsSpan(), ref copy));
        Assert.Equal(zone.ToSerializedString(), copy!.ToSerializedString());
        Assert.Equal(bytes.Length, LuminPackSerializer.Sizeof<TProfile>(zone));
        string json = LuminPackSerializer.SerializeJson<TProfile>(zone);
        copy = null;
        LuminPackSerializer.DeserializeJson<TProfile>(json, ref copy);
        Assert.Equal(zone.ToSerializedString(), copy!.ToSerializedString());
    }
}
