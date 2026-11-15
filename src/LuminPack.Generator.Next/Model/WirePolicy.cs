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
    /// <param name="format">The format supplied to <c>StringProfile</c>.</param>
    /// <param name="precision">The precision supplied to <c>StringProfile</c>.</param>
    /// <param name="depth">The current nesting depth.</param>
    /// <param name="strict">Whether to enable strict for this operation.</param>
    /// <param name="encoding">The encoding supplied to <c>StringProfile</c>.</param>
    /// <param name="recording">The recording supplied to <c>StringProfile</c>.</param>
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
    public bool Equals(StringProfile? other) => ReferenceEquals(this, other) || other != null && Format == other.Format &&
        Precision == other.Precision && Depth == other.Depth && Strict == other.Strict && Encoding == other.Encoding &&
        Recording == other.Recording;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as StringProfile);

    /// <inheritdoc/>
    public override int GetHashCode() => ((int)Encoding << 3) | (int)Recording;
}

/// <summary>The wire policy that applies to a type or to one member declaration.</summary>
internal sealed class WirePolicy : IEquatable<WirePolicy>
{
    /// <summary>The default hostile-input policy, which applies when a declaration states no options.</summary>
    /// <remarks>Generated reads check object markers, Boolean domains and declared limits unless a declaration opts out
    /// with <c>UntrustedData = false</c>. This matches the runtime default that the legacy pipeline compiled.</remarks>
    internal static readonly WirePolicy Default = new WirePolicy(InputPolicy.Untrusted,
        new StringProfile('G', byte.MaxValue, 1024, strict: false, StringEncoding.Utf16, StringRecording.Length), null, false);

    /// <summary>The input policy.</summary>
    internal readonly InputPolicy Input;

    /// <summary>The string profile.</summary>
    internal readonly StringProfile Strings;

    /// <summary>Whether a declaration explicitly owns its serialization profile.</summary>
    internal readonly bool Declared;

    /// <summary>The complete-record byte bound, or null.</summary>
    internal readonly int? Bound;

    /// <summary>Whether the payload tracks object references.</summary>
    internal readonly bool TrackReferences;

    /// <summary>The frozen external-input limits of an untrusted read.</summary>
    internal readonly InputLimits Limits;

    /// <summary>Captures a wire policy.</summary>
    /// <param name="input">The input to process.</param>
    /// <param name="strings">The strings supplied to <c>WirePolicy</c>.</param>
    /// <param name="bound">The bound supplied to <c>WirePolicy</c>.</param>
    /// <param name="trackReferences">Whether to enable track references for this operation.</param>
    /// <param name="declared">Whether to enable declared for this operation.</param>
    /// <param name="limits">The limits supplied to <c>WirePolicy</c>.</param>
    internal WirePolicy(InputPolicy input, StringProfile strings, int? bound, bool trackReferences, bool declared = false,
        InputLimits? limits = null)
    {
        Limits = limits ?? InputLimits.Unbounded;
        Declared = declared;
        TrackReferences = trackReferences;
        Bound = bound;
        Strings = strings;
        Input = input;
    }

    /// <summary>Gets the profile of JSON documents declared hostile: the strict grammar and the untrusted depth bound.</summary>
    /// <remarks>An implicit untrusted default keeps the declared JSON profile; an explicit untrusted declaration or limit selects
    /// strict JSON. Binary reads keep the declared profile.</remarks>
    internal StringProfile JsonInput => Input == InputPolicy.Trusted || !Limits.Explicit ||
        Strings.Strict && Strings.Depth > 0 && Strings.Depth <= Limits.Depth ? Strings :
        new StringProfile(Strings.Format, Strings.Precision, Strings.Depth > 0 && Strings.Depth <= Limits.Depth ? Strings.Depth : Limits.Depth,
            strict: true, Strings.Encoding, Strings.Recording);

    /// <inheritdoc/>
    public bool Equals(WirePolicy? other) => ReferenceEquals(this, other) || other != null && Input == other.Input &&
        Strings.Equals(other.Strings) && Bound == other.Bound && TrackReferences == other.TrackReferences && Declared == other.Declared &&
        Limits.Equals(other.Limits);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as WirePolicy);

    /// <inheritdoc/>
    public override int GetHashCode() => Strings.GetHashCode();
}
