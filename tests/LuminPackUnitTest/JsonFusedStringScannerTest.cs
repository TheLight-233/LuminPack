using System.Text;
using System.Text.Json;
using LuminPack;
using LuminPack.Option;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Provides json fused string scanner test functionality.</summary>
public class JsonFusedStringScannerTest
{
    /// <summary>Verifies escapes controls and truncations agree across scan boundaries.</summary>
    /// <param name="encoding">The encoding used by this operation.</param>
    /// <param name="strict">The strict used by this operation.</param>
    [Theory]
    [InlineData(LuminPackStringEncoding.UTF8, false)]
    [InlineData(LuminPackStringEncoding.UTF8, true)]
    [InlineData(LuminPackStringEncoding.UTF16, false)]
    [InlineData(LuminPackStringEncoding.UTF16, true)]
    public void EscapesControlsAndTruncationsAgreeAcrossScanBoundaries(LuminPackStringEncoding encoding, bool strict)
    {
        var option = new LuminPackSerializerOption { StringEncoding = encoding, ValidateJsonSyntax = strict };
        foreach (int length in new[] { 0, 1, 7, 15, 16, 17, 24, 31, 32, 63, 128 })
        {
            string prefix = new('x', length);
            foreach (string suffix in new[] { "tail", "\\\"tail", "\\ntail", "\\\\tail", "\\u4E2Dtail", "\\uD83D\\uDE00tail" })
            {
                string json = "\"" + prefix + suffix + "\"";
                using var document = JsonDocument.Parse(json);
                Assert.Equal(document.RootElement.GetString(), LuminPackSerializer.DeserializeJson<string>(json, option));
                byte[] bytes = encoding == LuminPackStringEncoding.UTF8 ? Encoding.UTF8.GetBytes(json) : Encoding.Unicode.GetBytes(json);
                for (int cut = 1; cut < bytes.Length; cut++)
                {
                    int truncatedLength = cut;
                    Assert.ThrowsAny<Exception>(() => LuminPackSerializer.DeserializeJson<string>(bytes.AsSpan(0, truncatedLength), option));
                }
            }

            for (char control = '\0'; control < 0x20; control++)
            {
                string beforeEscape = "\"" + prefix + control + "\\n\"";
                string afterEscape = "\"" + prefix + "\\n" + control + "\"";
                Assert.ThrowsAny<Exception>(() => LuminPackSerializer.DeserializeJson<string>(beforeEscape, option));
                Assert.ThrowsAny<Exception>(() => LuminPackSerializer.DeserializeJson<string>(afterEscape, option));
            }
        }
    }
}
