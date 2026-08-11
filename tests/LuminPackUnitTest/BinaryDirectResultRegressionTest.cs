using LuminPack;
using LuminPack.Attribute;
using LuminPack.Code;
using LuminPack.Core;
using LuminPack.Generated;
using LuminPack.Utility;


namespace LuminPackUnitTest;

[LuminPackable]
public partial class BinaryDirectNestedModel
{
    public int Value;
    public List<int>? Numbers;
    public Dictionary<int, string>? Labels;
}

[LuminPackable]
public partial class BinaryDirectEnvelope<T>
{
    public long Sequence;
    public T? Payload;
    public int[]? Samples;
}

[LuminPackable]
public partial class BinaryDirectRootModel
{
    public int Id;
    public BinaryDirectNestedModel? Nested;
    public BinaryDirectEnvelope<BinaryDirectNestedModel>? Envelope;
    public string? Tail;
}

[LuminPackable]
public partial class BinaryDirectReferenceElement
{
    public int Value;
}

[LuminPackable]
public partial class BinaryDirectReferenceCollectionRoot
{
    public List<BinaryDirectReferenceElement?>? Items;
    public BinaryDirectReferenceElement?[]? Elements;
}

[LuminPackable]
public partial class BinaryDirectDictionaryMutationValue
{
    public Lazy<int>? MutationTrigger;
    public int Value;
}

internal static class BinaryDirectResultRegressionTest
{
    public static void Run(List<string> results)
    {
        RunCase(results, nameof(SmallNestedGenericAndFreshCollectionsRoundTrip),
            SmallNestedGenericAndFreshCollectionsRoundTrip);
        RunCase(results, nameof(PartiallyReadFreshResultIsNotPublished),
            PartiallyReadFreshResultIsNotPublished);
        RunCase(results, nameof(ArraySizeofMatchesSerializedLength),
            ArraySizeofMatchesSerializedLength);
        RunCase(results, nameof(NullClassElementsConsumeTheirWireHeader),
            NullClassElementsConsumeTheirWireHeader);
        RunCase(results, nameof(FreshDictionaryBuilderPreservesDictionarySemantics),
            FreshDictionaryBuilderPreservesDictionarySemantics);
        RunCase(results, nameof(UnmanagedDictionaryPairsRemainCompact),
            UnmanagedDictionaryPairsRemainCompact);
        RunCase(results, nameof(DictionaryArchitectureFallbacksRoundTrip),
            DictionaryArchitectureFallbacksRoundTrip);
        RunCase(results, nameof(DictionaryDenseMutationIsRejected),
            DictionaryDenseMutationIsRejected);
    }

    public static void VerifyDictionaryLayoutRegression()
    {
        FreshDictionaryBuilderPreservesDictionarySemantics();
        UnmanagedDictionaryPairsRemainCompact();
        DictionaryArchitectureFallbacksRoundTrip();
        DictionaryDenseMutationIsRejected();
    }

    private static void SmallNestedGenericAndFreshCollectionsRoundTrip()
    {
        var nested = new BinaryDirectNestedModel
        {
            Value = 17,Numbers = [1, 2, 3, 5, 8],
            Labels = new Dictionary<int, string> { [1] = "one", [2] = "two" }
        };
        var value = new BinaryDirectRootModel
        {
            Id = 42,Nested = nested,
            Envelope = new BinaryDirectEnvelope<BinaryDirectNestedModel>
            {Sequence = 99,
                Payload = new BinaryDirectNestedModel
                {
                    Value = 23,Numbers = [13, 21],
                    Labels = new Dictionary<int, string> { [3] = "three" }
                },
                Samples = [4, 9, 16, 25]
            },
            Tail = "complete"
        };

        var payload = LuminPackSerializer.Serialize(value);
        var result = LuminPackSerializer.Deserialize<BinaryDirectRootModel>(payload);

        Assert(result is not null && result.Id == 42 && result.Tail == "complete",
            "Small direct-result DTO did not round-trip scalar fields.");
        Assert(result.Nested is not null && result.Nested.Value == 17 &&result.Nested.Numbers is not null && result.Nested.Numbers.SequenceEqual([1, 2, 3, 5, 8]) &&result.Nested.Labels is not null && result.Nested.Labels[2] == "two",
            "named nested direct-result DTO did not round-trip fresh collections.");
        Assert(result.Envelope is not null && result.Envelope.Sequence == 99 &&result.Envelope.Samples is not null && result.Envelope.Samples.SequenceEqual([4, 9, 16, 25]) &&result.Envelope.Payload is not null && result.Envelope.Payload.Value == 23 &&result.Envelope.Payload.Numbers is not null && result.Envelope.Payload.Numbers.SequenceEqual([13, 21]),
            "Generic direct-result DTO on fresh unmanaged array did not round-trip.");
    }

