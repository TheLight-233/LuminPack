using System.Globalization;
using System.Numerics;
using System.Text;
using LuminPack;
using LuminPack.Utility;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Checks the stateless concrete root entries of single framework values against the pooled reusable-buffer entries.</summary>
/// <remarks>Every call names its concrete type, so it binds the generated concrete overload rather than the generic fallback.</remarks>
public class ConcreteRootPathTest
{
    /// <summary>Checks sixteen-byte scalars read with one complete-width store, including into array element storage.</summary>
    [Fact]
    public void SixteenByteScalarsReadTheirCompleteStorage()
    {
        decimal number = -79228162514264337593543950335m;
        byte[] decimalBytes = LuminPackSerializer.Serialize(number);
        Pooled(decimalBytes, buffer => LuminPackSerializer.Serialize(number, buffer));
        var decimals = new decimal[3];
        Assert.Equal(16, LuminPackSerializer.Deserialize(decimalBytes.AsSpan(), ref decimals[1]));
        Assert.Equal(new[] { 0m, number, 0m }, decimals);

        Int128 signed = Int128.MinValue + 12345;
        byte[] signedBytes = LuminPackSerializer.Serialize(signed);
        Pooled(signedBytes, buffer => LuminPackSerializer.Serialize(signed, buffer));
        Int128 signedCopy = 0;
        Assert.Equal(16, LuminPackSerializer.Deserialize(signedBytes.AsSpan(), ref signedCopy));
        Assert.Equal(signed, signedCopy);

        UInt128 unsigned = UInt128.MaxValue - 99;
        byte[] unsignedBytes = LuminPackSerializer.Serialize(unsigned);
        UInt128 unsignedCopy = 0;
        Assert.Equal(16, LuminPackSerializer.Deserialize(unsignedBytes.AsSpan(), ref unsignedCopy));
        Assert.Equal(unsigned, unsignedCopy);

        var complex = new Complex(-3.5, double.Epsilon);
        byte[] complexBytes = LuminPackSerializer.Serialize(complex);
        Complex complexCopy = default;
        Assert.Equal(16, LuminPackSerializer.Deserialize(complexBytes.AsSpan(), ref complexCopy));
        Assert.Equal(complex, complexCopy);

        var guid = Guid.Parse("8f3d1c2b-4a59-4e6f-9b70-1d2c3e4f5a6b");
        byte[] guidBytes = LuminPackSerializer.Serialize(guid);
        Assert.Equal(guid.ToByteArray(), guidBytes);
        Guid guidCopy = default;
        Assert.Equal(16, LuminPackSerializer.Deserialize(guidBytes.AsSpan(), ref guidCopy));
        Assert.Equal(guid, guidCopy);

        var vector = new Vector4(1.5f, -2.5f, float.NaN, 4.5f);
        byte[] vectorBytes = LuminPackSerializer.Serialize(vector);
        Vector4 vectorCopy = default;
        Assert.Equal(16, LuminPackSerializer.Deserialize(vectorBytes.AsSpan(), ref vectorCopy));
        Assert.Equal(vectorBytes, LuminPackSerializer.Serialize(vectorCopy));
    }

    /// <summary>Checks string records, including null, empty, non-ASCII and an unpaired surrogate, against the pooled path and the evaluator.</summary>
    /// <param name="text">The string to check.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("seven")]
    [InlineData("h\u00E9llo \u4E16\u754C \U0001F600")]
    [InlineData("\uD800x\uDC00")]
    public void StringRootsMatchThePooledRecord(string? text)
    {
        string? value = text;
        byte[] bytes = LuminPackSerializer.Serialize(value);
        Pooled(bytes, buffer => LuminPackSerializer.Serialize(value, buffer));
        // Nonempty Latin-1 text uses the compact one-byte form; other text keeps raw code units.
        bool compact = !string.IsNullOrEmpty(value) && value.All(character => character <= 'ÿ');
        Assert.Equal(sizeof(int) + (value?.Length ?? 0) * (compact ? 1 : sizeof(char)), bytes.Length);
        string? copy = "other";
        Assert.Equal(bytes.Length, LuminPackSerializer.Deserialize(bytes.AsSpan(), ref copy));
        Assert.Equal(value, copy);
        string? fromBuffer = null;
        ReadPooled(bytes, buffer => LuminPackSerializer.Deserialize(buffer, ref fromBuffer));
        Assert.Equal(value, fromBuffer);
    }

    /// <summary>Checks a long string, whose payload exceeds the short vector copies, through the exact record width.</summary>
    [Fact]
    public void LongStringRootUsesTheExactRecordWidth()
    {
        string value = new string('x', 300) + "\uDBFF";
        byte[] bytes = LuminPackSerializer.Serialize(value);
        Pooled(bytes, buffer => LuminPackSerializer.Serialize(value, buffer));
        Assert.Equal(sizeof(int) + value.Length * sizeof(char), bytes.Length);
        string copy = null!;
        LuminPackSerializer.Deserialize(bytes.AsSpan(), ref copy);
        Assert.Equal(value, copy);
    }

