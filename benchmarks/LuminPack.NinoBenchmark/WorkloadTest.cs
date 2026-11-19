// Adapted from the Nino benchmark suite. See LICENSE.Nino.txt.
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using LuminPack.Utility;
using Nino.Core;

#nullable disable
namespace LuminPack.NinoBenchmark.Workloads;

/// <summary>Compares LuminPack and Nino on the same seeded Workloads fixtures.</summary>
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[MemoryDiagnoser]
public class WorkloadTest
{
    private NinoArrayBufferWriter _ninoBuffer;
    private LuminBufferWriter _luminPackBuffer;

    private static readonly MediaContent MediaContentValue = MediaContent.Create(new Random(1));
    private static readonly StackOverflowPage StackOverflowPageValue = StackOverflowPage.Create(new Random(2));
    private static readonly RpcCall RpcCallValue = RpcCall.Create(new Random(3));
    private static readonly ProfileV2 ProfileCacheValue = ProfileV2.From(ProfileV1.Create(new Random(4)));
    private static readonly ProfileV1 ProfileCacheOld = ProfileV1.Create(new Random(4));
    private static readonly EventStream EventStreamValue = EventStream.Create(new Random(5));
    private static readonly LogBatch LogBatchValue = LogBatch.Create(new Random(6));
    private static readonly StateSnapshot StateSnapshotValue = StateSnapshot.Create(new Random(7));
    private static readonly LockstepFrames LockstepFramesValue = LockstepFrames.Create(new Random(8));
    private static readonly AoiUpdate AoiUpdateValue = AoiUpdate.Create(new Random(9));
    private static readonly WorldRegion WorldRegionValue = WorldRegion.Create(new Random(10));
    private static readonly CardBattleLog CardBattleLogValue = CardBattleLog.Create(new Random(11));
    private static readonly IdleSaveV2 IdleSaveValue = IdleSaveV2.From(IdleSaveV1.Create(new Random(12)));
    private static readonly IdleSaveV1 IdleSaveOld = IdleSaveV1.Create(new Random(12));

    private StateSnapshot _ninoStateSnapshot;
    private StateSnapshot _luminPackStateSnapshot;
    private IdleSaveV2 _ninoIdleSave;
    private IdleSaveV2 _luminPackIdleSave;
    private static readonly byte[] NinoMediaContentPayload;
    private static readonly byte[] LuminPackMediaContentPayload;
    private static readonly byte[] NinoStackOverflowPagePayload;
    private static readonly byte[] LuminPackStackOverflowPagePayload;
    private static readonly byte[] NinoRpcCallPayload;
    private static readonly byte[] LuminPackRpcCallPayload;
    private static readonly byte[] NinoProfileCachePayload;
    private static readonly byte[] LuminPackProfileCachePayload;
    private static readonly byte[] NinoProfileCacheOldBytes;
    private static readonly byte[] LuminPackProfileCacheOldBytes;
    private static readonly byte[] NinoEventStreamPayload;
    private static readonly byte[] LuminPackEventStreamPayload;
    private static readonly byte[] NinoLogBatchPayload;
    private static readonly byte[] LuminPackLogBatchPayload;
    private static readonly byte[] NinoStateSnapshotPayload;
    private static readonly byte[] LuminPackStateSnapshotPayload;
    private static readonly byte[] NinoLockstepFramesPayload;
    private static readonly byte[] LuminPackLockstepFramesPayload;
    private static readonly byte[] NinoAoiUpdatePayload;
    private static readonly byte[] LuminPackAoiUpdatePayload;
    private static readonly byte[] NinoWorldRegionPayload;
    private static readonly byte[] LuminPackWorldRegionPayload;
    private static readonly byte[] NinoCardBattleLogPayload;
    private static readonly byte[] LuminPackCardBattleLogPayload;
    private static readonly byte[] NinoIdleSavePayload;
    private static readonly byte[] LuminPackIdleSavePayload;
    private static readonly byte[] NinoIdleSaveOldBytes;
    private static readonly byte[] LuminPackIdleSaveOldBytes;