    private static void PartiallyReadFreshResultIsNotPublished()
    {
        var payload = LuminPackSerializer.Serialize(new BinaryDirectRootModel
        {
            Id = 7,Nested = new BinaryDirectNestedModel { Value = 11 },
            Envelope = new BinaryDirectEnvelope<BinaryDirectNestedModel>
            {Sequence = 13,
                Payload = new BinaryDirectNestedModel { Value = 17 },
                Samples = [19, 23]
            },
            Tail = "must-throw"
        });
        Array.Resize(ref payload, payload.Length - 1);

        var original = new BinaryDirectRootModel { Id = 1234, Tail = "original" };
        BinaryDirectRootModel? target = original;
        ReadOnlySpan<byte> span = payload;
        var reader = new LuminPackReader(ref span);
        var threw = false;
        try
        {
            global::LuminPack.Generated.LuminPackExtensions_LuminPackUnitTest
                .ReadValue(ref reader, ref target);
        }
        catch (Exception)
        {
            threw = true;
        }

        Assert(threw, "Truncated direct-result payload unexpectedly completed.");
        Assert(ReferenceEquals(target, original) && original.Id == 1234 && original.Tail == "original",
            "Binary direct-result parsing published a partially initialized object after failure.");
    }

    private static void ArraySizeofMatchesSerializedLength()
    {
        int[] unmanaged = [1, 2, 3, 5, 8];
        string?[] managed = ["alpha", null, "gamma"];

        Assert(LuminPackSerializer.Sizeof(unmanaged) == LuminPackSerializer.Serialize(unmanaged).Length,
            "Unmanaged array CalculateOffset did not match serialized length.");
        Assert(LuminPackSerializer.Sizeof(managed) == LuminPackSerializer.Serialize(managed).Length,
            "Reference array CalculateOffset did not match serialized length.");
    }

    private static void NullClassElementsConsumeTheirWireHeader()
    {
        var value = new BinaryDirectReferenceCollectionRoot
        {
            Items = [new BinaryDirectReferenceElement { Value = 11 }, null,
                new BinaryDirectReferenceElement { Value = 13 }],
            Elements = [new BinaryDirectReferenceElement { Value = 17 }, null,
                new BinaryDirectReferenceElement { Value = 19 }]
        };

        var payload = LuminPackSerializer.Serialize(value);
        var result = LuminPackSerializer.Deserialize<BinaryDirectReferenceCollectionRoot>(payload);

        Assert(result?.Items is { Count: 3 } && result.Items[0]?.Value == 11 &&
               result.Items[1] is null && result.Items[2]?.Value == 13,
            "A null class element corrupted the following generated List element.");
        Assert(result?.Elements is { Length: 3 } && result.Elements[0]?.Value == 17 &&
               result.Elements[1] is null && result.Elements[2]?.Value == 19,
            "A null class element corrupted the following generated array element.");
    }

