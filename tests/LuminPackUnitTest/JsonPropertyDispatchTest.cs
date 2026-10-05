using LuminPack;
using LuminPack.Attribute;
using LuminPack.Option;
using Xunit;

namespace LuminPackUnitTest;

[LuminPackable]
public partial class JsonSmallDispatchModel
{
    public int AA;
    public int AB;
    public string 名称 = "";
}

[LuminPackable]
public partial class JsonLargeDispatchModel
{
    public int F00; public int F01; public int F02; public int F03;
    public int F04; public int F05; public int F06; public int F07;
    public int F08; public int F09; public int F10; public int F11;
}

public class JsonPropertyDispatchTest
{
    [Theory]
    [InlineData(LuminPackStringEncoding.UTF8)]
    [InlineData(LuminPackStringEncoding.UTF16)]
    public void SmallModelsMatchLengthsAndActualNames(LuminPackStringEncoding encoding)
    {
        var option = new LuminPackSerializerOption { StringEncoding = encoding };
        var result = LuminPackSerializer.DeserializeJson<JsonSmallDispatchModel>(
            "{\"AZ\":{\"ignored\":[\"escape\\n\"]},\"A\\u0042\":2,\"名称\":\"中文😀\",\"AA\":1,\"aa\":99}", option);
        Assert.NotNull(result);
        Assert.Equal(1, result.AA);
        Assert.Equal(2, result.AB);
        Assert.Equal("中文😀", result.名称);
    }

    [Theory]
    [InlineData(LuminPackStringEncoding.UTF8)]
    [InlineData(LuminPackStringEncoding.UTF16)]
    public void LargeModelsVerifyNamesAndSkipUnknownValues(LuminPackStringEncoding encoding)
    {
        var option = new LuminPackSerializerOption { StringEncoding = encoding };
        var result = LuminPackSerializer.DeserializeJson<JsonLargeDispatchModel>(
            "{\"F11\":11,\"F99\":[{\"x\":\"\\u4F60\"}],\"F0\\u0030\":1,\"F01\":2,\"f02\":99}", option);
        Assert.NotNull(result);
        Assert.Equal(1, result.F00);
        Assert.Equal(2, result.F01);
        Assert.Equal(0, result.F02);
        Assert.Equal(11, result.F11);
    }

    [Theory]
    [InlineData(LuminPackStringEncoding.UTF8)]
    [InlineData(LuminPackStringEncoding.UTF16)]
    public void ReferenceMetadataIsMatchedInTheCorrectEncoding(LuminPackStringEncoding encoding)
    {
        var option = new LuminPackSerializerOption { StringEncoding = encoding };
        var node = new CircleReferenceSerializationTest.SimpleNode { Name = "中文😀" };
        node.Parent = node;
        string json = LuminPackSerializer.SerializeJson(node, option);
        json = json.Replace("\"$id\"", "\"$i\\u0064\"").Replace("\"$ref\"", "\"$r\\u0065f\"");
        var result = LuminPackSerializer.DeserializeJson<CircleReferenceSerializationTest.SimpleNode>(json, option);
        Assert.NotNull(result);
        Assert.Equal(node.Name, result.Name);
        Assert.Same(result, result.Parent);
    }
}
