using LuminPack;
using LuminPack.Attribute;
using LuminPack.Platforms;
using LuminPack.Platforms.Tests;

/// <summary>Checks the existing Custom mechanism for external platform values in ordinary generated fields and collections.</summary>
internal static class GeneratedContract
{
    /// <summary>Runs fresh/reused roots, arrays, lists and unchanged binary reserialization.</summary>
    internal static void Run()
    {
        var source = new PlatformPayload { Single = new global::Microsoft.Maui.Graphics.Point(1.25, 2.25), Items = new LuminPackValue<global::Microsoft.Maui.Graphics.Point>[] { new global::Microsoft.Maui.Graphics.Point(1.25, 2.25) }, List = new() { new global::Microsoft.Maui.Graphics.Point(1.25, 2.25) } };
        byte[] bytes = LuminPackSerializer.Serialize(source);
        PlatformPayload? restored = null;
        LuminPackSerializer.Deserialize(bytes, ref restored);
        CodecContract.Assert(restored is not null && EqualityComparer<global::Microsoft.Maui.Graphics.Point>.Default.Equals(source.Single.Value,restored.Single.Value), "Generated platform value failed.");
        CodecContract.Assert(restored!.Items!.Length == 1 && restored.List!.Count == 1, "Generated platform collections failed.");
        CodecContract.Assert(bytes.AsSpan().SequenceEqual(LuminPackSerializer.Serialize(restored)), "Generated platform bytes changed.");
        LuminPackSerializer.Deserialize(bytes,ref restored);
    }
}

/// <summary>Opts external values into a module-owned Custom schema without changing Core or generator sources.</summary>
[LuminPackable]
public partial class PlatformPayload
{
    public LuminPackValue<global::Microsoft.Maui.Graphics.Point> Single;
    public LuminPackValue<global::Microsoft.Maui.Graphics.Point>[]? Items;
    public List<LuminPackValue<global::Microsoft.Maui.Graphics.Point>>? List;
}
