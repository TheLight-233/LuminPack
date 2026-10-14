using System;
using System.Linq;
using System.Text;
using LuminPack;
using LuminPack.Option;
using Xunit;

namespace LuminPackUnitTest;

public class JsonOrderedPropertyTest
{
    [Theory]
    [InlineData(LuminPackStringEncoding.UTF8, false)]
    [InlineData(LuminPackStringEncoding.UTF8, true)]
    [InlineData(LuminPackStringEncoding.UTF16, false)]
    [InlineData(LuminPackStringEncoding.UTF16, true)]
    public void OrderedAndFallbackPathsCanAlternate(LuminPackStringEncoding encoding, bool strict)
    {
        var option = new LuminPackSerializerOption { StringEncoding = encoding, ValidateJsonSyntax = strict };
        // Ordered prefix, unknown nested value, escaped name, reordered field,
        // whitespace, duplicate and then an ordered suffix in the same object.
        const string json = "{\"F00\":10,\"F01\":11,\"unknown\":{\"x\":[{\"F02\":99}]}," +
            "\"F0\\u0032\":12,\"F07\":17, \"F03\" : 13,\"F03\":23,\"F04\":14,\"F05\":15}";
        var result = LuminPackSerializer.DeserializeJson<JsonLargeDispatchModel>(json, option);
        Assert.Equal(10, result.F00);
        Assert.Equal(11, result.F01);
        Assert.Equal(12, result.F02);
        Assert.Equal(23, result.F03);
        Assert.Equal(14, result.F04);
        Assert.Equal(15, result.F05);
        Assert.Equal(0, result.F06);
        Assert.Equal(17, result.F07);

        var unicode = LuminPackSerializer.DeserializeJson<JsonSmallDispatchModel>(
            "{\"AA\":1,\"AB\":2,\"名称\":\"中文😀\\n\"}", option);
        Assert.Equal("中文😀\n", unicode.名称);
    }

    [Theory]
    [InlineData(LuminPackStringEncoding.UTF8)]
    [InlineData(LuminPackStringEncoding.UTF16)]
    public void StrictOrderedPathRejectsMalformedSeparatorsAndTruncations(LuminPackStringEncoding encoding)
    {
        var option = new LuminPackSerializerOption { StringEncoding = encoding, ValidateJsonSyntax = true };
        string[] invalid =
        {
            "{\"F00\":0\"F01\":1}", "{,\"F00\":0}", "{\"F00\":0,,\"F01\":1}",
            "{\"F00\":0,}", "{\"F00\":0,\"F01\":}", "{\"F00\":0,\"F01\":]}",
            "{\"F00\":0,\"F01\"1}", "{\"F00\":0,\"F01\"::1}",
            "{\"F00\":0,\"F01\":1]", "{\"F00\":0}{}"
        };
        foreach (string json in invalid)
            Assert.ThrowsAny<Exception>(() => LuminPackSerializer.DeserializeJson<JsonLargeDispatchModel>(json, option));

        const string complete = "{\"F00\":0,\"F01\":1,\"F02\":2}";
        var bytes = encoding == LuminPackStringEncoding.UTF8
            ? Encoding.UTF8.GetBytes(complete) : Encoding.Unicode.GetBytes(complete);
        for (int length = 1; length < bytes.Length; length++)
        {
            int cut = length;
            Assert.ThrowsAny<Exception>(() =>
                LuminPackSerializer.DeserializeJson<JsonLargeDispatchModel>(bytes.AsSpan(0, cut), option));
        }
    }

    [Theory]
    [InlineData(LuminPackStringEncoding.UTF8)]
    [InlineData(LuminPackStringEncoding.UTF16)]
    public void RandomPropertyOrdersPreserveValues(LuminPackStringEncoding encoding)
    {
        var option = new LuminPackSerializerOption { StringEncoding = encoding, ValidateJsonSyntax = true };
        var random = new Random(20261011);
        for (int i = 0; i < 100; i++)
        {
            string json = "{" + string.Join(",", Enumerable.Range(0, 12)
                .OrderBy(_ => random.Next()).Select(n => $"\"F{n:D2}\":{n + 10}")) + "}";
            var value = LuminPackSerializer.DeserializeJson<JsonLargeDispatchModel>(json, option);
            Assert.Equal("{\"F00\":10,\"F01\":11,\"F02\":12,\"F03\":13,\"F04\":14,\"F05\":15," +
                "\"F06\":16,\"F07\":17,\"F08\":18,\"F09\":19,\"F10\":20,\"F11\":21}",
                LuminPackSerializer.SerializeJson(value, option));
        }
    }
}
