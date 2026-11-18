using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using LuminPack.Code;
using Microsoft.CodeAnalysis;

internal static partial class SerializerIssueRegression
{
    private const int MatrixCountOffset = 1 + 2 * sizeof(int);
    private const int MatrixHeaderWidth = MatrixCountOffset + sizeof(int);

    private delegate int ReadNullableArray<T>(ReadOnlySpan<byte> input, ref T? value) where T : class;

    /// <summary>Checks independent consumers against the Packed, Native and Tagged rectangular nullable wire fixtures.</summary>
    /// <param name="references">Platform and runtime references.</param>
    internal static void RunRectangularNullableWire(MetadataReference[] references)
    {
        const string source = """
            #nullable enable
            using System;
            using System.IO;
            using LuminPack;
            using LuminPack.Attribute;
            using LuminPack.Utility;
            ASSEMBLY_LAYOUT

            public static class Entry
            {
                public static byte[] WriteMatrix(int?[,]? value) => LuminPackSerializer.Serialize(value);
                public static byte[] WriteBufferedMatrix(int?[,]? value)
                {
                    using var writer = new LuminBufferWriter(true);
                    LuminPackSerializer.Serialize(value, writer);
                    LuminPackSerializer.Serialize(value, writer);
                    return writer.GetSpan().ToArray();
                }
                public static int ReadMatrix(ReadOnlySpan<byte> input, ref int?[,]? value) => LuminPackSerializer.Deserialize(input, ref value);
                public static byte[] WriteCube(int?[,,] value) => LuminPackSerializer.Serialize(value);
                public static int ReadCube(ReadOnlySpan<byte> input, ref int?[,,]? value) => LuminPackSerializer.Deserialize(input, ref value);
                public static byte[] WriteWideMatrix(long?[,] value) => LuminPackSerializer.Serialize(value);
                public static int ReadWideMatrix(ReadOnlySpan<byte> input, ref long?[,]? value) => LuminPackSerializer.Deserialize(input, ref value);
                public static int ReadBooleanMatrix(ReadOnlySpan<byte> input, ref bool?[,]? value) => LuminPackSerializer.Deserialize(input, ref value);
                public static byte[] WriteVector(int?[] value) => LuminPackSerializer.Serialize(value);
                public static string WriteJsonMatrix(int?[,]? value) => LuminPackSerializer.SerializeJson(value);
                public static bool RejectsOversizedBoundedMatrix(int?[,] value)
                {
                    byte[] wire = LuminPackSerializer.Serialize(new LimitedMatrix { Values = value });
                    LimitedMatrix? target = null;
                    try
                    {
                        LuminPackSerializer.Deserialize(wire, ref target);
                        return false;
                    }
                    catch (InvalidDataException)
                    {
                        return target?.Values is null;
                    }
                }
            }

            [LuminPackable, LuminPackOption(UntrustedData = true, MaxCollectionLength = 3)]
            public sealed class LimitedMatrix { public int?[,]? Values; }
            """;
        Type implicitTagged = Consumer("ImplicitTagged", declaration: string.Empty);
        Type packed = Consumer("Packed", Declaration("Packed"));
        Type native = Consumer("Native", Declaration("Native"));
        Type tagged = Consumer("Tagged", Declaration("Tagged"));
        var packedReader = Bind<ReadNullableArray<int?[,]>>(packed, "ReadMatrix");
        var nativeReader = Bind<ReadNullableArray<int?[,]>>(native, "ReadMatrix");
        var taggedReader = Bind<ReadNullableArray<int?[,]>>(tagged, "ReadMatrix");

        int?[,] mixed = { { 0x01020304, null }, { -1, 0 } };
        byte[] framedFixture = Convert.FromHexString("000200000002000000040000000004030201FF00FFFFFFFF0000000000");
        CheckWriter(packed, framedFixture, mixed);
        ReadMatrix(packedReader, framedFixture, mixed);

        // The local Int32 nullable model has an eight-byte stride. Nonzero padding is intentional in the fixed fixtures.
        byte[] nativeFixture = Convert.FromHexString("0002000000020000000400000001A1A2A30403020100B1B2B31122334401C1C2C3FFFFFFFF01D1D2D300000000");
        foreach (var reader in new[] { nativeReader, taggedReader, Bind<ReadNullableArray<int?[,]>>(implicitTagged, "ReadMatrix") })
        {
            ReadMatrix(reader, nativeFixture, mixed);
            RejectTruncations(reader, nativeFixture);
            RejectChangedHeader(reader, nativeFixture, MatrixCountOffset, -2);
            RejectChangedHeader(reader, nativeFixture, MatrixCountOffset, -1);
            RejectChangedHeader(reader, nativeFixture, MatrixCountOffset, 3);
            RejectChangedHeader(reader, nativeFixture, 1, -1);
            RejectChangedHeader(reader, nativeFixture, 1, int.MaxValue);
            RejectChangedByte(reader, nativeFixture, MatrixHeaderWidth + 3 * Unsafe.SizeOf<int?>(), 2);
            RejectChangedByte(reader, nativeFixture, MatrixHeaderWidth + Unsafe.SizeOf<int?>(), 255);
        }

        // Decode the explicit padding before testing the direct writers. This makes the required copy independent of padding
        // in newly constructed managed values.
        int?[,]? seeded = null;
        nativeReader(nativeFixture, ref seeded);
        CheckWriter(native, nativeFixture, seeded!);
        CheckWriter(tagged, nativeFixture, seeded!);
        CheckWriter(implicitTagged, nativeFixture, seeded!);

        string json = Bind<Func<int?[,], string>>(packed, "WriteJsonMatrix")(mixed);
        foreach (var (writer, reader) in new[] { (native, nativeReader), (tagged, taggedReader) })
        {
            var write = Bind<Func<int?[,]?, byte[]>>(writer, "WriteMatrix");
            byte[] nullWire = write(null);
            RequireNullableWire(nullWire.AsSpan().SequenceEqual(new byte[] { 255 }), "The null matrix marker changed.");
            int?[,]? old = mixed;
            RequireNullableWire(reader(nullWire, ref old) == 1 && old is null, "The nullable matrix did not clear a reused root.");
            foreach (int?[,] matrix in new[] { new int?[0, 4], new int?[2, 17], new int?[3, 11] })
            {
                if (matrix.GetLength(0) == 3)
                    for (int row = 0; row < 3; row++)
                        for (int col = 0; col < 11; col++)
                            matrix[row, col] = row * 11 - col;
                byte[] wire = write(matrix);
                RequireNullableWire(wire.Length == checked(MatrixHeaderWidth + matrix.Length * Unsafe.SizeOf<int?>()) &&
                    BinaryPrimitives.ReadInt32LittleEndian(wire.AsSpan(MatrixCountOffset)) == matrix.Length,
                    "The matrix header or fixed payload width changed.");
                ReadMatrix(reader, wire, matrix);
            }

            RequireNullableWire(Bind<Func<int?[,], string>>(writer, "WriteJsonMatrix")(mixed) == json,
                "The binary layout option changed JSON.");
            RequireNullableWire(Bind<Func<int?[,], bool>>(writer, "RejectsOversizedBoundedMatrix")(mixed),
                "The optimized matrix bypassed its enclosing collection limit.");
            VerifyNullableBooleanMatrix(writer);
            VerifyLargeNullableMatrix(write, reader);
        }
        byte[] vector = Bind<Func<int?[], byte[]>>(tagged, "WriteVector")([17, null, 0, -1]);
        RequireNullableWire(vector.Length == sizeof(int) + 4 * Unsafe.SizeOf<int?>() &&
            BinaryPrimitives.ReadInt32LittleEndian(vector) == 4, "The single-model Tagged nullable vector gained extra framing.");

        int?[,,] cube = { { { null, 7, -1 } }, { { 8, null, 9 } } };
        foreach (Type writer in new[] { native, tagged })
        {
            var write = Bind<Func<int?[,,], byte[]>>(writer, "WriteCube");
            var read = Bind<ReadNullableArray<int?[,,]>>(writer, "ReadCube");
            foreach (int?[,,] input in new[] { cube, new int?[3, 0, 2] })
            {
                byte[] wire = write(input);
                RequireNullableWire(wire.Length == 1 + 4 * sizeof(int) + input.Length * Unsafe.SizeOf<int?>() &&
                    BinaryPrimitives.ReadInt32LittleEndian(wire.AsSpan(1 + 3 * sizeof(int))) == input.Length,
                    "A rank-three nullable array used the wrong binary layout.");
                int?[,,]? result = null;
                RequireNullableWire(read(wire, ref result) == wire.Length &&
                    result != null && Enumerable.Range(0, 3).All(axis => result.GetLength(axis) == input.GetLength(axis)) &&
                    result.Cast<int?>().SequenceEqual(input.Cast<int?>()), "A rank-three nullable array changed shape or values.");
            }
        }
        VerifyTaggedWideMatrix(tagged);

        Console.WriteLine("Passed Packed, Native and Tagged rectangular nullable wire fixtures, validation and retained writes.");

        Type Consumer(string name, string declaration) => CompileConsumer(references,
            source.Replace("ASSEMBLY_LAYOUT", declaration, StringComparison.Ordinal), "RectangularNullable" + name).GetType("Entry")!;

        static string Declaration(string mode) => "[assembly: LuminPackOption(StructLayout = LuminPackStructLayout." + mode + ")]";

        static void CheckWriter(Type consumer, byte[] fixture, int?[,] value)
        {
            RequireNullableWire(Bind<Func<int?[,], byte[]>>(consumer, "WriteMatrix")(value).AsSpan().SequenceEqual(fixture),
                "The matrix writer changed the pinned layout bytes.");
            RequireNullableWire(Bind<Func<int?[,], byte[]>>(consumer, "WriteBufferedMatrix")(value).AsSpan()
                .SequenceEqual(fixture.Concat(fixture).ToArray()), "The retained writer changed a matrix or its appended boundary.");
        }
    }

