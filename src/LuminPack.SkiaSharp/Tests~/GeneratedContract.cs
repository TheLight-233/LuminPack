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
        var source = new PlatformPayload { Single = new global::SkiaSharp.SKPoint(1, 2), Items = new LuminPackValue<global::SkiaSharp.SKPoint>[] { new global::SkiaSharp.SKPoint(1, 2) }, List = new() { new global::SkiaSharp.SKPoint(1, 2) } };
        byte[] bytes = LuminPackSerializer.Serialize(source);
        PlatformPayload? restored = null;
        LuminPackSerializer.Deserialize(bytes, ref restored);
        CodecContract.Assert(restored is not null && EqualityComparer<global::SkiaSharp.SKPoint>.Default.Equals(source.Single.Value,restored.Single.Value), "Generated platform value failed.");
        CodecContract.Assert(restored!.Items!.Length == 1 && restored.List!.Count == 1, "Generated platform collections failed.");
        CodecContract.Assert(bytes.AsSpan().SequenceEqual(LuminPackSerializer.Serialize(restored)), "Generated platform bytes changed.");
        LuminPackSerializer.Deserialize(bytes,ref restored);
    }
}

/// <summary>Opts external values into a module-owned Custom schema without changing Core or generator sources.</summary>
[LuminPackable]
public partial class PlatformPayload
{
    public LuminPackValue<global::SkiaSharp.SKPoint> Single;
    public LuminPackValue<global::SkiaSharp.SKPoint>[]? Items;
    public List<LuminPackValue<global::SkiaSharp.SKPoint>>? List;
}