    /// <summary>Encodes current and previous-version fixtures before timing.</summary>
    static WorkloadTest()
    {
        NinoMediaContentPayload = NinoSerializer.Serialize(MediaContentValue);
        LuminPackMediaContentPayload = BenchmarkBuffers.Serialize(MediaContentValue, (v, w) => LuminPackSerializer.Serialize(v, w));
        NinoStackOverflowPagePayload = NinoSerializer.Serialize(StackOverflowPageValue);
        LuminPackStackOverflowPagePayload = BenchmarkBuffers.Serialize(StackOverflowPageValue, (v, w) => LuminPackSerializer.Serialize(v, w));
        NinoRpcCallPayload = NinoSerializer.Serialize(RpcCallValue);
        LuminPackRpcCallPayload = BenchmarkBuffers.Serialize(RpcCallValue, (v, w) => LuminPackSerializer.Serialize(v, w));
        NinoProfileCachePayload = NinoSerializer.Serialize(ProfileCacheValue);
        LuminPackProfileCachePayload = BenchmarkBuffers.Serialize(ProfileCacheValue, (v, w) => LuminPackSerializer.Serialize(v, w));
        NinoProfileCacheOldBytes = NinoSerializer.Serialize(ProfileCacheOld);
        LuminPackProfileCacheOldBytes = BenchmarkBuffers.Serialize(ProfileCacheOld, (v, w) => LuminPackSerializer.Serialize(v, w));
        NinoEventStreamPayload = NinoSerializer.Serialize(EventStreamValue);
        LuminPackEventStreamPayload = BenchmarkBuffers.Serialize(EventStreamValue, (v, w) => LuminPackSerializer.Serialize(v, w));
        NinoLogBatchPayload = NinoSerializer.Serialize(LogBatchValue);
        LuminPackLogBatchPayload = BenchmarkBuffers.Serialize(LogBatchValue, (v, w) => LuminPackSerializer.Serialize(v, w));
        NinoStateSnapshotPayload = NinoSerializer.Serialize(StateSnapshotValue);
        LuminPackStateSnapshotPayload = BenchmarkBuffers.Serialize(StateSnapshotValue, (v, w) => LuminPackSerializer.Serialize(v, w));
        NinoLockstepFramesPayload = NinoSerializer.Serialize(LockstepFramesValue);
        LuminPackLockstepFramesPayload = BenchmarkBuffers.Serialize(LockstepFramesValue, (v, w) => LuminPackSerializer.Serialize(v, w));
        NinoAoiUpdatePayload = NinoSerializer.Serialize(AoiUpdateValue);
        LuminPackAoiUpdatePayload = BenchmarkBuffers.Serialize(AoiUpdateValue, (v, w) => LuminPackSerializer.Serialize(v, w));
        NinoWorldRegionPayload = NinoSerializer.Serialize(WorldRegionValue);
        LuminPackWorldRegionPayload = BenchmarkBuffers.Serialize(WorldRegionValue, (v, w) => LuminPackSerializer.Serialize(v, w));
        NinoCardBattleLogPayload = NinoSerializer.Serialize(CardBattleLogValue);
        LuminPackCardBattleLogPayload = BenchmarkBuffers.Serialize(CardBattleLogValue, (v, w) => LuminPackSerializer.Serialize(v, w));
        NinoIdleSavePayload = NinoSerializer.Serialize(IdleSaveValue);
        LuminPackIdleSavePayload = BenchmarkBuffers.Serialize(IdleSaveValue, (v, w) => LuminPackSerializer.Serialize(v, w));
        NinoIdleSaveOldBytes = NinoSerializer.Serialize(IdleSaveOld);
        LuminPackIdleSaveOldBytes = BenchmarkBuffers.Serialize(IdleSaveOld, (v, w) => LuminPackSerializer.Serialize(v, w));
    }

