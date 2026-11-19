// Adapted from the Nino benchmark suite. See LICENSE.Nino.txt.
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using LuminPack.Utility;
using Nino.Core;

#nullable disable
namespace LuminPack.NinoBenchmark.AppData;

/// <summary>Compares LuminPack and Nino on the same seeded AppData fixtures.</summary>
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[MemoryDiagnoser]
public class AppDataTest
{
    private NinoArrayBufferWriter _ninoBuffer;
    private LuminBufferWriter _luminPackBuffer;

    private static readonly Player[] Players = AppDataValues.Players;
    private static readonly Zoo[] Zoos = AppDataValues.Zoos;
    private static readonly OrderBook[] OrderBooks = AppDataValues.OrderBooks;
    private static readonly Article[] Articles = AppDataValues.Articles;
    private static readonly Mesh[] Meshes = AppDataValues.Meshes;
    private static readonly Samples[] SampleSets = AppDataValues.SampleSets;
    private static readonly Lookup[] Lookups = AppDataValues.Lookups;
    private static readonly Event[] Events = AppDataValues.Events;

    private static readonly byte[] NinoPlayersBytes;
    private static readonly byte[] LuminPackPlayersBytes;
    private static readonly byte[] NinoZoosBytes;
    private static readonly byte[] LuminPackZoosBytes;
    private static readonly byte[] NinoOrderBooksBytes;
    private static readonly byte[] LuminPackOrderBooksBytes;
    private static readonly byte[] NinoArticlesBytes;
    private static readonly byte[] LuminPackArticlesBytes;
    private static readonly byte[] NinoMeshesBytes;
    private static readonly byte[] LuminPackMeshesBytes;
    private static readonly byte[] NinoSampleSetsBytes;
    private static readonly byte[] LuminPackSampleSetsBytes;
    private static readonly byte[] NinoLookupsBytes;
    private static readonly byte[] LuminPackLookupsBytes;
    private static readonly byte[] NinoEventsBytes;
    private static readonly byte[] LuminPackEventsBytes;

