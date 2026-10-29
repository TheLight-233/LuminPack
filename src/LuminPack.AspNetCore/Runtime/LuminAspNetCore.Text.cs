using LuminPack.Core;
using LuminPack.Platforms;

namespace LuminPack.AspNetCore;

public static unsafe partial class LuminAspNetCore
{
    /// <summary>Gets the logical-value StringSegment codec.</summary>
    public static LuminPackCodec<global::Microsoft.Extensions.Primitives.StringSegment> StringSegmentCodec { get; } = new(Write, Read);

    /// <summary>Writes only the public logical text value, preserving null and empty.</summary>
    /// <param name="writer">The Core cursor.</param><param name="value">The HTTP value.</param>
    public static void Write(ref LuminPackWriter writer, in global::Microsoft.Extensions.Primitives.StringSegment value) => writer.Write(value.Value);

    /// <summary>Parses and validates the public value before publishing the cursor or destination.</summary>
    /// <param name="reader">The bounded Core cursor.</param><param name="value">The destination.</param>
    public static void Read(ref LuminPackReader reader, ref global::Microsoft.Extensions.Primitives.StringSegment value)
    {
        var pending = reader;
        string? text = null;
        pending.Read(ref text);
        global::Microsoft.Extensions.Primitives.StringSegment candidate = text is null ? default : new global::Microsoft.Extensions.Primitives.StringSegment(text);
        value = candidate;
        reader = pending;
    }

    /// <summary>Gets the logical-value HostString codec.</summary>
    public static LuminPackCodec<global::Microsoft.AspNetCore.Http.HostString> HostStringCodec { get; } = new(Write, Read);

    /// <summary>Writes only the public logical text value, preserving null and empty.</summary>
    /// <param name="writer">The Core cursor.</param><param name="value">The HTTP value.</param>
    public static void Write(ref LuminPackWriter writer, in global::Microsoft.AspNetCore.Http.HostString value) => writer.Write(value.Value);

    /// <summary>Parses and validates the public value before publishing the cursor or destination.</summary>
    /// <param name="reader">The bounded Core cursor.</param><param name="value">The destination.</param>
    public static void Read(ref LuminPackReader reader, ref global::Microsoft.AspNetCore.Http.HostString value)
    {
        var pending = reader;
        string? text = null;
        pending.Read(ref text);
        global::Microsoft.AspNetCore.Http.HostString candidate = text is null ? default : new global::Microsoft.AspNetCore.Http.HostString(text);
        value = candidate;
        reader = pending;
    }

    /// <summary>Gets the logical-value PathString codec.</summary>
    public static LuminPackCodec<global::Microsoft.AspNetCore.Http.PathString> PathStringCodec { get; } = new(Write, Read);

    /// <summary>Writes only the public logical text value, preserving null and empty.</summary>
    /// <param name="writer">The Core cursor.</param><param name="value">The HTTP value.</param>
    public static void Write(ref LuminPackWriter writer, in global::Microsoft.AspNetCore.Http.PathString value) => writer.Write(value.Value);

    /// <summary>Parses and validates the public value before publishing the cursor or destination.</summary>
    /// <param name="reader">The bounded Core cursor.</param><param name="value">The destination.</param>
    public static void Read(ref LuminPackReader reader, ref global::Microsoft.AspNetCore.Http.PathString value)
    {
        var pending = reader;
        string? text = null;
        pending.Read(ref text);
        global::Microsoft.AspNetCore.Http.PathString candidate = text is null ? default : new global::Microsoft.AspNetCore.Http.PathString(text);
        value = candidate;
        reader = pending;
    }

    /// <summary>Gets the logical-value QueryString codec.</summary>
    public static LuminPackCodec<global::Microsoft.AspNetCore.Http.QueryString> QueryStringCodec { get; } = new(Write, Read);

    /// <summary>Writes only the public logical text value, preserving null and empty.</summary>
    /// <param name="writer">The Core cursor.</param><param name="value">The HTTP value.</param>
    public static void Write(ref LuminPackWriter writer, in global::Microsoft.AspNetCore.Http.QueryString value) => writer.Write(value.Value);

    /// <summary>Parses and validates the public value before publishing the cursor or destination.</summary>
    /// <param name="reader">The bounded Core cursor.</param><param name="value">The destination.</param>
    public static void Read(ref LuminPackReader reader, ref global::Microsoft.AspNetCore.Http.QueryString value)
    {
        var pending = reader;
        string? text = null;
        pending.Read(ref text);
        global::Microsoft.AspNetCore.Http.QueryString candidate = text is null ? default : new global::Microsoft.AspNetCore.Http.QueryString(text);
        value = candidate;
        reader = pending;
    }

    /// <summary>Gets the logical-value FragmentString codec.</summary>
    public static LuminPackCodec<global::Microsoft.AspNetCore.Http.FragmentString> FragmentStringCodec { get; } = new(Write, Read);

    /// <summary>Writes only the public logical text value, preserving null and empty.</summary>
    /// <param name="writer">The Core cursor.</param><param name="value">The HTTP value.</param>
    public static void Write(ref LuminPackWriter writer, in global::Microsoft.AspNetCore.Http.FragmentString value) => writer.Write(value.Value);

    /// <summary>Parses and validates the public value before publishing the cursor or destination.</summary>
    /// <param name="reader">The bounded Core cursor.</param><param name="value">The destination.</param>
    public static void Read(ref LuminPackReader reader, ref global::Microsoft.AspNetCore.Http.FragmentString value)
    {
        var pending = reader;
        string? text = null;
        pending.Read(ref text);
        global::Microsoft.AspNetCore.Http.FragmentString candidate = text is null ? default : new global::Microsoft.AspNetCore.Http.FragmentString(text);
        value = candidate;
        reader = pending;
    }

    /// <summary>Registers the public text records and their generated Custom wrappers.</summary>
    static partial void RegisterTextValues()
    {
        PlatformRegistration.Add<global::Microsoft.Extensions.Primitives.StringSegment>(&Write, &Read, StringSegmentCodec);
        PlatformRegistration.Add<global::Microsoft.AspNetCore.Http.HostString>(&Write, &Read, HostStringCodec);
        PlatformRegistration.Add<global::Microsoft.AspNetCore.Http.PathString>(&Write, &Read, PathStringCodec);
        PlatformRegistration.Add<global::Microsoft.AspNetCore.Http.QueryString>(&Write, &Read, QueryStringCodec);
        PlatformRegistration.Add<global::Microsoft.AspNetCore.Http.FragmentString>(&Write, &Read, FragmentStringCodec);
    }
}
