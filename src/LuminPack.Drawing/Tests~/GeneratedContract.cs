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
        var source = new PlatformPayload { Single = global::System.Drawing.Color.FromName("CustomColor"), Items = new LuminPackValue<global::System.Drawing.Color>[] { global::System.Drawing.Color.FromName("CustomColor") }, List = new() { global::System.Drawing.Color.FromName("CustomColor") } };
        byte[] bytes = LuminPackSerializer.Serialize(source);
        PlatformPayload? restored = null;
        LuminPackSerializer.Deserialize(bytes, ref restored);
        CodecContract.Assert(restored is not null && EqualityComparer<global::System.Drawing.Color>.Default.Equals(source.Single.Value,restored.Single.Value), "Generated platform value failed.");
        CodecContract.Assert(restored!.Items!.Length == 1 && restored.List!.Count == 1, "Generated platform collections failed.");
        CodecContract.Assert(bytes.AsSpan().SequenceEqual(LuminPackSerializer.Serialize(restored)), "Generated platform bytes changed.");
        LuminPackSerializer.Deserialize(bytes,ref restored);
        var native = new NativeColorPayload { Color = global::System.Drawing.Color.FromName("CustomColor") };
        byte[] nativeBytes = LuminPackSerializer.Serialize(native);
        NativeColorPayload? nativeResult = null;
        LuminPackSerializer.Deserialize(nativeBytes, ref nativeResult);
        CodecContract.Assert(nativeResult?.Color == native.Color, "The existing native Color fallback did not use its registered formatter.");
    }
}

/// <summary>Opts external values into a module-owned Custom schema without changing Core or generator sources.</summary>
[LuminPackable]
public partial class PlatformPayload
{
    public LuminPackValue<global::System.Drawing.Color> Single;
    public LuminPackValue<global::System.Drawing.Color>[]? Items;
    public List<LuminPackValue<global::System.Drawing.Color>>? List;
}

/// <summary>Exercises the existing opaque BCL Color dispatch without a wrapper.</summary>
[LuminPackable]
public partial class NativeColorPayload
{
    public global::System.Drawing.Color Color;
}
