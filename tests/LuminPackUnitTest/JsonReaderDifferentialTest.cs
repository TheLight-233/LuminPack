using System.Globalization;
using System.Text;
using System.Text.Json;
using LuminPack;
using LuminPack.Core;
using LuminPack.Option;
using Xunit;

namespace LuminPackUnitTest;

// Differential fuzzing for the JSON reader (phase A) against System.Text.Json as an independent oracle,
// as the task book asks for. Reflection-based JsonSerializer is disabled in this assembly (AOT), so the
// oracle is JsonDocument/JsonElement, which is the raw reader plus correctly rounded number accessors.
//
// Contract under test:
//   * for strict-clean documents the reader and the oracle must agree on accept/reject and on the decoded
//     values bit-for-bit;
//   * the reader must never accept a document the oracle rejects;
//   * where the reader is deliberately stricter than System.Text.Json (leading zeros, "1." and the other
//     shapes the task book lists as must-reject), that is allowed and counted.
/// <summary>Provides json reader differential test functionality.</summary>
public class JsonReaderDifferentialTest
{
    private static readonly TestSerializationProfile s_strictUtf8 =
        TestSerializationProfile.Utf8 with { ValidateJsonSyntax = true };
    private static readonly TestSerializationProfile s_strictUtf16 =
        TestSerializationProfile.Utf16 with { ValidateJsonSyntax = true };

    private const int CaseCount = 4000;

    /// <summary>Verifies number documents agree with system text json.</summary>
    [Fact]
    public void NumberDocumentsAgreeWithSystemTextJson()
    {
        var rng = new Random(20261012);
        int checkedDocuments = 0;
        int overflowRejections = 0;

        for (int i = 0; i < CaseCount; i++)
        {
            string json = "[" + string.Join(",", RandomNumbers(rng, 1 + rng.Next(6))) + "]";

            bool oracleAccepted = TryOracleDoubles(json, out double[] oracleValues);
            bool readerAccepted = TryReaderDoubles(json, out double[] readerValues);

            if (oracleAccepted && !readerAccepted)
            {
                // Documented policy difference: the reader reports an error for numbers that overflow
                // double (e.g. 1e400) while System.Text.Json's GetDouble returns Infinity. Allow it only
                // when the oracle's own value is non-finite for that document.
                if (LooksLikeOverflow(oracleValues))
                {
                    overflowRejections++;
                    continue;
                }
            }

            Assert.True(oracleAccepted == readerAccepted,
                $"accept/reject mismatch for '{json}' (oracle={oracleAccepted}, reader={readerAccepted})");

            if (!oracleAccepted)
                continue;

            for (int v = 0; v < oracleValues.Length; v++)
            {
                Assert.True(
                    BitConverter.DoubleToUInt64Bits(oracleValues[v]) == BitConverter.DoubleToUInt64Bits(readerValues[v]),
                    $"value mismatch for '{json}' at {v}: oracle={oracleValues[v]:R} reader={readerValues[v]:R}");
            }

            checkedDocuments++;
        }

        Assert.True(checkedDocuments > 3000, $"only {checkedDocuments} documents were accepted by both");
        Assert.True(overflowRejections * 10 < checkedDocuments,
            $"the reader rejected {overflowRejections} documents the oracle accepted as finite values");
    }

    /// <summary>True when the oracle's values are non-finite, i.e. the document overflowed double.</summary>
    /// <param name="values">The values used by this operation.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    private static bool LooksLikeOverflow(double[] values)
    {
        foreach (double value in values)
        {
            if (!double.IsFinite(value))
                return true;
        }

        return false;
    }

    /// <summary>Verifies integer documents agree with system text json.</summary>
    [Fact]
    public void IntegerDocumentsAgreeWithSystemTextJson()
    {
        var rng = new Random(20261014);

        for (int i = 0; i < CaseCount; i++)
        {
            string json = "[" + string.Join(",", RandomIntegers(rng, 1 + rng.Next(6))) + "]";

            bool oracleAccepted = TryOracleLongs(json, out long[] oracleValues);
            bool readerAccepted = TryReaderLongs(json, out long[] readerValues);

            Assert.True(oracleAccepted == readerAccepted,
                $"accept/reject mismatch for '{json}' (oracle={oracleAccepted}, reader={readerAccepted})");

            if (oracleAccepted)
                Assert.Equal(oracleValues, readerValues);
        }
    }

