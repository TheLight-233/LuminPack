using LuminPack.Code;
using LuminPack.Core;
using LuminPack.Platforms;
using WpfColor = System.Windows.Media.Color;

namespace LuminPack.Wpf;

/// <summary>Provides linear scRGB color records without silently discarding external color profiles.</summary>
public static unsafe partial class LuminWpf
{
    /// <summary>Gets the linear scRGB color codec.</summary>
    public static LuminPackCodec<WpfColor> ColorCodec { get; } = new(Write, Read, Size);

    /// <summary>Writes ScA, ScR, ScG and ScB, retaining linear/HDR components.</summary>
    /// <param name="writer">The Core output cursor.</param><param name="value">The color without an external color context.</param>
    public static void Write(ref LuminPackWriter writer, in WpfColor value)
    {
        if (value.ColorContext is not null) LuminPackExceptionHelper.ThrowNotSupportedException("Profile-backed WPF colors require an application color-profile contract.");
        writer.EnsureAdditionalCapacity(16);
        writer.Write(value.ScA);
        writer.Write(value.ScR);
        writer.Write(value.ScG);
        writer.Write(value.ScB);
    }

    /// <summary>Checks and constructs the complete scRGB color before publication.</summary>
    /// <param name="reader">The bounded Core input.</param><param name="value">The color destination.</param>
    public static void Read(ref LuminPackReader reader, ref WpfColor value)
    {
        reader.EnsureReadable(reader.Position, 16);
        var pending = reader;
        float alpha = default, red = default, green = default, blue = default;
        pending.Read(ref alpha); pending.Read(ref red); pending.Read(ref green); pending.Read(ref blue);
        WpfColor candidate = WpfColor.FromScRgb(alpha, red, green, blue);
        value = candidate;
        reader = pending;
    }

    /// <summary>Adds the exact public scRGB record width, rejecting unsupported external profiles.</summary>
    /// <param name="evaluator">The Core size accumulator.</param><param name="value">The color.</param>
    public static void Size(ref LuminPackEvaluator evaluator, in WpfColor value)
    {
        if (value.ColorContext is not null) LuminPackExceptionHelper.ThrowNotSupportedException("Profile-backed WPF colors require an application color-profile contract.");
        evaluator.Add(16);
    }

    /// <summary>Registers the explicit linear color record.</summary>
    static partial void RegisterAdditional() => PlatformRegistration.Add<WpfColor>(&Write, &Read, &Size, ColorCodec);
}
