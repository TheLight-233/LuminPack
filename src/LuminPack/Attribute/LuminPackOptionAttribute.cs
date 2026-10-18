using LuminPack.Option;

namespace LuminPack.Attribute;

/// <summary>Declares serialization options that are compiled into generated operations.</summary>
/// <remarks>
/// Unspecified properties inherit the enclosing declaration's options. Assembly options override the library defaults;
/// type and member declarations override the corresponding enclosing scope. Binary string settings can vary by member.
/// JSON encoding, depth and syntax policy describe a complete document and are selected at its root.
/// </remarks>
[AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Class | AttributeTargets.Struct |
    AttributeTargets.Interface | AttributeTargets.Field | AttributeTargets.Property, Inherited = false)]
public sealed class LuminPackOptionAttribute : System.Attribute
{
    /// <summary>Gets or sets the binary string encoding, or the JSON document encoding at its root.</summary>
    public LuminPackStringEncoding StringEncoding { get; set; } = LuminPackStringEncoding.UTF8;

    /// <summary>Gets or sets whether binary strings have length prefixes or encoding-specific terminators.</summary>
    public LuminPackStringRecording StringRecording { get; set; } = LuminPackStringRecording.Length;

    /// <summary>Gets or sets the profile's standard-format constant, available to explicit formatted and Custom kernels.</summary>
    public string StandardFormat { get; set; } = "G";

    /// <summary>Gets or sets the maximum JSON nesting depth; nonpositive values disable the limit.</summary>
    public int MaxJsonDepth { get; set; } = 1024;

    /// <summary>Gets or sets whether the JSON document uses the strict grammar validator.</summary>
    public bool ValidateJsonSyntax { get; set; }
}
