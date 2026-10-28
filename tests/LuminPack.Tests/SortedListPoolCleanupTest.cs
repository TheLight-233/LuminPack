using System.Buffers;
using LuminPack;
using LuminPack.Attribute;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Protects ownership and clearing of the two fresh sorted-list staging arrays on failure.</summary>
[Collection(nameof(RuntimeStateCollection))]
public sealed class SortedListPoolCleanupTest
{
    /// <summary>Returns both original rentals and preserves the original callback exception when parsing fails.</summary>
    [Fact]
    public void FailedFreshReadReturnsAndClearsBothRentals()
    {
        var source = new SortedList<string, SortedListPoolCleanupValue>
        {
            ["a"] = new() { Number = 1 },
            ["b"] = new() { Number = 2 }
        };
        byte[] payload = LuminPackSerializer.Serialize(source);
        string[] expectedKeys = ArrayPool<string>.Shared.Rent(16);
        SortedListPoolCleanupValue[] expectedValues = ArrayPool<SortedListPoolCleanupValue>.Shared.Rent(16);
        ArrayPool<string>.Shared.Return(expectedKeys, clearArray: true);
        ArrayPool<SortedListPoolCleanupValue>.Shared.Return(expectedValues, clearArray: true);
        string[]? actualKeys = null;
        SortedListPoolCleanupValue[]? actualValues = null;
        var failure = new InvalidOperationException("The second value failed after reading its fields.");
        try
        {
            SortedListPoolCleanupValue.Failure = failure;
            SortedList<string, SortedListPoolCleanupValue>? target = null;
            Exception? actualFailure = null;
            try { LuminPackSerializer.Deserialize(payload, ref target); }
            catch (Exception error) { actualFailure = error; }
            actualKeys = ArrayPool<string>.Shared.Rent(16);
            actualValues = ArrayPool<SortedListPoolCleanupValue>.Shared.Rent(16);
            Assert.Same(failure, actualFailure);
            Assert.Null(target);
            Assert.True(ReferenceEquals(expectedKeys, actualKeys) && ReferenceEquals(expectedValues, actualValues),
                $"Returned original key/value rentals: {ReferenceEquals(expectedKeys, actualKeys)}/{ReferenceEquals(expectedValues, actualValues)}.");
            Assert.All(actualKeys, item => Assert.Null(item));
            Assert.All(actualValues, item => Assert.Null(item));
        }
        finally
        {
            SortedListPoolCleanupValue.Failure = null;
            if (actualKeys is not null) ArrayPool<string>.Shared.Return(actualKeys, clearArray: true);
            if (actualValues is not null) ArrayPool<SortedListPoolCleanupValue>.Shared.Return(actualValues, clearArray: true);
            // Failed assertions against the former implementation still restore the deliberately captured lost rentals.
            if (!ReferenceEquals(expectedKeys, actualKeys)) ArrayPool<string>.Shared.Return(expectedKeys, clearArray: true);
            if (!ReferenceEquals(expectedValues, actualValues)) ArrayPool<SortedListPoolCleanupValue>.Shared.Return(expectedValues, clearArray: true);
        }
    }
}

/// <summary>Provides a user failure after one staging slot was populated successfully.</summary>
[LuminPackable]
public sealed class SortedListPoolCleanupValue
{
    public int Number;
    public static Exception? Failure;

    /// <summary>Raises the caller's original failure only for the second parsed value.</summary>
    [LuminPackOnDeserialized]
    public void OnRead()
    {
        if (Number == 2 && Failure is not null) throw Failure;
    }
}