    /// <summary>Checks presence validation at the start, vector boundaries, and scalar tail of a long nullable block.</summary>
    /// <param name="write">The fixed-width matrix writer.</param>
    /// <param name="reader">The matching checked matrix reader.</param>
    private static void VerifyLargeNullableMatrix(Func<int?[,]?, byte[]> write, ReadNullableArray<int?[,]> reader)
    {
        const int length = 1027;
        var matrix = new int?[1, length];
        for (int index = 0; index < length; index++)
            matrix[0, index] = index % 5 == 0 ? null : index - 51;
        byte[] wire = write(matrix);
        ReadMatrix(reader, wire, matrix);
        // A native eight-byte Int32 slot crosses the second 128-, 256-, or 512-bit vector at index 2, 4, or 8.
        foreach (int index in new[] { 0, 2, 4, 8, 512, length - 1 })
            RejectChangedByte(reader, wire, MatrixHeaderWidth + index * Unsafe.SizeOf<int?>(), 2);
    }

    /// <summary>Checks the tagged Int64 nullable matrix's alternate model and invalid layout tag rejection.</summary>
    /// <param name="consumer">The Tagged generated consumer.</param>
    private static void VerifyTaggedWideMatrix(Type consumer)
    {
        var read = Bind<ReadNullableArray<long?[,]>>(consumer, "ReadWideMatrix");
        var write = Bind<Func<long?[,], byte[]>>(consumer, "WriteWideMatrix");
        const long value = 0x0102030405060708;
        // Model 2 aligns Int64 to four bytes. A 64-bit reader must use the existing modeled transfer.
        byte[] fixture = Convert.FromHexString("0001000000020000000200000002010000000807060504030201000000001122334455667788");
        long?[,]? result = null;
        RequireNullableWire(read(fixture, ref result) == fixture.Length && result is not null &&
            result.GetLength(0) == 1 && result.GetLength(1) == 2 && result[0, 0] == value && result[0, 1] is null,
            "The alternate Int64 nullable model was not converted correctly.");
        byte[] encoded = write(result!);
        byte tag = encoded[MatrixHeaderWidth];
        RequireNullableWire(tag is 1 or 2 && encoded.Length == MatrixHeaderWidth + 1 + 2 * (tag == 1 ? 16 : 12),
            "A nonempty variable-layout matrix must have exactly one storage tag.");
        RequireNullableWire(write(new long?[0, 3]).Length == MatrixHeaderWidth, "An empty tagged matrix acquired a storage tag.");
        foreach (byte bad in new byte[] { 0, 255 })
        {
            fixture[MatrixHeaderWidth] = bad;
            RejectNullableFrame(read, fixture, result!);
        }
        fixture[MatrixHeaderWidth] = 2;
        fixture[MatrixHeaderWidth + 1 + 12] = 2;
        RejectNullableFrame(read, fixture, result!);
    }

