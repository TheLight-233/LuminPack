using LuminPack.Platforms.Tests;
using LuminPack.Wpf;

/// <summary>Checks module-specific identity and unusual public values.</summary>
internal static class PlatformAdditionalTests
{
    /// <summary>Runs the additional module contracts.</summary>
    internal static void Run()
    {
        CodecContract.Check(LuminWpf.ColorCodec, global::System.Windows.Media.Colors.Red, "sRGB color");
        CodecContract.Check(LuminWpf.ColorCodec, global::System.Windows.Media.Color.FromScRgb(0.75f,-0.25f,2f,0.125f), "HDR color");
        LuminPack.Platforms.LuminPackValue<global::System.Windows.Size> empty = global::System.Windows.Size.Empty;
        byte[] emptyBytes = LuminPack.LuminPackSerializer.Serialize(empty);
        CodecContract.Assert(emptyBytes.Length == 17, "A closed unmanaged Custom root lost its Empty marker.");
        LuminPack.Platforms.LuminPackValue<global::System.Windows.Size> restored = default;
        LuminPack.LuminPackSerializer.Deserialize(emptyBytes, ref restored);
        CodecContract.Assert(restored.Value.IsEmpty, "A closed Custom root lost Empty geometry.");
        GeneratedContract.Run();
    }
}