    /// <summary>Encodes each fixture before the timed operations.</summary>
    static AppDataTest()
    {
        NinoPlayersBytes = NinoSerializer.Serialize(Players);
        LuminPackPlayersBytes = BenchmarkBuffers.Serialize(Players, (v, w) => LuminPackSerializer.Serialize(v, w));
        NinoZoosBytes = NinoSerializer.Serialize(Zoos);
        LuminPackZoosBytes = BenchmarkBuffers.Serialize(Zoos, (v, w) => LuminPackSerializer.Serialize(v, w));
        NinoOrderBooksBytes = NinoSerializer.Serialize(OrderBooks);
        LuminPackOrderBooksBytes = BenchmarkBuffers.Serialize(OrderBooks, (v, w) => LuminPackSerializer.Serialize(v, w));
        NinoArticlesBytes = NinoSerializer.Serialize(Articles);
        LuminPackArticlesBytes = BenchmarkBuffers.Serialize(Articles, (v, w) => LuminPackSerializer.Serialize(v, w));
        NinoMeshesBytes = NinoSerializer.Serialize(Meshes);
        LuminPackMeshesBytes = BenchmarkBuffers.Serialize(Meshes, (v, w) => LuminPackSerializer.Serialize(v, w));
        NinoSampleSetsBytes = NinoSerializer.Serialize(SampleSets);
        LuminPackSampleSetsBytes = BenchmarkBuffers.Serialize(SampleSets, (v, w) => LuminPackSerializer.Serialize(v, w));
        NinoLookupsBytes = NinoSerializer.Serialize(Lookups);
        LuminPackLookupsBytes = BenchmarkBuffers.Serialize(Lookups, (v, w) => LuminPackSerializer.Serialize(v, w));
        NinoEventsBytes = NinoSerializer.Serialize(Events);
        LuminPackEventsBytes = BenchmarkBuffers.Serialize(Events, (v, w) => LuminPackSerializer.Serialize(v, w));
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
    [Benchmark(Baseline = true), BenchmarkCategory("PlayersSerialize")]
    public int NinoSerializePlayers()
    {
        _ninoBuffer.ResetWrittenCount();
        NinoSerializer.Serialize(Players, _ninoBuffer);
        return _ninoBuffer.WrittenCount;
    }

    /// <summary>Writes the fixture to a reusable buffer.</summary>
    /// <returns>The written byte count.</returns>
    [Benchmark, BenchmarkCategory("PlayersSerialize")]
    public int LuminPackSerializePlayers()
    {
        _luminPackBuffer.ResetWrittenCount();
        LuminPackSerializer.Serialize(Players, _luminPackBuffer);
        return _luminPackBuffer.CurrentIndex;
    }

    /// <summary>Reads the fixture using its configured destination.</summary>
    /// <returns>The decoded fixture.</returns>
    [Benchmark(Baseline = true), BenchmarkCategory("PlayersDeserialize")]
    public Player[] NinoDeserializePlayers()
    {
        return NinoDeserializer.Deserialize<Player[]>(NinoPlayersBytes);
    }

    /// <summary>Reads the fixture using its configured destination.</summary>
    /// <returns>The decoded fixture.</returns>
    [Benchmark, BenchmarkCategory("PlayersDeserialize")]
    public Player[] LuminPackDeserializePlayers()
    {
        Player[] result = default;
        LuminPackSerializer.Deserialize(LuminPackPlayersBytes, ref result);
        return result;
    }

    /// <summary>Writes the fixture to a reusable buffer.</summary>
    /// <returns>The written byte count.</returns>
    [Benchmark(Baseline = true), BenchmarkCategory("ZoosSerialize")]
    public int NinoSerializeZoos()
    {
        _ninoBuffer.ResetWrittenCount();
        NinoSerializer.Serialize(Zoos, _ninoBuffer);
        return _ninoBuffer.WrittenCount;
    }

    /// <summary>Writes the fixture to a reusable buffer.</summary>
    /// <returns>The written byte count.</returns>
    [Benchmark, BenchmarkCategory("ZoosSerialize")]
    public int LuminPackSerializeZoos()
    {
        _luminPackBuffer.ResetWrittenCount();
        LuminPackSerializer.Serialize(Zoos, _luminPackBuffer);
        return _luminPackBuffer.CurrentIndex;
    }

    /// <summary>Reads the fixture using its configured destination.</summary>
    /// <returns>The decoded fixture.</returns>
    [Benchmark(Baseline = true), BenchmarkCategory("ZoosDeserialize")]
    public Zoo[] NinoDeserializeZoos()
    {
        return NinoDeserializer.Deserialize<Zoo[]>(NinoZoosBytes);
    }

    /// <summary>Reads the fixture using its configured destination.</summary>
    /// <returns>The decoded fixture.</returns>
    [Benchmark, BenchmarkCategory("ZoosDeserialize")]
    public Zoo[] LuminPackDeserializeZoos()
    {
        Zoo[] result = default;
        LuminPackSerializer.Deserialize(LuminPackZoosBytes, ref result);
        return result;
    }

    /// <summary>Writes the fixture to a reusable buffer.</summary>
    /// <returns>The written byte count.</returns>
    [Benchmark(Baseline = true), BenchmarkCategory("OrderBooksSerialize")]
    public int NinoSerializeOrderBooks()
    {
        _ninoBuffer.ResetWrittenCount();
        NinoSerializer.Serialize(OrderBooks, _ninoBuffer);
        return _ninoBuffer.WrittenCount;
    }

    /// <summary>Writes the fixture to a reusable buffer.</summary>
    /// <returns>The written byte count.</returns>
    [Benchmark, BenchmarkCategory("OrderBooksSerialize")]
    public int LuminPackSerializeOrderBooks()
    {
        _luminPackBuffer.ResetWrittenCount();
        LuminPackSerializer.Serialize(OrderBooks, _luminPackBuffer);
        return _luminPackBuffer.CurrentIndex;
    }

    /// <summary>Reads the fixture using its configured destination.</summary>
    /// <returns>The decoded fixture.</returns>
    [Benchmark(Baseline = true), BenchmarkCategory("OrderBooksDeserialize")]
    public OrderBook[] NinoDeserializeOrderBooks()
    {
        return NinoDeserializer.Deserialize<OrderBook[]>(NinoOrderBooksBytes);
    }

    /// <summary>Reads the fixture using its configured destination.</summary>
    /// <returns>The decoded fixture.</returns>
    [Benchmark, BenchmarkCategory("OrderBooksDeserialize")]
    public OrderBook[] LuminPackDeserializeOrderBooks()
    {
        OrderBook[] result = default;
        LuminPackSerializer.Deserialize(LuminPackOrderBooksBytes, ref result);
        return result;
    }

    /// <summary>Writes the fixture to a reusable buffer.</summary>
    /// <returns>The written byte count.</returns>
    [Benchmark(Baseline = true), BenchmarkCategory("ArticlesSerialize")]
    public int NinoSerializeArticles()
    {
        _ninoBuffer.ResetWrittenCount();
        NinoSerializer.Serialize(Articles, _ninoBuffer);
        return _ninoBuffer.WrittenCount;
    }

    /// <summary>Writes the fixture to a reusable buffer.</summary>
    /// <returns>The written byte count.</returns>
    [Benchmark, BenchmarkCategory("ArticlesSerialize")]
    public int LuminPackSerializeArticles()
    {
        _luminPackBuffer.ResetWrittenCount();
        LuminPackSerializer.Serialize(Articles, _luminPackBuffer);
        return _luminPackBuffer.CurrentIndex;
    }

    /// <summary>Reads the fixture using its configured destination.</summary>
    /// <returns>The decoded fixture.</returns>
    [Benchmark(Baseline = true), BenchmarkCategory("ArticlesDeserialize")]
    public Article[] NinoDeserializeArticles()
    {
        return NinoDeserializer.Deserialize<Article[]>(NinoArticlesBytes);
    }

    /// <summary>Reads the fixture using its configured destination.</summary>
    /// <returns>The decoded fixture.</returns>
    [Benchmark, BenchmarkCategory("ArticlesDeserialize")]
    public Article[] LuminPackDeserializeArticles()
    {
        Article[] result = default;
        LuminPackSerializer.Deserialize(LuminPackArticlesBytes, ref result);
        return result;
    }

    /// <summary>Writes the fixture to a reusable buffer.</summary>
    /// <returns>The written byte count.</returns>
    [Benchmark(Baseline = true), BenchmarkCategory("MeshesSerialize")]
    public int NinoSerializeMeshes()
    {
        _ninoBuffer.ResetWrittenCount();
        NinoSerializer.Serialize(Meshes, _ninoBuffer);
        return _ninoBuffer.WrittenCount;
    }

    /// <summary>Writes the fixture to a reusable buffer.</summary>
    /// <returns>The written byte count.</returns>
    [Benchmark, BenchmarkCategory("MeshesSerialize")]
    public int LuminPackSerializeMeshes()
    {
        _luminPackBuffer.ResetWrittenCount();
        LuminPackSerializer.Serialize(Meshes, _luminPackBuffer);
        return _luminPackBuffer.CurrentIndex;
    }

    /// <summary>Reads the fixture using its configured destination.</summary>
    /// <returns>The decoded fixture.</returns>
    [Benchmark(Baseline = true), BenchmarkCategory("MeshesDeserialize")]
    public Mesh[] NinoDeserializeMeshes()
    {
        return NinoDeserializer.Deserialize<Mesh[]>(NinoMeshesBytes);
    }

    /// <summary>Reads the fixture using its configured destination.</summary>
    /// <returns>The decoded fixture.</returns>
    [Benchmark, BenchmarkCategory("MeshesDeserialize")]
    public Mesh[] LuminPackDeserializeMeshes()
    {
        Mesh[] result = default;
        LuminPackSerializer.Deserialize(LuminPackMeshesBytes, ref result);
        return result;
    }

    /// <summary>Writes the fixture to a reusable buffer.</summary>
    /// <returns>The written byte count.</returns>
    [Benchmark(Baseline = true), BenchmarkCategory("SampleSetsSerialize")]
    public int NinoSerializeSampleSets()
    {
        _ninoBuffer.ResetWrittenCount();
        NinoSerializer.Serialize(SampleSets, _ninoBuffer);
        return _ninoBuffer.WrittenCount;
    }

    /// <summary>Writes the fixture to a reusable buffer.</summary>
    /// <returns>The written byte count.</returns>
    [Benchmark, BenchmarkCategory("SampleSetsSerialize")]
    public int LuminPackSerializeSampleSets()
    {
        _luminPackBuffer.ResetWrittenCount();
        LuminPackSerializer.Serialize(SampleSets, _luminPackBuffer);
        return _luminPackBuffer.CurrentIndex;
    }

    /// <summary>Reads the fixture using its configured destination.</summary>
    /// <returns>The decoded fixture.</returns>
    [Benchmark(Baseline = true), BenchmarkCategory("SampleSetsDeserialize")]
    public Samples[] NinoDeserializeSampleSets()
    {
        return NinoDeserializer.Deserialize<Samples[]>(NinoSampleSetsBytes);
    }

    /// <summary>Reads the fixture using its configured destination.</summary>
    /// <returns>The decoded fixture.</returns>
    [Benchmark, BenchmarkCategory("SampleSetsDeserialize")]
    public Samples[] LuminPackDeserializeSampleSets()
    {
        Samples[] result = default;
        LuminPackSerializer.Deserialize(LuminPackSampleSetsBytes, ref result);
        return result;
    }

    /// <summary>Writes the fixture to a reusable buffer.</summary>
    /// <returns>The written byte count.</returns>
    [Benchmark(Baseline = true), BenchmarkCategory("LookupsSerialize")]
    public int NinoSerializeLookups()
    {
        _ninoBuffer.ResetWrittenCount();
        NinoSerializer.Serialize(Lookups, _ninoBuffer);
        return _ninoBuffer.WrittenCount;
    }

    /// <summary>Writes the fixture to a reusable buffer.</summary>
    /// <returns>The written byte count.</returns>
    [Benchmark, BenchmarkCategory("LookupsSerialize")]
    public int LuminPackSerializeLookups()
    {
        _luminPackBuffer.ResetWrittenCount();
        LuminPackSerializer.Serialize(Lookups, _luminPackBuffer);
        return _luminPackBuffer.CurrentIndex;
    }

    /// <summary>Reads the fixture using its configured destination.</summary>
    /// <returns>The decoded fixture.</returns>
    [Benchmark(Baseline = true), BenchmarkCategory("LookupsDeserialize")]
    public Lookup[] NinoDeserializeLookups()
    {
        return NinoDeserializer.Deserialize<Lookup[]>(NinoLookupsBytes);
    }

    /// <summary>Reads the fixture using its configured destination.</summary>
    /// <returns>The decoded fixture.</returns>
    [Benchmark, BenchmarkCategory("LookupsDeserialize")]
    public Lookup[] LuminPackDeserializeLookups()
    {
        Lookup[] result = default;
        LuminPackSerializer.Deserialize(LuminPackLookupsBytes, ref result);
        return result;
    }

    /// <summary>Writes the fixture to a reusable buffer.</summary>
    /// <returns>The written byte count.</returns>
    [Benchmark(Baseline = true), BenchmarkCategory("EventsSerialize")]
    public int NinoSerializeEvents()
    {
        _ninoBuffer.ResetWrittenCount();
        NinoSerializer.Serialize(Events, _ninoBuffer);
        return _ninoBuffer.WrittenCount;
    }

    /// <summary>Writes the fixture to a reusable buffer.</summary>
    /// <returns>The written byte count.</returns>
    [Benchmark, BenchmarkCategory("EventsSerialize")]
    public int LuminPackSerializeEvents()
    {
        _luminPackBuffer.ResetWrittenCount();
        LuminPackSerializer.Serialize(Events, _luminPackBuffer);
        return _luminPackBuffer.CurrentIndex;
    }

    /// <summary>Reads the fixture using its configured destination.</summary>
    /// <returns>The decoded fixture.</returns>
    [Benchmark(Baseline = true), BenchmarkCategory("EventsDeserialize")]
    public Event[] NinoDeserializeEvents()
    {
        return NinoDeserializer.Deserialize<Event[]>(NinoEventsBytes);
    }

    /// <summary>Reads the fixture using its configured destination.</summary>
    /// <returns>The decoded fixture.</returns>
    [Benchmark, BenchmarkCategory("EventsDeserialize")]
    public Event[] LuminPackDeserializeEvents()
    {
        Event[] result = default;
        LuminPackSerializer.Deserialize(LuminPackEventsBytes, ref result);
        return result;
    }
}