    private static void UnmanagedDictionaryPairsRemainCompact()
    {
        var byteLong = new Dictionary<byte, long> { [7] = 0x0102030405060708L };
        var byteLongPayload = LuminPackSerializer.Serialize(byteLong);
        var byteLongResult = LuminPackSerializer.Deserialize<Dictionary<byte, long>>(byteLongPayload);
        Assert(byteLongPayload.Length == sizeof(int) + sizeof(byte) + sizeof(long) &&
               byteLongResult is { Count: 1 } && byteLongResult[7] == 0x0102030405060708L,
            "Dictionary<byte, long> introduced padding between an unmanaged key/value pair.");

        var longByte = new Dictionary<long, byte> { [0x0102030405060708L] = 9 };
        var longBytePayload = LuminPackSerializer.Serialize(longByte);
        var longByteResult = LuminPackSerializer.Deserialize<Dictionary<long, byte>>(longBytePayload);
        Assert(longBytePayload.Length == sizeof(int) + sizeof(long) + sizeof(byte) &&
               longByteResult is { Count: 1 } && longByteResult[0x0102030405060708L] == 9,
            "Dictionary<long, byte> introduced trailing padding in an unmanaged key/value pair.");
    }

    private static void DictionaryArchitectureFallbacksRoundTrip()
    {
        var value = new Dictionary<int, long>
        {
            [7] = long.MinValue,
            [11] = 42,
            [97] = long.MaxValue
        };

        var payload = LuminPackSerializer.Serialize(value);
        var binaryResult = LuminPackSerializer.Deserialize<Dictionary<int, long>>(payload);
        Assert(binaryResult is { Count: 3 } && binaryResult[7] == long.MinValue &&
               binaryResult[11] == 42 && binaryResult[97] == long.MaxValue,
            "The architecture-safe binary Dictionary path did not round-trip.");

        Assert(binaryResult!.Remove(11),
            "The architecture-safe binary Dictionary result could not remove an entry.");
        binaryResult.Add(13, -13);
        Assert(binaryResult.Count == 3 && binaryResult[13] == -13,
            "The architecture-safe binary Dictionary result did not preserve public mutation semantics.");

        var managedSparse = new Dictionary<string, long>
        {
            ["alpha"] = 1,
            ["removed"] = 2,
            ["omega"] = 3
        };
        Assert(managedSparse.Remove("removed"),
            "The managed sparse Dictionary fixture did not create a free entry.");
        var managedSparsePayload = LuminPackSerializer.Serialize(managedSparse);
        var managedSparseResult = LuminPackSerializer.Deserialize<Dictionary<string, long>>(managedSparsePayload);
        Assert(managedSparseResult is { Count: 2 } && managedSparseResult["alpha"] == 1 &&
               managedSparseResult["omega"] == 3 && !managedSparseResult.ContainsKey("removed"),
            "The managed Dictionary NoInlining sparse fallback did not preserve its wire values.");

        var json = LuminPackSerializer.SerializeJson(value);
        var jsonResult = LuminPackSerializer.DeserializeJson<Dictionary<int, long>>(json);
        Assert(jsonResult is { Count: 3 } && jsonResult[7] == long.MinValue &&
               jsonResult[11] == 42 && jsonResult[97] == long.MaxValue,
            "The architecture-safe JSON Dictionary path did not round-trip.");
    }

    private static void DictionaryDenseMutationIsRejected()
    {
        if (IntPtr.Size != 8)
            return;

        var removeDictionary = new Dictionary<int, BinaryDirectDictionaryMutationValue>();
        removeDictionary.Add(1, new BinaryDirectDictionaryMutationValue
        {
            Value = 11,
            MutationTrigger = new Lazy<int>(() =>
            {
                removeDictionary.Remove(2);
                return 101;
            })
        });
        removeDictionary.Add(2, new BinaryDirectDictionaryMutationValue { Value = 22 });
        AssertDictionaryMutationThrows(removeDictionary,
            "Dense Dictionary serialization did not reject a Remove that left a free entry.");

        var clearDictionary = new Dictionary<int, BinaryDirectDictionaryMutationValue>();
        clearDictionary.Add(1, new BinaryDirectDictionaryMutationValue
        {
            Value = 31,
            MutationTrigger = new Lazy<int>(() =>
            {
                clearDictionary.Clear();
                return 202;
            })
        });
        clearDictionary.Add(2, new BinaryDirectDictionaryMutationValue { Value = 32 });
        AssertDictionaryMutationThrows(clearDictionary,
            "Dense Dictionary serialization did not reject Clear after the version snapshot.");
    }