    /// <summary>Checks the string-bearing framework records against the pooled path and reads them back.</summary>
    [Fact]
    public void StringBearingFrameworkRecordsMatchThePooledPath()
    {
        foreach (Uri? uri in new[] { new Uri("https://example.com/path?query=value"), new Uri("a/b c", UriKind.Relative), null })
        {
            Uri? value = uri;
            byte[] bytes = LuminPackSerializer.Serialize(value);
            Pooled(bytes, buffer => LuminPackSerializer.Serialize(value, buffer));
            Uri? copy = null;
            Assert.Equal(bytes.Length, LuminPackSerializer.Deserialize(bytes.AsSpan(), ref copy));
            Assert.Equal(value?.OriginalString, copy?.OriginalString);
        }

        foreach (CultureInfo? culture in new[] { CultureInfo.GetCultureInfo("en-US"), CultureInfo.InvariantCulture, null })
        {
            CultureInfo? value = culture;
            byte[] bytes = LuminPackSerializer.Serialize(value);
            Pooled(bytes, buffer => LuminPackSerializer.Serialize(value, buffer));
            CultureInfo? copy = null;
            Assert.Equal(bytes.Length, LuminPackSerializer.Deserialize(bytes.AsSpan(), ref copy));
            Assert.Equal(value?.Name, copy?.Name);
        }

        var chunked = new StringBuilder("Hello World");
        for (int i = 0; i < 40; i++) chunked.Append('!', 17).Append('é');
        foreach (StringBuilder? builder in new[] { chunked, new StringBuilder(), null })
        {
            StringBuilder? value = builder;
            byte[] bytes = LuminPackSerializer.Serialize(value);
            Pooled(bytes, buffer => LuminPackSerializer.Serialize(value, buffer));
            StringBuilder? copy = null;
            Assert.Equal(bytes.Length, LuminPackSerializer.Deserialize(bytes.AsSpan(), ref copy));
            Assert.Equal(value?.ToString(), copy?.ToString());
        }
    }

    /// <summary>Checks pairs and tuples with string members and Lazy scalars, including null members and roots.</summary>
    [Fact]
    public void PairTupleAndLazyRecordsMatchThePooledPath()
    {
        foreach (var pair in new[] { new KeyValuePair<int, string>(7, "seven"), new KeyValuePair<int, string>(-1, null!) })
        {
            var value = pair;
            byte[] bytes = LuminPackSerializer.Serialize(value);
            Pooled(bytes, buffer => LuminPackSerializer.Serialize(value, buffer));
            KeyValuePair<int, string> copy = default;
            Assert.Equal(bytes.Length, LuminPackSerializer.Deserialize(bytes.AsSpan(), ref copy));
            Assert.Equal(value, copy);
        }

        foreach (var tuple in new[] { Tuple.Create(42, "Hello", true), new Tuple<int, string, bool>(0, null!, false), null })
        {
            var value = tuple;
            byte[] bytes = LuminPackSerializer.Serialize(value);
            Pooled(bytes, buffer => LuminPackSerializer.Serialize(value, buffer));
            Tuple<int, string, bool>? copy = null;
            Assert.Equal(bytes.Length, LuminPackSerializer.Deserialize(bytes.AsSpan(), ref copy));
            Assert.Equal(value, copy);
        }

        foreach (var tuple in new[] { (42, "Hello", true), (int.MinValue, (string)null!, false) })
        {
            var value = tuple;
            byte[] bytes = LuminPackSerializer.Serialize(value);
            Pooled(bytes, buffer => LuminPackSerializer.Serialize(value, buffer));
            (int, string, bool) copy = default;
            Assert.Equal(bytes.Length, LuminPackSerializer.Deserialize(bytes.AsSpan(), ref copy));
            Assert.Equal(value, copy);
        }

        int factoryCalls = 0;
        foreach (var lazy in new[] { new Lazy<int>(() => { factoryCalls++; return 42; }), null })
        {
            var value = lazy;
            byte[] bytes = LuminPackSerializer.Serialize(value);
            Pooled(bytes, buffer => LuminPackSerializer.Serialize(value, buffer));
            Lazy<int>? copy = null;
            Assert.Equal(bytes.Length, LuminPackSerializer.Deserialize(bytes.AsSpan(), ref copy));
            Assert.Equal(value?.Value, copy?.Value);
        }
        Assert.Equal(1, factoryCalls);
    }

    /// <summary>Checks BigInteger roots write the count-prefixed two's-complement record of the pooled path.</summary>
    [Fact]
    public void BigIntegerRootWritesItsRecordOnce()
    {
        foreach (var number in new[] { BigInteger.Zero, BigInteger.MinusOne, BigInteger.Parse("123456789012345678901234567890"), BigInteger.Pow(-7, 400) })
        {
            var value = number;
            byte[] bytes = LuminPackSerializer.Serialize(value);
            Pooled(bytes, buffer => LuminPackSerializer.Serialize(value, buffer));
            Assert.Equal(sizeof(int) + value.GetByteCount(), bytes.Length);
            Assert.Equal(value.GetByteCount(), BitConverter.ToInt32(bytes, 0));
            BigInteger copy = default;
            Assert.Equal(bytes.Length, LuminPackSerializer.Deserialize(bytes.AsSpan(), ref copy));
            Assert.Equal(value, copy);
        }
    }

    /// <summary>Compares a direct result with the record the pooled reusable-buffer entry writes.</summary>
    /// <param name="direct">The allocating concrete entry's result.</param>
    /// <param name="write">Writes the same value through the reusable-buffer entry.</param>
    private static void Pooled(byte[] direct, Action<LuminBufferWriter> write)
    {
        var buffer = LuminBufferWriterPool.Rent();
        try
        {
            write(buffer);
            Assert.Equal(buffer.GetSpan().ToArray(), direct);
        }
        finally
        {
            LuminBufferWriterPool.Return(buffer);
        }
    }

    /// <summary>Reads a record through the reusable-buffer entry.</summary>
    /// <param name="bytes">The complete record.</param>
    /// <param name="read">Reads from the buffer and returns the consumed width.</param>
    private static void ReadPooled(byte[] bytes, Func<LuminBufferWriter, int> read)
    {
        var buffer = LuminBufferWriterPool.Rent();
        try
        {
            buffer.LoadSpan(bytes);
            Assert.Equal(bytes.Length, read(buffer));
        }
        finally
        {
            LuminBufferWriterPool.Return(buffer);
        }
    }
}