    /// <summary>Verifies mutated documents never accepted when the oracle rejects them.</summary>
    [Fact]
    public void MutatedDocumentsNeverAcceptedWhenTheOracleRejectsThem()
    {
        var rng = new Random(20261013);
        int oracleRejections = 0, readerStricter = 0;

        for (int i = 0; i < CaseCount; i++)
        {
            string json = Mutate(BuildRandomDocument(rng), rng);

            bool oracleAccepted = TryOracleDoubles(json, out _);
            bool readerAccepted = TryReaderDoubles(json, out _);

            if (!oracleAccepted)
            {
                oracleRejections++;
                Assert.False(readerAccepted,
                    $"the reader accepted a document the oracle rejects: '{json}'");
            }
            else if (!readerAccepted)
            {
                readerStricter++;
            }
        }

        Assert.True(oracleRejections > 500, $"only {oracleRejections} mutated documents were rejected");
        Assert.True(readerStricter * 20 < oracleRejections,
            $"the reader was stricter than the oracle in {readerStricter} of {CaseCount} cases, which needs review");
    }

    /// <summary>Verifies malformed numbers are rejected by the reader.</summary>
    [Fact]
    public void MalformedNumbersAreRejectedByTheReader()
    {
        string[] malformed =
        {
            "[01]", "[-01]", "[00]", "[1.]", "[.5]", "[+1]", "[--1]", "[1e]", "[1e+]", "[1e--2]",
            "[1x]", "[NaN]", "[Infinity]", "[1.2.3]", "[-]", "[1 2]", "[1,]", "[,1]", "[1,,2]", "[]extra"
        };

        foreach (string json in malformed)
        {
            Assert.False(TryReaderDoubles(json, out _), $"the reader accepted malformed '{json}'");
            _ = TryOracleDoubles(json, out _); // the oracle's verdict is informational, see the class comment
        }
    }

    /// <summary>Verifies string documents agree with system text json.</summary>
    [Fact]
    public void StringDocumentsAgreeWithSystemTextJson()
    {
        var rng = new Random(20261015);

        for (int i = 0; i < 500; i++)
        {
            var builder = new StringBuilder("[");
            int count = 1 + rng.Next(6);
            for (int s = 0; s < count; s++)
            {
                if (s > 0)
                    builder.Append(',');
                builder.Append('"').Append(EscapeForJson(RandomString(rng))).Append('"');
            }

            string json = builder.Append(']').ToString();

            using JsonDocument oracle = JsonDocument.Parse(json);
            string?[] oracleStrings = FlattenStrings(oracle.RootElement);
            string?[] readerStrings = LuminPackSerializer
                .DeserializeJson<List<string?>>(json, s_strictUtf8)!.ToArray();

            Assert.Equal(oracleStrings.Length, readerStrings.Length);
            for (int s = 0; s < oracleStrings.Length; s++)
                Assert.True(oracleStrings[s] == readerStrings[s],
                    $"string mismatch in '{json}': oracle='{oracleStrings[s]}' reader='{readerStrings[s]}'");
        }
    }

    /// <summary>
    /// Acceptance-level differential test over arbitrary documents: the raw reader walk (read every token and
    /// skip it, then require the end of the document) must agree with the oracle on every generated document.
    /// </summary>
    [Fact]
    public void ObjectDocumentsAreAcceptedByBoth()
    {
        var rng = new Random(20261016);
        int accepted = 0;

        for (int i = 0; i < 1000; i++)
        {
            string json = BuildRandomDocument(rng);

            bool oracleAccepted;
            using (JsonDocument _ = JsonDocument.Parse(json))
                oracleAccepted = true;

            bool readerAccepted = TryReaderWalk(json);

            Assert.True(oracleAccepted == readerAccepted,
                $"accept/reject mismatch for '{json}' (oracle={oracleAccepted}, reader={readerAccepted})");
            Assert.True(readerAccepted);
            accepted++;
        }

        Assert.Equal(1000, accepted);
    }

