using LuminPack.Platforms.Tests;
using LuminPack.Drawing;

/// <summary>Checks module-specific identity and unusual public values.</summary>
internal static class PlatformAdditionalTests
{
    /// <summary>Runs the additional module contracts.</summary>
    internal static void Run()
    {
        CodecContract.Check(LuminDrawing.ColorCodec, global::System.Drawing.Color.Empty, "Empty");
        CodecContract.Check(LuminDrawing.ColorCodec, global::System.Drawing.Color.Red, "Known");
        CodecContract.Check(LuminDrawing.ColorCodec, global::System.Drawing.Color.FromKnownColor(global::System.Drawing.KnownColor.Control), "System");
        CodecContract.Check(LuminDrawing.ColorCodec, global::System.Drawing.Color.FromName("CustomColor"), "Named");
        CodecContract.Check(LuminDrawing.ColorCodec, global::System.Drawing.Color.FromArgb(127,1,2,3), "Argb");
        CodecContract.RejectMarker(LuminDrawing.ColorCodec, global::System.Drawing.Color.Empty, "Color");
        GeneratedContract.Run();
    }
}