    private static void AssertDictionaryMutationThrows(
        Dictionary<int, BinaryDirectDictionaryMutationValue> dictionary,
        string message)
    {
        LuminBufferWriter buffer = LuminBufferWriterPool.Rent();
        try
        {
            var writer = new LuminPackWriter(buffer);
            bool threw = false;
            try
            {
                global::LuminPack.Generated.LuminPackExtensions_LuminPackUnitTest
                    .WriteValue(ref writer, in dictionary);
            }
            catch (InvalidOperationException)
            {
                threw = true;
            }

            Assert(threw, message);
        }
        finally
        {
            LuminBufferWriterPool.Return(buffer);
        }
    }

    private static void FreshDictionaryBuilderPreservesDictionarySemantics()
    {
        var value = new Dictionary<int, string>();
        for (int i = 0; i < 256; i++)
        {
            value.Add(i * 257, $"value-{i}");
        }

        var payload = LuminPackSerializer.Serialize(value);
        var result = LuminPackSerializer.Deserialize<Dictionary<int, string>>(payload);
        Assert(result is { Count: 256 } && result.All(pair => value[pair.Key] == pair.Value),
            "The fresh Dictionary bucket builder lost colliding entries.");

        if (IntPtr.Size == 8)
        {
            var resultView = LuminPackMarshal.GetDictionaryView(result!);
            Assert(resultView._count == result.Count && resultView._freeList == -1 &&
                   resultView._freeCount == 0 && resultView._version == result.Count,
                "The fresh Dictionary builder did not publish the runtime's count/free-list/version state.");
            Assert(resultView._fastModMultiplier != 0,
                "The modern Dictionary layout did not expose its cached fast-mod multiplier.");
        }

        Assert(result.Remove(257),
            "A freshly rebuilt Dictionary could not remove an existing entry.");

        var sparsePayload = LuminPackSerializer.Serialize(result);
        var sparseResult = LuminPackSerializer.Deserialize<Dictionary<int, string>>(sparsePayload);
        Assert(sparseResult is { Count: 255 } && !sparseResult.ContainsKey(257) &&
               sparseResult.All(pair => result[pair.Key] == pair.Value),
            "The managed Dictionary sparse serialization fallback lost or reordered entries.");

        result.Add(-1, "replacement");
        Assert(result.Count == 256 && result[-1] == "replacement" && !result.ContainsKey(257),
            "A freshly rebuilt Dictionary could not reuse a removed entry correctly.");

        var enumerator = result.GetEnumerator();
        Assert(enumerator.MoveNext(), "The rebuilt Dictionary unexpectedly had no entries.");
        result.Add(-2, "version-change");
        var versionThrew = false;
        try
        {
            _ = enumerator.MoveNext();
        }
        catch (InvalidOperationException)
        {
            versionThrew = true;
        }
        finally
        {
            enumerator.Dispose();
        }

        Assert(versionThrew,
            "The fresh Dictionary builder did not preserve enumerator version checks.");

        var duplicatePayload = LuminPackSerializer.Serialize(
            new Dictionary<int, int> { [1] = 10, [2] = 20 });
        BitConverter.GetBytes(1).CopyTo(duplicatePayload, sizeof(int) * 3);

        var duplicateThrew = false;
        try
        {
            _ = LuminPackSerializer.Deserialize<Dictionary<int, int>>(duplicatePayload);
        }
        catch (ArgumentException)
        {
            duplicateThrew = true;
        }

        Assert(duplicateThrew,
            "The fresh Dictionary bucket builder did not preserve duplicate-key rejection.");
    }

    private static void RunCase(List<string> results, string name, Action test)
    {
        try
        {
            test();
            results.Add($"✓ {name} - PASSED");
        }
        catch (Exception ex)
        {
            results.Add($"✗ {name} - FAILED: {ex.Message}");
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
