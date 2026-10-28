using LuminPack.Code;
using LuminPack.Core;
using LuminPack.Platforms;
using DrawingColor = System.Drawing.Color;

namespace LuminPack.Drawing;

/// <summary>Preserves drawing color identity as empty, known, named or explicit ARGB.</summary>
public static unsafe partial class LuminDrawing
{
    /// <summary>Gets the color codec, retaining named/system colors and Empty.</summary>
    public static LuminPackCodec<DrawingColor> ColorCodec { get; } = new(Write, Read, Size);

    /// <summary>Writes color identity followed by the matching public value record.</summary>
    /// <param name="writer">The Core output cursor.</param><param name="value">The color.</param>
    public static void Write(ref LuminPackWriter writer, in DrawingColor value)
    {
        writer.EnsureAdditionalCapacity(ColorCodec.GetSize(value));
        if (value.IsEmpty) { writer.Write((byte)0); return; }
        if (value.IsKnownColor) { writer.Write((byte)1); writer.Write((int)value.ToKnownColor()); return; }
        if (value.IsNamedColor) { writer.Write((byte)2); writer.Write(value.Name); return; }
        writer.Write((byte)3);
        writer.Write(value.ToArgb());
    }

    /// <summary>Reads a complete identity record before publishing the cursor and destination.</summary>
    /// <param name="reader">The bounded Core input.</param><param name="value">The color destination.</param>
    public static void Read(ref LuminPackReader reader, ref DrawingColor value)
    {
        var pending = reader;
        byte kind = default;
        pending.Read(ref kind);
        DrawingColor candidate = default;
        if (kind == 1)
        {
            int known = default;
            pending.Read(ref known);
            if (!Enum.IsDefined((System.Drawing.KnownColor)known)) LuminPackExceptionHelper.ThrowInvalidDataException("Invalid known drawing color.");
            candidate = DrawingColor.FromKnownColor((System.Drawing.KnownColor)known);
        }
        else if (kind == 2)
        {
            string? name = null;
            pending.Read(ref name);
            if (name is null) LuminPackExceptionHelper.ThrowInvalidDataException("A named drawing color requires its name.");
            candidate = DrawingColor.FromName(name!);
        }
        else if (kind == 3)
        {
            int argb = default;
            pending.Read(ref argb);
            candidate = DrawingColor.FromArgb(argb);
        }
        else if (kind != 0) LuminPackExceptionHelper.ThrowInvalidDataException("Invalid drawing color marker.");
        value = candidate;
        reader = pending;
    }

    /// <summary>Adds the exact color identity and public payload width.</summary>
    /// <param name="evaluator">The Core size accumulator.</param><param name="value">The color.</param>
    public static void Size(ref LuminPackEvaluator evaluator, in DrawingColor value)
    {
        if (value.IsEmpty) evaluator.Add(1);
        else if (value.IsKnownColor || !value.IsNamedColor) evaluator.Add(5);
        else
        {
            string? name = value.Name;
            evaluator.Add(checked(1 + evaluator.GetStringLength(ref name)));
        }
    }

    /// <summary>Registers the variable color identity record.</summary>
    public static void Register() => PlatformRegistration.Add<DrawingColor>(&Write, &Read, &Size, ColorCodec);
}