    /// <summary>Checks the independent presence and payload byte domains of a nullable Boolean matrix.</summary>
    /// <param name="consumer">The Native or Tagged generated consumer.</param>
    private static void VerifyNullableBooleanMatrix(Type consumer)
    {
        var read = Bind<ReadNullableArray<bool?[,]>>(consumer, "ReadBooleanMatrix");
        byte[] fixture = Convert.FromHexString("00010000000300000003000000000001000101");
        bool?[,]? result = null;
        RequireNullableWire(read(fixture, ref result) == fixture.Length && result is not null &&
            result.Cast<bool?>().SequenceEqual(new bool?[] { null, false, true }), "The nullable Boolean fixture changed.");
        fixture[^1] = 2;
        RejectNullableFrame(read, fixture, result!);
        fixture[^1] = 1;
        fixture[^2] = 2;
        RejectNullableFrame(read, fixture, result!);
    }

    /// <summary>Binds a framework-only signature to one generated consumer's entry point.</summary>
    /// <typeparam name="T">The entry signature.</typeparam>
    /// <param name="consumer">The generated entry container.</param>
    /// <param name="name">The method name.</param>
    /// <returns>The typed entry point.</returns>
    private static T Bind<T>(Type consumer, string name) where T : Delegate => consumer.GetMethod(name)!.CreateDelegate<T>();

