using System;

namespace LuminPack.SourceGenerator.Next.Model;

/// <summary>How a read treats its input.</summary>
internal enum InputPolicy
{
    /// <summary>The payload is trusted and reads without marker, length or depth checks.</summary>
    Trusted,

    /// <summary>The payload is untrusted and reads through the checked policy helpers.</summary>
    Untrusted,
}

/// <summary>The declared string encoding.</summary>
internal enum StringEncoding
{
    /// <summary>UTF-8 length-prefixed records.</summary>
    Utf8,

    /// <summary>UTF-16 length-prefixed records.</summary>
    Utf16,
}

/// <summary>The declared string recording mode.</summary>
internal enum StringRecording
{
    /// <summary>The record stores its byte length.</summary>
    Length,

    /// <summary>The record stores a token index.</summary>
    Token,

    /// <summary>The record stores a variable-length payload.</summary>
    VarInt,
}

/// <summary>A stateless wire profile: the settings that select generated string, collection and input operations.</summary>
internal sealed class StringProfile : IEquatable<StringProfile>
{
    /// <summary>The format character.</summary>
    internal readonly char Format;

    /// <summary>The precision byte, or 255 for the default.</summary>
    internal readonly byte Precision;

    /// <summary>The maximum nesting depth.</summary>
    internal readonly int Depth;

    /// <summary>Whether strict parsing is on.</summary>
    internal readonly bool Strict;

    /// <summary>The string encoding.</summary>
    internal readonly StringEncoding Encoding;

    /// <summary>The string recording.</summary>
    internal readonly StringRecording Recording;

    /// <summary>Captures a wire profile.</summary>
    internal StringProfile(char format, byte precision, int depth, bool strict, StringEncoding encoding, StringRecording recording)
    {
        Recording = recording;
        Encoding = encoding;
        Strict = strict;
        Depth = depth;
        Precision = precision;
        Format = format;
    }

    /// <inheritdoc/>
    public bool Equals(StringProfile? other) => other != null && Format == other.Format && Precision == other.Precision &&
        Depth == other.Depth && Strict == other.Strict && Encoding == other.Encoding && Recording == other.Recording;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as StringProfile);

    /// <inheritdoc/>
    public override int GetHashCode() => ((int)Encoding << 3) | (int)Recording;
}

/// <summary>The wire policy that applies to a type or to one member declaration.</summary>
internal sealed class WirePolicy : IEquatable<WirePolicy>
{
    /// <summary>The default trusted policy, which applies when a declaration states no options.</summary>
    internal static readonly WirePolicy Default = new WirePolicy(InputPolicy.Trusted,
        new StringProfile('G', byte.MaxValue, 1024, strict: false, StringEncoding.Utf16, StringRecording.Length), null, false);

    /// <summary>The input policy.</summary>
    internal readonly InputPolicy Input;

    /// <summary>The string profile.</summary>
    internal readonly StringProfile Strings;

    /// <summary>The complete-record byte bound, or null.</summary>
    internal readonly int? Bound;

    /// <summary>Whether the payload tracks object references.</summary>
    internal readonly bool TrackReferences;

    /// <summary>Captures a wire policy.</summary>
    internal WirePolicy(InputPolicy input, StringProfile strings, int? bound, bool trackReferences)
    {
        TrackReferences = trackReferences;
        Bound = bound;
        Strings = strings;
        Input = input;
    }

    /// <inheritdoc/>
    public bool Equals(WirePolicy? other) => other != null && Input == other.Input && Strings.Equals(other.Strings) &&
        Bound == other.Bound && TrackReferences == other.TrackReferences;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as WirePolicy);

    /// <inheritdoc/>
    public override int GetHashCode() => Strings.GetHashCode();
}
