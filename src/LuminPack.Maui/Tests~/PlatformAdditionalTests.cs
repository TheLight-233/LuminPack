using LuminPack.Platforms.Tests;
using LuminPack.Maui;

/// <summary>Checks module-specific identity and unusual public values.</summary>
internal static class PlatformAdditionalTests
{
    /// <summary>Runs the additional module contracts.</summary>
    internal static void Run()
    {
        CodecContract.Check(LuminMaui.ColorCodec, null, "Null color");
        CodecContract.Check(LuminMaui.ColorCodec, new global::Microsoft.Maui.Graphics.Color(0.25f,0.5f,0.75f,1f), "Color");
        CodecContract.Check(LuminMaui.ColorCodec, new global::Microsoft.Maui.Graphics.Color(-1f,2f,float.NaN,0.75f), "Public color fields");
        CodecContract.RejectMarker(LuminMaui.ColorCodec, null, "Color");
        GeneratedContract.Run();
    }
}
