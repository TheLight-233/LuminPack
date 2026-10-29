using LuminPack.Code;
using LuminPack.Core;
using LuminPack.Platforms;
using MauiColor = Microsoft.Maui.Graphics.Color;

namespace LuminPack.Maui;

/// <summary>Provides nullable value records for the managed MAUI graphics color.</summary>
public static unsafe partial class LuminMaui
{
    /// <summary>Gets the nullable graphics color codec.</summary>
    public static LuminPackCodec<MauiColor?> ColorCodec { get; } = new(Write, Read);

    /// <summary>Writes a null/non-null marker followed by public Red, Green, Blue and Alpha components.</summary>
    /// <param name="writer">The Core output cursor.</param><param name="value">The nullable color, which must not be mutated concurrently.</param>
    public static void Write(ref LuminPackWriter writer, in MauiColor? value)
    {
        writer.EnsureAdditionalCapacity(value is null ? 1 : 17);
        writer.Write((byte)(value is null ? 0 : 1));
        if (value is null) return;
        writer.Write(value.Red); writer.Write(value.Green); writer.Write(value.Blue); writer.Write(value.Alpha);
    }

    /// <summary>Parses a complete record before creating and publishing a color value.</summary>
    /// <param name="reader">The bounded Core input.</param><param name="value">The nullable destination.</param>
    public static void Read(ref LuminPackReader reader, ref MauiColor? value)
    {
        var pending = reader;
        byte marker = default;
        pending.Read(ref marker);
        if (marker == 0) { value = null; reader = pending; return; }
        if (marker != 1) LuminPackExceptionHelper.ThrowInvalidDataException("Invalid MAUI color marker.");
        pending.EnsureReadable(pending.Position, 16);
        float red = default, green = default, blue = default, alpha = default;
        pending.Read(ref red); pending.Read(ref green); pending.Read(ref blue); pending.Read(ref alpha);
        MauiColor candidate = new(red, green, blue, alpha);
        value = candidate;
        reader = pending;
    }

    /// <summary>Registers the nullable public color record.</summary>
    static partial void RegisterAdditional() => PlatformRegistration.Add<MauiColor?>(&Write, &Read, ColorCodec);
}
