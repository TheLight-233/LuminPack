using System.Buffers.Binary;
using LuminPack;
using LuminPack.Attribute;
using LuminPack.Option;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Checks ownership and failure publication in generated binary string matrix readers.</summary>
public sealed class StringMatrixReuseTest
{
    /// <summary>Checks that a vector of matrices and a matrix of vectors preserve their distinct outer ranks.</summary>
    [Fact]
    public void MixedArrayRanksUseTheOuterCollectionFormatter()
    {
        string[][,] matrices = [new string[,] { { "one", "two" } }, new string[0, 3]];
        string[,][] vectors = { { new[] { "one", "two" }, Array.Empty<string>() } };
        string[][,]? readMatrices = null;
        string[,][]? readVectors = null;
        foreach (bool json in new[] { false, true })
        {
            if (json)
            {
                LuminPackSerializer.DeserializeJson(LuminPackSerializer.SerializeJson(matrices), ref readMatrices);
                LuminPackSerializer.DeserializeJson(LuminPackSerializer.SerializeJson(vectors), ref readVectors);
            }
            else
            {
                LuminPackSerializer.Deserialize(LuminPackSerializer.Serialize(matrices), ref readMatrices);
                LuminPackSerializer.Deserialize(LuminPackSerializer.Serialize(vectors), ref readVectors);
            }
            Assert.Equal(2, readMatrices!.Length);
            Assert.Equal("two", readMatrices[0][0, 1]);
            Assert.Equal(3, readMatrices[1].GetLength(1));
            Assert.Equal(2, readVectors!.GetLength(1));
            Assert.Equal(vectors[0, 0], readVectors[0, 0]);
            Assert.Empty(readVectors[0, 1]);
        }
    }

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
            byte[] encoded = LuminPackSerializer.Serialize<string[,], TOptions>(source);
            var alias = new string[2, 2];
            var target = alias;
            LuminPackSerializer.Deserialize<string[,], TOptions>(encoded, ref target);
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
