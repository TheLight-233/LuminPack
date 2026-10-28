using LuminPack.Platforms.Tests;
using LuminPack.Avalonia;

/// <summary>Checks module-specific identity and unusual public values.</summary>
internal static class PlatformAdditionalTests
{
    /// <summary>Runs the additional module contracts.</summary>
    internal static void Run()
    {

        LuminPack.Platforms.LuminPackValue<global::Avalonia.RelativePoint> source = new global::Avalonia.RelativePoint(0.25, 0.75, global::Avalonia.RelativeUnit.Relative);
        byte[] bytes = LuminPack.LuminPackSerializer.Serialize(source);
        CodecContract.Assert(bytes.Length == 20, "A closed unmanaged Custom root bypassed its explicit schema.");
        LuminPack.Platforms.LuminPackValue<global::Avalonia.RelativePoint> restored = default;
        LuminPack.LuminPackSerializer.Deserialize(bytes, ref restored);
        CodecContract.Assert(source.Value.Equals(restored.Value), "Closed Custom root did not round trip.");
        GeneratedContract.Run();
    }
}