    /// <summary>Allocates reusable writer storage for both serializers.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _ninoBuffer = new NinoArrayBufferWriter(BenchmarkBuffers.Capacity);
        _luminPackBuffer = BenchmarkBuffers.Create();
    }

    /// <summary>Releases the writer storage at the end of the benchmark.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _luminPackBuffer.Dispose();
        _ninoBuffer.Dispose();
    }

    /// <summary>Writes the fixture to a reusable buffer.</summary>
    /// <returns>The written byte count.</returns>
    [Benchmark(Baseline = true), BenchmarkCategory("MediaContentSerialize")]
    public int NinoSerializeMediaContent()
    {
        _ninoBuffer.ResetWrittenCount();
        NinoSerializer.Serialize(MediaContentValue, _ninoBuffer);
        return _ninoBuffer.WrittenCount;
    }

    /// <summary>Writes the fixture to a reusable buffer.</summary>
    /// <returns>The written byte count.</returns>
    [Benchmark, BenchmarkCategory("MediaContentSerialize")]
    public int LuminPackSerializeMediaContent()
    {
        _luminPackBuffer.ResetWrittenCount();
        LuminPackSerializer.Serialize(MediaContentValue, _luminPackBuffer);
        return _luminPackBuffer.CurrentIndex;
    }

    /// <summary>Reads the fixture using its configured destination.</summary>
    /// <returns>The decoded fixture.</returns>
    [Benchmark(Baseline = true), BenchmarkCategory("MediaContentDeserialize")]
    public MediaContent NinoDeserializeMediaContent()
    {
        return NinoDeserializer.Deserialize<MediaContent>(NinoMediaContentPayload);
    }

    /// <summary>Reads the fixture using its configured destination.</summary>
    /// <returns>The decoded fixture.</returns>
    [Benchmark, BenchmarkCategory("MediaContentDeserialize")]
    public MediaContent LuminPackDeserializeMediaContent()
    {
        MediaContent result = default;
        LuminPackSerializer.Deserialize(LuminPackMediaContentPayload, ref result);
        return result;
    }

    /// <summary>Writes the fixture to a reusable buffer.</summary>
    /// <returns>The written byte count.</returns>
    [Benchmark(Baseline = true), BenchmarkCategory("StackOverflowPageSerialize")]
    public int NinoSerializeStackOverflowPage()
    {
        _ninoBuffer.ResetWrittenCount();
        NinoSerializer.Serialize(StackOverflowPageValue, _ninoBuffer);
        return _ninoBuffer.WrittenCount;
    }

    /// <summary>Writes the fixture to a reusable buffer.</summary>
    /// <returns>The written byte count.</returns>
    [Benchmark, BenchmarkCategory("StackOverflowPageSerialize")]
    public int LuminPackSerializeStackOverflowPage()
    {
        _luminPackBuffer.ResetWrittenCount();
        LuminPackSerializer.Serialize(StackOverflowPageValue, _luminPackBuffer);
        return _luminPackBuffer.CurrentIndex;
    }

    /// <summary>Reads the fixture using its configured destination.</summary>
    /// <returns>The decoded fixture.</returns>
    [Benchmark(Baseline = true), BenchmarkCategory("StackOverflowPageDeserialize")]
    public StackOverflowPage NinoDeserializeStackOverflowPage()
    {
        return NinoDeserializer.Deserialize<StackOverflowPage>(NinoStackOverflowPagePayload);
    }

    /// <summary>Reads the fixture using its configured destination.</summary>
    /// <returns>The decoded fixture.</returns>
    [Benchmark, BenchmarkCategory("StackOverflowPageDeserialize")]
    public StackOverflowPage LuminPackDeserializeStackOverflowPage()
    {
        StackOverflowPage result = default;
        LuminPackSerializer.Deserialize(LuminPackStackOverflowPagePayload, ref result);
        return result;
    }

    /// <summary>Writes the fixture to a reusable buffer.</summary>
    /// <returns>The written byte count.</returns>
    [Benchmark(Baseline = true), BenchmarkCategory("RpcCallSerialize")]
    public int NinoSerializeRpcCall()
    {
        _ninoBuffer.ResetWrittenCount();
        NinoSerializer.Serialize(RpcCallValue, _ninoBuffer);
        return _ninoBuffer.WrittenCount;
    }

    /// <summary>Writes the fixture to a reusable buffer.</summary>
    /// <returns>The written byte count.</returns>
    [Benchmark, BenchmarkCategory("RpcCallSerialize")]
    public int LuminPackSerializeRpcCall()
    {
        _luminPackBuffer.ResetWrittenCount();
        LuminPackSerializer.Serialize(RpcCallValue, _luminPackBuffer);
        return _luminPackBuffer.CurrentIndex;
    }

    /// <summary>Reads the fixture using its configured destination.</summary>
    /// <returns>The decoded fixture.</returns>
    [Benchmark(Baseline = true), BenchmarkCategory("RpcCallDeserialize")]
    public RpcCall NinoDeserializeRpcCall()
    {
        return NinoDeserializer.Deserialize<RpcCall>(NinoRpcCallPayload);
    }

    /// <summary>Reads the fixture using its configured destination.</summary>
    /// <returns>The decoded fixture.</returns>
    [Benchmark, BenchmarkCategory("RpcCallDeserialize")]
    public RpcCall LuminPackDeserializeRpcCall()
    {
        RpcCall result = default;
        LuminPackSerializer.Deserialize(LuminPackRpcCallPayload, ref result);
        return result;
    }

    /// <summary>Writes the fixture to a reusable buffer.</summary>
    /// <returns>The written byte count.</returns>
    [Benchmark(Baseline = true), BenchmarkCategory("ProfileCacheSerialize")]
    public int NinoSerializeProfileCache()
    {
        _ninoBuffer.ResetWrittenCount();
        NinoSerializer.Serialize(ProfileCacheValue, _ninoBuffer);
        return _ninoBuffer.WrittenCount;
    }

    /// <summary>Writes the fixture to a reusable buffer.</summary>
    /// <returns>The written byte count.</returns>
    [Benchmark, BenchmarkCategory("ProfileCacheSerialize")]
    public int LuminPackSerializeProfileCache()
    {
        _luminPackBuffer.Reset();
        LuminPackSerializer.Serialize(ProfileCacheValue, _luminPackBuffer);
        return _luminPackBuffer.CurrentIndex;
    }

    /// <summary>Reads the fixture using its configured destination.</summary>
    /// <returns>The decoded fixture.</returns>
    [Benchmark(Baseline = true), BenchmarkCategory("ProfileCacheDeserialize")]
    public ProfileV2 NinoDeserializeProfileCache()
    {
        return NinoDeserializer.Deserialize<ProfileV2>(NinoProfileCacheOldBytes);
    }

    /// <summary>Reads the fixture using its configured destination.</summary>
    /// <returns>The decoded fixture.</returns>
    [Benchmark, BenchmarkCategory("ProfileCacheDeserialize")]
    public ProfileV2 LuminPackDeserializeProfileCache()
    {
        ProfileV2 result = default;
        LuminPackSerializer.Deserialize(LuminPackProfileCacheOldBytes, ref result);
        return result;
    }

    /// <summary>Writes the fixture to a reusable buffer.</summary>
    /// <returns>The written byte count.</returns>
    [Benchmark(Baseline = true), BenchmarkCategory("EventStreamSerialize")]
    public int NinoSerializeEventStream()
    {
        _ninoBuffer.ResetWrittenCount();
        NinoSerializer.Serialize(EventStreamValue, _ninoBuffer);
        return _ninoBuffer.WrittenCount;
    }

    /// <summary>Writes the fixture to a reusable buffer.</summary>
    /// <returns>The written byte count.</returns>
    [Benchmark, BenchmarkCategory("EventStreamSerialize")]
    public int LuminPackSerializeEventStream()
    {
        _luminPackBuffer.ResetWrittenCount();
        LuminPackSerializer.Serialize(EventStreamValue, _luminPackBuffer);
        return _luminPackBuffer.CurrentIndex;
    }

    /// <summary>Reads the fixture using its configured destination.</summary>
    /// <returns>The decoded fixture.</returns>
    [Benchmark(Baseline = true), BenchmarkCategory("EventStreamDeserialize")]
    public EventStream NinoDeserializeEventStream()
    {
        return NinoDeserializer.Deserialize<EventStream>(NinoEventStreamPayload);
    }

    /// <summary>Reads the fixture using its configured destination.</summary>
    /// <returns>The decoded fixture.</returns>
    [Benchmark, BenchmarkCategory("EventStreamDeserialize")]
    public EventStream LuminPackDeserializeEventStream()
    {
        EventStream result = default;
        LuminPackSerializer.Deserialize(LuminPackEventStreamPayload, ref result);
        return result;
    }

    /// <summary>Writes the fixture to a reusable buffer.</summary>
    /// <returns>The written byte count.</returns>
    [Benchmark(Baseline = true), BenchmarkCategory("LogBatchSerialize")]
    public int NinoSerializeLogBatch()
    {
        _ninoBuffer.ResetWrittenCount();
        NinoSerializer.Serialize(LogBatchValue, _ninoBuffer);
        return _ninoBuffer.WrittenCount;
    }

    /// <summary>Writes the fixture to a reusable buffer.</summary>
    /// <returns>The written byte count.</returns>
    [Benchmark, BenchmarkCategory("LogBatchSerialize")]
    public int LuminPackSerializeLogBatch()
    {
        _luminPackBuffer.ResetWrittenCount();
        LuminPackSerializer.Serialize(LogBatchValue, _luminPackBuffer);
        return _luminPackBuffer.CurrentIndex;
    }

    /// <summary>Reads the fixture using its configured destination.</summary>
    /// <returns>The decoded fixture.</returns>
    [Benchmark(Baseline = true), BenchmarkCategory("LogBatchDeserialize")]
    public LogBatch NinoDeserializeLogBatch()
    {
        return NinoDeserializer.Deserialize<LogBatch>(NinoLogBatchPayload);
    }

    /// <summary>Reads the fixture using its configured destination.</summary>
    /// <returns>The decoded fixture.</returns>
    [Benchmark, BenchmarkCategory("LogBatchDeserialize")]
    public LogBatch LuminPackDeserializeLogBatch()
    {
        LogBatch result = default;
        LuminPackSerializer.Deserialize(LuminPackLogBatchPayload, ref result);
        return result;
    }

    /// <summary>Writes the fixture to a reusable buffer.</summary>
    /// <returns>The written byte count.</returns>
    [Benchmark(Baseline = true), BenchmarkCategory("StateSnapshotSerialize")]
    public int NinoSerializeStateSnapshot()
    {
        _ninoBuffer.ResetWrittenCount();
        NinoSerializer.Serialize(StateSnapshotValue, _ninoBuffer);
        return _ninoBuffer.WrittenCount;
    }

    /// <summary>Writes the fixture to a reusable buffer.</summary>
    /// <returns>The written byte count.</returns>
    [Benchmark, BenchmarkCategory("StateSnapshotSerialize")]
    public int LuminPackSerializeStateSnapshot()
    {
        _luminPackBuffer.ResetWrittenCount();
        LuminPackSerializer.Serialize(StateSnapshotValue, _luminPackBuffer);
        return _luminPackBuffer.CurrentIndex;
    }

    /// <summary>Reads the fixture using its configured destination.</summary>
    /// <returns>The decoded fixture.</returns>
    [Benchmark(Baseline = true), BenchmarkCategory("StateSnapshotDeserialize")]
    public StateSnapshot NinoDeserializeStateSnapshot()
    {
        NinoDeserializer.Deserialize(NinoStateSnapshotPayload, ref _ninoStateSnapshot);
        return _ninoStateSnapshot;
    }

    /// <summary>Reads the fixture using its configured destination.</summary>
    /// <returns>The decoded fixture.</returns>
    [Benchmark, BenchmarkCategory("StateSnapshotDeserialize")]
    public StateSnapshot LuminPackDeserializeStateSnapshot()
    {
        LuminPackSerializer.Deserialize(LuminPackStateSnapshotPayload, ref _luminPackStateSnapshot);
        return _luminPackStateSnapshot;
    }

    /// <summary>Writes the fixture to a reusable buffer.</summary>
    /// <returns>The written byte count.</returns>
    [Benchmark(Baseline = true), BenchmarkCategory("LockstepFramesSerialize")]
    public int NinoSerializeLockstepFrames()
    {
        _ninoBuffer.ResetWrittenCount();
        NinoSerializer.Serialize(LockstepFramesValue, _ninoBuffer);
        return _ninoBuffer.WrittenCount;
    }

    /// <summary>Writes the fixture to a reusable buffer.</summary>
    /// <returns>The written byte count.</returns>
    [Benchmark, BenchmarkCategory("LockstepFramesSerialize")]
    public int LuminPackSerializeLockstepFrames()
    {
        _luminPackBuffer.ResetWrittenCount();
        LuminPackSerializer.Serialize(LockstepFramesValue, _luminPackBuffer);
        return _luminPackBuffer.CurrentIndex;
    }

    /// <summary>Reads the fixture using its configured destination.</summary>
    /// <returns>The decoded fixture.</returns>
    [Benchmark(Baseline = true), BenchmarkCategory("LockstepFramesDeserialize")]
    public LockstepFrames NinoDeserializeLockstepFrames()
    {
        return NinoDeserializer.Deserialize<LockstepFrames>(NinoLockstepFramesPayload);
    }

    /// <summary>Reads the fixture using its configured destination.</summary>
    /// <returns>The decoded fixture.</returns>
    [Benchmark, BenchmarkCategory("LockstepFramesDeserialize")]
    public LockstepFrames LuminPackDeserializeLockstepFrames()
    {
        LockstepFrames result = default;
        LuminPackSerializer.Deserialize(LuminPackLockstepFramesPayload, ref result);
        return result;
    }

    /// <summary>Writes the fixture to a reusable buffer.</summary>
    /// <returns>The written byte count.</returns>
    [Benchmark(Baseline = true), BenchmarkCategory("AoiUpdateSerialize")]
    public int NinoSerializeAoiUpdate()
    {
        _ninoBuffer.ResetWrittenCount();
        NinoSerializer.Serialize(AoiUpdateValue, _ninoBuffer);
        return _ninoBuffer.WrittenCount;
    }

    /// <summary>Writes the fixture to a reusable buffer.</summary>
    /// <returns>The written byte count.</returns>
    [Benchmark, BenchmarkCategory("AoiUpdateSerialize")]
    public int LuminPackSerializeAoiUpdate()
    {
        _luminPackBuffer.ResetWrittenCount();
        LuminPackSerializer.Serialize(AoiUpdateValue, _luminPackBuffer);
        return _luminPackBuffer.CurrentIndex;
    }

    /// <summary>Reads the fixture using its configured destination.</summary>
    /// <returns>The decoded fixture.</returns>
    [Benchmark(Baseline = true), BenchmarkCategory("AoiUpdateDeserialize")]
    public AoiUpdate NinoDeserializeAoiUpdate()
    {
        return NinoDeserializer.Deserialize<AoiUpdate>(NinoAoiUpdatePayload);
    }

    /// <summary>Reads the fixture using its configured destination.</summary>
    /// <returns>The decoded fixture.</returns>
    [Benchmark, BenchmarkCategory("AoiUpdateDeserialize")]
    public AoiUpdate LuminPackDeserializeAoiUpdate()
    {
        AoiUpdate result = default;
        LuminPackSerializer.Deserialize(LuminPackAoiUpdatePayload, ref result);
        return result;
    }

    /// <summary>Writes the fixture to a reusable buffer.</summary>
    /// <returns>The written byte count.</returns>
    [Benchmark(Baseline = true), BenchmarkCategory("WorldRegionSerialize")]
    public int NinoSerializeWorldRegion()
    {
        _ninoBuffer.ResetWrittenCount();
        NinoSerializer.Serialize(WorldRegionValue, _ninoBuffer);
        return _ninoBuffer.WrittenCount;
    }

    /// <summary>Writes the fixture to a reusable buffer.</summary>
    /// <returns>The written byte count.</returns>
    [Benchmark, BenchmarkCategory("WorldRegionSerialize")]
    public int LuminPackSerializeWorldRegion()
    {
        _luminPackBuffer.Reset();
        LuminPackSerializer.Serialize(WorldRegionValue, _luminPackBuffer);
        return _luminPackBuffer.CurrentIndex;
    }

    /// <summary>Reads the fixture using its configured destination.</summary>
    /// <returns>The decoded fixture.</returns>
    [Benchmark(Baseline = true), BenchmarkCategory("WorldRegionDeserialize")]
    public WorldRegion NinoDeserializeWorldRegion()
    {
        return NinoDeserializer.Deserialize<WorldRegion>(NinoWorldRegionPayload);
    }

    /// <summary>Reads the fixture using its configured destination.</summary>
    /// <returns>The decoded fixture.</returns>
    [Benchmark, BenchmarkCategory("WorldRegionDeserialize")]
    public WorldRegion LuminPackDeserializeWorldRegion()
    {
        WorldRegion result = default;
        LuminPackSerializer.Deserialize(LuminPackWorldRegionPayload, ref result);
        return result;
    }

    /// <summary>Writes the fixture to a reusable buffer.</summary>
    /// <returns>The written byte count.</returns>
    [Benchmark(Baseline = true), BenchmarkCategory("CardBattleLogSerialize")]
    public int NinoSerializeCardBattleLog()
    {
        _ninoBuffer.ResetWrittenCount();
        NinoSerializer.Serialize(CardBattleLogValue, _ninoBuffer);
        return _ninoBuffer.WrittenCount;
    }

    /// <summary>Writes the fixture to a reusable buffer.</summary>
    /// <returns>The written byte count.</returns>
    [Benchmark, BenchmarkCategory("CardBattleLogSerialize")]
    public int LuminPackSerializeCardBattleLog()
    {
        _luminPackBuffer.Reset();
        LuminPackSerializer.Serialize(CardBattleLogValue, _luminPackBuffer);
        return _luminPackBuffer.CurrentIndex;
    }

    /// <summary>Reads the fixture using its configured destination.</summary>
    /// <returns>The decoded fixture.</returns>
    [Benchmark(Baseline = true), BenchmarkCategory("CardBattleLogDeserialize")]
    public CardBattleLog NinoDeserializeCardBattleLog()
    {
        return NinoDeserializer.Deserialize<CardBattleLog>(NinoCardBattleLogPayload);
    }

    /// <summary>Reads the fixture using its configured destination.</summary>
    /// <returns>The decoded fixture.</returns>
    [Benchmark, BenchmarkCategory("CardBattleLogDeserialize")]
    public CardBattleLog LuminPackDeserializeCardBattleLog()
    {
        CardBattleLog result = default;
        LuminPackSerializer.Deserialize(LuminPackCardBattleLogPayload, ref result);
        return result;
    }

    /// <summary>Writes the fixture to a reusable buffer.</summary>
    /// <returns>The written byte count.</returns>
    [Benchmark(Baseline = true), BenchmarkCategory("IdleSaveSerialize")]
    public int NinoSerializeIdleSave()
    {
        _ninoBuffer.ResetWrittenCount();
        NinoSerializer.Serialize(IdleSaveValue, _ninoBuffer);
        return _ninoBuffer.WrittenCount;
    }

    /// <summary>Writes the fixture to a reusable buffer.</summary>
    /// <returns>The written byte count.</returns>
    [Benchmark, BenchmarkCategory("IdleSaveSerialize")]
    public int LuminPackSerializeIdleSave()
    {
        _luminPackBuffer.Reset();
        LuminPackSerializer.Serialize(IdleSaveValue, _luminPackBuffer);
        return _luminPackBuffer.CurrentIndex;
    }

    /// <summary>Reads the fixture using its configured destination.</summary>
    /// <returns>The decoded fixture.</returns>
    [Benchmark(Baseline = true), BenchmarkCategory("IdleSaveDeserialize")]
    public IdleSaveV2 NinoDeserializeIdleSave()
    {
        NinoDeserializer.Deserialize(NinoIdleSaveOldBytes, ref _ninoIdleSave);
        return _ninoIdleSave;
    }

    /// <summary>Reads the fixture using its configured destination.</summary>
    /// <returns>The decoded fixture.</returns>
    [Benchmark, BenchmarkCategory("IdleSaveDeserialize")]
    public IdleSaveV2 LuminPackDeserializeIdleSave()
    {
        LuminPackSerializer.Deserialize(LuminPackIdleSaveOldBytes, ref _luminPackIdleSave);
        return _luminPackIdleSave;
    }
}
