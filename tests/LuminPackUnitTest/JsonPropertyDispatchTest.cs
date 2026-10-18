using LuminPack;
using LuminPack.Attribute;
using LuminPack.Option;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Provides json small dispatch model functionality.</summary>
[LuminPackable]
public partial class JsonSmallDispatchModel
{
    /// <summary>Stores the aa.</summary>
    public int AA;
    /// <summary>Stores the ab.</summary>
    public int AB;
    /// <summary>Stores the name in the Unicode-name regression fixture.</summary>
    public string \u540d\u79f0 = "";
}

/// <summary>Provides json large dispatch model functionality.</summary>
[LuminPackable]
public partial class JsonLargeDispatchModel
{
    /// <summary>Stores the f00.</summary>
    public int F00;
    /// <summary>Stores the f01.</summary>
    public int F01;
    /// <summary>Stores the f02.</summary>
    public int F02;
    /// <summary>Stores the f03.</summary>
    public int F03;
    /// <summary>Stores the f04.</summary>
    public int F04;
    /// <summary>Stores the f05.</summary>
    public int F05;
    /// <summary>Stores the f06.</summary>
    public int F06;
    /// <summary>Stores the f07.</summary>
    public int F07;
    /// <summary>Stores the f08.</summary>
    public int F08;
    /// <summary>Stores the f09.</summary>
    public int F09;
    /// <summary>Stores the f10.</summary>
    public int F10;
    /// <summary>Stores the f11.</summary>
    public int F11;
}

/// <summary>Provides json property dispatch test functionality.</summary>
public class JsonPropertyDispatchTest
{
    /// <summary>Verifies small models match lengths and actual names.</summary>
    /// <param name="encoding">The encoding used by this operation.</param>
    [Theory]
    [InlineData(LuminPackStringEncoding.UTF8)]
    [InlineData(LuminPackStringEncoding.UTF16)]
    public void SmallModelsMatchLengthsAndActualNames(LuminPackStringEncoding encoding)
    {
        var option = new TestSerializationProfile { StringEncoding = encoding };
        var result = LuminPackSerializer.DeserializeJson<JsonSmallDispatchModel>(
            "{\u0022AZ\u0022:{\u0022ignored\u0022:[\u0022escape\\n\u0022]},\u0022A\\u0042\u0022:2,\u0022\u540D\u79F0\u0022:\u0022\u4E2D\u6587\uD83D\uDE00\u0022,\u0022AA\u0022:1,\u0022aa\u0022:99}", option);
        Assert.NotNull(result);
        Assert.Equal(1, result.AA);
        Assert.Equal(2, result.AB);
        Assert.Equal("\u4E2D\u6587\uD83D\uDE00", result.\u540d\u79f0);
    }

    /// <summary>Verifies large models verify names and skip unknown values.</summary>
    /// <param name="encoding">The encoding used by this operation.</param>
    [Theory]
    [InlineData(LuminPackStringEncoding.UTF8)]
    [InlineData(LuminPackStringEncoding.UTF16)]
    public void LargeModelsVerifyNamesAndSkipUnknownValues(LuminPackStringEncoding encoding)
    {
        var option = new TestSerializationProfile { StringEncoding = encoding };
        var result = LuminPackSerializer.DeserializeJson<JsonLargeDispatchModel>(
            "{\"F11\":11,\"F99\":[{\"x\":\"\\u4F60\"}],\"F0\\u0030\":1,\"F01\":2,\"f02\":99}", option);
        Assert.NotNull(result);
        Assert.Equal(1, result.F00);
        Assert.Equal(2, result.F01);
        Assert.Equal(0, result.F02);
        Assert.Equal(11, result.F11);
    }

    /// <summary>Verifies reference metadata is matched in the correct encoding.</summary>
    /// <param name="encoding">The encoding used by this operation.</param>
    [Theory]
    [InlineData(LuminPackStringEncoding.UTF8)]
    [InlineData(LuminPackStringEncoding.UTF16)]
    public void ReferenceMetadataIsMatchedInTheCorrectEncoding(LuminPackStringEncoding encoding)
    {
        var option = new TestSerializationProfile { StringEncoding = encoding };
        var node = new CircleReferenceSerializationTest.SimpleNode { Name = "\u4E2D\u6587\uD83D\uDE00" };
        node.Parent = node;
        string json = LuminPackSerializer.SerializeJson(node, option);
        json = json.Replace("\"$id\"", "\"$i\\u0064\"").Replace("\"$ref\"", "\"$r\\u0065f\"");
        var result = LuminPackSerializer.DeserializeJson<CircleReferenceSerializationTest.SimpleNode>(json, option);
        Assert.NotNull(result);
        Assert.Equal(node.Name, result.Name);
        Assert.Same(result, result.Parent);
    }
}