    /// <summary>Checks values, dimensions and the consumed position in a buffer containing another record.</summary>
    /// <param name="reader">The generated matrix reader.</param>
    /// <param name="wire">A complete matrix record.</param>
    /// <param name="expected">The independently known matrix values.</param>
    private static void ReadMatrix(ReadNullableArray<int?[,]> reader, byte[] wire, int?[,] expected)
    {
        byte[] stream = [.. wire, 255];
        int?[,]? output = null;
        int consumed = reader(stream, ref output);
        RequireNullableWire(consumed == wire.Length && output != null && output.GetLength(0) == expected.GetLength(0) &&
            output.GetLength(1) == expected.GetLength(1) && output.Cast<int?>().SequenceEqual(expected.Cast<int?>()),
            "The rectangular reader changed dimensions, row-major order or the next record boundary.");
        RequireNullableWire(reader(stream.AsSpan(consumed), ref output) == 1 && output is null, "The trailing null record was damaged.");
    }

    /// <summary>Rejects every incomplete prefix of a fixed-width record.</summary>
    /// <param name="reader">The generated matrix reader.</param>
    /// <param name="wire">A complete fixed-width record.</param>
    private static void RejectTruncations(ReadNullableArray<int?[,]> reader, byte[] wire)
    {
        for (int length = 0; length < wire.Length; length++)
            RejectNullableFrame(reader, wire.AsSpan(0, length));
    }

    /// <summary>Rejects a rectangular record whose four-byte shape field was changed.</summary>
    /// <param name="reader">The generated matrix reader.</param>
    /// <param name="wire">A valid reference record.</param>
    /// <param name="offset">The position of the field.</param>
    /// <param name="value">The invalid field value.</param>
    private static void RejectChangedHeader(ReadNullableArray<int?[,]> reader, byte[] wire, int offset, int value)
    {
        byte[] bad = (byte[])wire.Clone();
        BinaryPrimitives.WriteInt32LittleEndian(bad.AsSpan(offset), value);
        RejectNullableFrame(reader, bad);
    }

    /// <summary>Rejects a rectangular record with an unsupported tag or a noncanonical presence flag.</summary>
    /// <param name="reader">The generated matrix reader.</param>
    /// <param name="wire">A valid reference record.</param>
    /// <param name="offset">The position of the byte.</param>
    /// <param name="value">The invalid byte value.</param>
    private static void RejectChangedByte(ReadNullableArray<int?[,]> reader, byte[] wire, int offset, byte value)
    {
        byte[] bad = (byte[])wire.Clone();
        bad[offset] = value;
        RejectNullableFrame(reader, bad);
    }

    /// <summary>Checks that invalid fixed-width input does not publish or mutate a target matrix.</summary>
    /// <param name="reader">The generated matrix reader.</param>
    /// <param name="wire">The invalid record or incomplete prefix.</param>
    private static void RejectNullableFrame(ReadNullableArray<int?[,]> reader, ReadOnlySpan<byte> wire)
    {
        int?[,] prior = { { 42 } };
        RejectNullableFrame(reader, wire, prior);
        RequireNullableWire(prior[0, 0] == 42, "Invalid nullable input modified the prior target array.");
    }

    /// <summary>Checks that an invalid fixed-width record is rejected before publishing its target.</summary>
    /// <typeparam name="T">The exact rectangular array type.</typeparam>
    /// <param name="reader">The generated matrix reader.</param>
    /// <param name="wire">The invalid record or incomplete prefix.</param>
    /// <param name="prior">The destination before the read.</param>
    private static void RejectNullableFrame<T>(ReadNullableArray<T> reader, ReadOnlySpan<byte> wire, T prior) where T : class
    {
        T? output = prior;
        try
        {
            reader(wire, ref output);
        }
        catch (Exception error) when (error is InvalidDataException or FormatException ||
            error.GetType().DeclaringType == typeof(LuminPackExceptionHelper))
        {
            RequireNullableWire(ReferenceEquals(output, prior),
                "An invalid fixed-width nullable matrix was published before validation.");
            return;
        }
        throw new InvalidOperationException("An invalid rectangular nullable record was accepted.");
    }

    /// <summary>Reports a rectangular nullable wire regression.</summary>
    /// <param name="condition">The required wire or reader behavior.</param>
    /// <param name="error">The failure description.</param>
    private static void RequireNullableWire(bool condition, string error)
    {
        if (!condition)
            throw new InvalidOperationException(error);
    }
}
