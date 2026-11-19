#if LUMINPACK_NEXT_GENERATOR
using System.Collections.Immutable;
using LuminPack;
using LuminPack.Attribute;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Exercises the generated entry points for mixed array ranks and pooled Union cases.</summary>
public sealed class NinoGeneratorRegressionTest
{
    /// <summary>Checks that lists and vectors of rectangular arrays retain their nested vector elements.</summary>
    /// <param name="json">Whether to use the JSON entry points.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ListAndVectorOfRectangularArraysWithVectorElementsRoundTrip(bool json)
    {
        int[,][] matrix = { { new[] { 1, 2 }, Array.Empty<int>() } };
        var list = new List<int[,][]> { matrix };
        int[][,][] vector = [matrix];
        List<int[,][]>? readList = null;
        int[][,][]? readVector = null;

        if (json)
        {
            LuminPackSerializer.DeserializeJson(LuminPackSerializer.SerializeJson(list), ref readList);
            LuminPackSerializer.DeserializeJson(LuminPackSerializer.SerializeJson(vector), ref readVector);
        }
        else
        {
            LuminPackSerializer.Deserialize(LuminPackSerializer.Serialize(list), ref readList);
            LuminPackSerializer.Deserialize(LuminPackSerializer.Serialize(vector), ref readVector);
        }

        Assert.NotNull(readList);
        Assert.NotNull(readVector);
        foreach (var readMatrix in new int[][,][] { Assert.Single(readList), Assert.Single(readVector) })
        {
            Assert.Equal(1, readMatrix.GetLength(0));
            Assert.Equal(2, readMatrix.GetLength(1));
            Assert.Equal(new[] { 1, 2 }, readMatrix[0, 0]);
            Assert.Empty(readMatrix[0, 1]);
        }
    }

    /// <summary>Checks immutable construction from a staging vector of rectangular arrays.</summary>
    /// <param name="json">Whether to use the JSON entry points.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ImmutableListOfRectangularArraysRoundTrips(bool json)
    {
        var source = ImmutableList.Create(new int[,] { { 1, 2 } }, new int[,] { { 3 }, { 4 } });
        ImmutableList<int[,]>? result = null;

        if (json)
            LuminPackSerializer.DeserializeJson(LuminPackSerializer.SerializeJson(source), ref result);
        else
            LuminPackSerializer.Deserialize(LuminPackSerializer.Serialize(source), ref result);

        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.Equal(2, result[0].GetLength(1));
        Assert.Equal(2, result[1].GetLength(0));
        Assert.Equal(2, result[0][0, 1]);
        Assert.Equal(4, result[1][1, 0]);
    }

    /// <summary>Checks that a Union case rents for a fresh read and reuses an existing exact case.</summary>
    /// <param name="json">Whether to use the JSON entry points.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PooledUnionCaseRentsAndReusesItsInstance(bool json)
    {
        NinoPooledUnionRoot source = new NinoPooledUnionCase { Id = 23, Extra = 42 };
        NinoPooledUnionRoot? fresh = null;
        var existing = new NinoPooledUnionCase();
        NinoPooledUnionRoot? reused = existing;

        if (json)
        {
            string payload = LuminPackSerializer.SerializeJson(source);
            LuminPackSerializer.DeserializeJson(payload, ref fresh);
            LuminPackSerializer.DeserializeJson(payload, ref reused);
        }
        else
        {
            byte[] payload = LuminPackSerializer.Serialize(source);
            LuminPackSerializer.Deserialize(payload, ref fresh);
            LuminPackSerializer.Deserialize(payload, ref reused);
        }

        Assert.Same(NinoPooledUnionCase.Shared, fresh);
        Assert.Same(existing, reused);
        Assert.Equal(23, NinoPooledUnionCase.Shared.Id);
        Assert.Equal(42, NinoPooledUnionCase.Shared.Extra);
        Assert.Equal(23, existing.Id);
        Assert.Equal(42, existing.Extra);
    }
}

/// <summary>A Union root whose storage is inherited by a pooled case.</summary>
[LuminPackable]
public abstract partial class NinoPooledUnionRoot
{
    public int Id;
}

/// <summary>A fixed-width Union case with a pool rent and inherited storage.</summary>
[LuminPackable]
public sealed partial class NinoPooledUnionCase : NinoPooledUnionRoot
{
    internal static readonly NinoPooledUnionCase Shared = new();

    public int Extra;

    /// <summary>Rents the instance reserved for this fixture.</summary>
    /// <returns>The fixture's shared case.</returns>
    [LuminPackPoolRent]
    public static NinoPooledUnionCase Rent() => Shared;
}
#endif