    /// <summary>Walks the whole document with the raw reader and requires the end of the document.</summary>
    /// <param name="json">The json used by this operation.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    private static bool TryReaderWalk(string json)
    {
        try
        {
            byte[] buffer = Encoding.UTF8.GetBytes(json);
            Span<byte> span = buffer;
            var reader = new LuminPackJsonReader(ref span, new LuminPackReaderOptionalState());
            while (reader.ReadFor(s_strictUtf8))
                reader.SkipFor(s_strictUtf8);

            reader.EnsureEndOfDocumentFor(s_strictUtf8);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    // ---------------------------------------------------------------- oracle / reader

    /// <summary>Attempts to oracle doubles and reports whether it succeeded.</summary>
    /// <param name="json">The json used by this operation.</param>
    /// <param name="values">Receives the values produced by the operation.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    private static bool TryOracleDoubles(string json, out double[] values)
    {
        values = Array.Empty<double>();
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
                return false;

            var result = new List<double>();
            foreach (JsonElement element in document.RootElement.EnumerateArray())
            {
                if (element.ValueKind != JsonValueKind.Number)
                    return false;
                result.Add(element.GetDouble());
            }

            values = result.ToArray();
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>Attempts to oracle longs and reports whether it succeeded.</summary>
    /// <param name="json">The json used by this operation.</param>
    /// <param name="values">Receives the values produced by the operation.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    private static bool TryOracleLongs(string json, out long[] values)
    {
        values = Array.Empty<long>();
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
                return false;

            var result = new List<long>();
            foreach (JsonElement element in document.RootElement.EnumerateArray())
            {
                if (element.ValueKind != JsonValueKind.Number)
                    return false;
                result.Add(element.GetInt64());
            }

            values = result.ToArray();
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>Attempts to reader doubles and reports whether it succeeded.</summary>
    /// <param name="json">The json used by this operation.</param>
    /// <param name="values">Receives the values produced by the operation.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    private static bool TryReaderDoubles(string json, out double[] values)
    {
        try
        {
            values = LuminPackSerializer.DeserializeJson<double[]>(json, s_strictUtf8)!;
            return true;
        }
        catch (Exception)
        {
            values = Array.Empty<double>();
            return false;
        }
    }

    /// <summary>Attempts to reader longs and reports whether it succeeded.</summary>
    /// <param name="json">The json used by this operation.</param>
    /// <param name="values">Receives the values produced by the operation.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    private static bool TryReaderLongs(string json, out long[] values)
    {
        try
        {
            values = LuminPackSerializer.DeserializeJson<long[]>(json, s_strictUtf8)!;
            return true;
        }
        catch (Exception)
        {
            values = Array.Empty<long>();
            return false;
        }
    }

    // ---------------------------------------------------------------- generators

    /// <summary>Verifies random numbers.</summary>
    /// <param name="rng">The rng used by this operation.</param>
    /// <param name="count">The number of entries involved in the operation.</param>
    /// <returns>The result of the operation.</returns>
    private static string[] RandomNumbers(Random rng, int count)
    {
        var result = new string[count];
        for (int i = 0; i < count; i++)
            result[i] = RandomNumber(rng);
        return result;
    }

    /// <summary>Generates a number token that is strict-clean in both parsers (no leading zero, digits after '.' and 'e').</summary>
    /// <param name="rng">The rng used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    private static string RandomNumber(Random rng)
    {
        int digits = 1 + rng.Next(20);
        var builder = new StringBuilder();
        if (rng.Next(4) == 0)
            builder.Append('-');
        if (rng.Next(8) == 0)
        {
            builder.Append('0');
        }
        else
        {
            builder.Append((char)('1' + rng.Next(9)));
            for (int d = 1; d < digits; d++)
                builder.Append((char)('0' + rng.Next(10)));
        }

        if (rng.Next(3) == 0)
        {
            int fraction = 1 + rng.Next(18);
            builder.Append('.');
            for (int d = 0; d < fraction; d++)
                builder.Append((char)('0' + rng.Next(10)));
        }

        if (rng.Next(3) == 0)
        {
            builder.Append(rng.Next(2) == 0 ? 'e' : 'E');
            if (rng.Next(2) == 0)
                builder.Append(rng.Next(2) == 0 ? '+' : '-');
            builder.Append(rng.Next(3) == 0
                ? rng.Next(400).ToString(CultureInfo.InvariantCulture)
                : rng.Next(30).ToString(CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }

    /// <summary>Verifies random integers.</summary>
    /// <param name="rng">The rng used by this operation.</param>
    /// <param name="count">The number of entries involved in the operation.</param>
    /// <returns>The result of the operation.</returns>
    private static string[] RandomIntegers(Random rng, int count)
    {
        var result = new string[count];
        for (int i = 0; i < count; i++)
        {
            var builder = new StringBuilder();
            if (rng.Next(3) == 0)
                builder.Append('-');
            if (rng.Next(4) == 0)
            {
                builder.Append('0');
            }
            else
            {
                builder.Append((char)('1' + rng.Next(9)));
                int digits = rng.Next(18);
                for (int d = 0; d < digits; d++)
                    builder.Append((char)('0' + rng.Next(10)));
            }

            result[i] = builder.ToString();
        }

        return result;
    }

    /// <summary>Verifies build random document.</summary>
    /// <param name="rng">The rng used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    private static string BuildRandomDocument(Random rng)
    {
        int valueCount = 1 + rng.Next(4);
        var builder = new StringBuilder("{");
        for (int i = 0; i < valueCount; i++)
        {
            if (i > 0)
                builder.Append(',');

            string key = (i & 1) == 0 ? "k" + i : "\u952E" + i;
            builder.Append('"').Append(key).Append("\":");

            switch (rng.Next(4))
            {
                case 0:
                    builder.Append(RandomNumber(rng));
                    break;
                case 1:
                    builder.Append('"').Append(EscapeForJson(RandomString(rng))).Append('"');
                    break;
                case 2:
                    builder.Append(rng.Next(2) == 0 ? "true" : "false");
                    break;
                default:
                    builder.Append('[').Append(string.Join(",", RandomNumbers(rng, 1 + rng.Next(3)))).Append(']');
                    break;
            }
        }

        return builder.Append('}').ToString();
    }

    /// <summary>Verifies random string.</summary>
    /// <param name="rng">The rng used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    private static string RandomString(Random rng)
    {
        int length = rng.Next(8);
        var builder = new StringBuilder();
        for (int i = 0; i < length; i++)
        {
            switch (rng.Next(5))
            {
                case 0:
                    builder.Append((char)('a' + rng.Next(26)));
                    break;
                case 1:
                    builder.Append((char)('0' + rng.Next(10)));
                    break;
                case 2:
                    builder.Append('\u4e2d');
                    break;
                case 3:
                    builder.Append('\\');
                    break;
                default:
                    builder.Append('"');
                    break;
            }
        }

        return builder.ToString();
    }

    /// <summary>Verifies escape for json.</summary>
    /// <param name="value">The value consumed or produced by this operation.</param>
    /// <returns>The result of the operation.</returns>
    private static string EscapeForJson(string value) =>
        value.Replace("\\", "\\\\").Replace("\"", "\\\"");

    /// <summary>Verifies mutate.</summary>
    /// <param name="json">The json used by this operation.</param>
    /// <param name="rng">The rng used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    private static string Mutate(string json, Random rng)
    {
        if (json.Length == 0)
            return json;

        int index = rng.Next(json.Length);
        switch (rng.Next(4))
        {
            case 0:
                return json.Remove(index, 1);
            case 1:
                return json.Insert(index, ",");
            case 2:
                return json.Insert(index, " ");
            default:
                return json.Insert(index, json[index].ToString());
        }
    }

    /// <summary>Verifies flatten strings.</summary>
    /// <param name="element">The element used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    private static string?[] FlattenStrings(JsonElement element)
    {
        var values = new List<string?>();
        Collect(element, values);
        return values.ToArray();
    }

    /// <summary>Verifies collect.</summary>
    /// <param name="element">The element used by this operation.</param>
    /// <param name="values">The values used by this operation.</param>
    private static void Collect(JsonElement element, List<string?> values)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                values.Add(element.GetString());
                break;
            case JsonValueKind.Array:
                foreach (JsonElement item in element.EnumerateArray())
                    Collect(item, values);
                break;
            case JsonValueKind.Object:
                foreach (JsonProperty property in element.EnumerateObject())
                    Collect(property.Value, values);
                break;
        }
    }
}
