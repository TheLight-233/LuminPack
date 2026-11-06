using System.Buffers.Binary;
using LuminPack;
using LuminPack.Attribute;
using LuminPack.Option;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Checks ownership and failure publication in generated binary string matrix readers.</summary>
public sealed class StringMatrixReuseTest
{
    /// <summary>Retains both the matching matrix and equal strings, including empty shapes.</summary>
    [Fact]
    public void SameShapePublishesThroughExistingReferences()
    {
        string?[,] source = { { "same", "new" }, { null, "last" } };
        byte[] bytes = LuminPackSerializer.Serialize(source);
        string?[,] alias = { { new string("same".ToCharArray()), "old" }, { "old", null } };
        string?[,] target = alias;
        string? equal = target[0, 0];
        Assert.Equal(bytes.Length, LuminPackSerializer.Deserialize(bytes, ref target));
        Assert.Same(alias, target);
        Assert.Same(equal, target[0, 0]);
        Assert.Equal(source.Cast<string?>(), alias.Cast<string?>());

        foreach (var shape in new[] { (0, 3), (3, 0), (0, 0) })
        {
            alias = new string?[shape.Item1, shape.Item2];
            target = alias;
            bytes = LuminPackSerializer.Serialize(target);
            Assert.Equal(bytes.Length, LuminPackSerializer.Deserialize(bytes, ref target));
            Assert.Same(alias, target);
        }
        alias = new string?[0, 2];
        target = alias;
        LuminPackSerializer.Deserialize(LuminPackSerializer.Serialize(new string?[0, 3]), ref target);
        Assert.NotSame(alias, target);
        Assert.Equal(3, target.GetLength(1));
    }

    /// <summary>Replaces different dimensions, nonzero bounds and other compiled profiles.</summary>
    [Fact]
    public void OtherShapesAndProfilesAllocateTheirOwnMatrix()
    {
        string[,] source = { { "one", "two" }, { "three", "four" } };
        byte[] bytes = LuminPackSerializer.Serialize(source);
        foreach (var alias in new[] { new string[1, 4], new string[4, 1],
            (string[,])Array.CreateInstance(typeof(string), [2, 2], [1, 0]),
            (string[,])Array.CreateInstance(typeof(string), [2, 2], [0, -1]) })
        {
            var target = alias;
            LuminPackSerializer.Deserialize(bytes, ref target);
            Assert.NotSame(alias, target);
            Assert.All(alias.Cast<string?>(), Assert.Null);
            Assert.Equal(source.Cast<string>(), target.Cast<string>());
        }

        CheckReplacement<LuminPackUtf8LengthOptions>();
        CheckReplacement<LuminPackUtf16TokenOptions>();
        CheckReplacement<LuminPackUtf8TokenOptions>();
        CheckReplacement<LuminPackUtf16VarIntOptions>();
        CheckReplacement<LuminPackUtf8VarIntOptions>();
        CheckReplacement<MatchingTextProfile>();
        void CheckReplacement<TOptions>() where TOptions : struct, ILuminPackOptions
        {
            byte[] encoded = LuminPackSerializer.Serialize<TOptions>(source);
            var alias = new string[2, 2];
            var target = alias;
            LuminPackSerializer.Deserialize<TOptions>(encoded, ref target);
            Assert.NotSame(alias, target);
            Assert.Equal(source.Cast<string>(), target.Cast<string>());
        }
    }

    /// <summary>Other ranks and JSON allocate their original replacement arrays.</summary>
    [Fact]
    public void HigherRanksAndJsonKeepTheirOriginalOwnership()
    {
        string[,,] cube = { { { "new" } } };
        var previousCube = new string[1, 1, 1];
        var resultCube = previousCube;
        LuminPackSerializer.Deserialize(LuminPackSerializer.Serialize(cube), ref resultCube);
        Assert.NotSame(previousCube, resultCube);
        Assert.Equal("new", resultCube[0, 0, 0]);

        string[,] matrix = { { "new" } };
        var previous = new string[1, 1];
        var result = previous;
        LuminPackSerializer.DeserializeJson(LuminPackSerializer.SerializeJson(matrix), ref result);
        Assert.NotSame(previous, result);
        Assert.Equal("new", result[0, 0]);
    }

    /// <summary>Checks shape validation before mutation and the completed prefix on a truncated payload.</summary>
    [Fact]
    public void FailedReadsPublishOnlyValidatedSlots()
    {
        string[,] source = { { "first", "second" }, { "third", "last" } };
        byte[] bytes = LuminPackSerializer.Serialize(source);
        string[,] alias = { { "a", "b" }, { "c", "d" } };
        var target = alias;
        byte[] malformed = (byte[])bytes.Clone();
        BinaryPrimitives.WriteInt32LittleEndian(malformed.AsSpan(1), 3);
        Assert.ThrowsAny<Exception>(() => LuminPackSerializer.Deserialize(malformed, ref target));
        Assert.Equal(new[] { "a", "b", "c", "d" }, alias.Cast<string>());
        Assert.ThrowsAny<Exception>(() => LuminPackSerializer.Deserialize(bytes.AsSpan(0, bytes.Length - 1), ref target));
        Assert.Same(alias, target);
        Assert.Equal(new[] { "first", "second", "third", "d" }, alias.Cast<string>());
    }

    /// <summary>Checks the frozen string quota before publishing the offending slot into a nested matrix.</summary>
    [Fact]
    public void NestedMatrixAppliesPerStringLimits()
    {
        var source = new LimitedStringMatrix { Values = new string[,] { { "ok", "long" } } };
        var alias = new string[,] { { "a", "b" } };
        var target = new LimitedStringMatrix { Values = alias };
        byte[] bytes = LuminPackSerializer.Serialize(source);
        Assert.ThrowsAny<Exception>(() => LuminPackSerializer.Deserialize(bytes, ref target));
        Assert.Same(alias, target.Values);
        Assert.Equal(new[] { "ok", "b" }, alias.Cast<string>());
    }

    /// <summary>A consumer profile with the same binary encoding as the default framework profile.</summary>
    private readonly struct MatchingTextProfile : ILuminPackOptions
    {
        /// <inheritdoc/>
        public LuminPackStringEncoding StringEncoding => LuminPackStringEncoding.UTF16;
        /// <inheritdoc/>
        public LuminPackStringRecording StringRecording => LuminPackStringRecording.Length;
        /// <inheritdoc/>
        public System.Buffers.StandardFormat StandardFormat => new('G');
        /// <inheritdoc/>
        public int MaxJsonDepth => 1024;
        /// <inheritdoc/>
        public bool ValidateJsonSyntax => false;
    }
}

/// <summary>A matrix whose individual string lengths are limited by its enclosing binary model.</summary>
[LuminPackable, LuminPackOption(UntrustedData = true, MaxStringLength = 3)]
public partial class LimitedStringMatrix
{
    public string[,] Values = new string[0, 0];
}
