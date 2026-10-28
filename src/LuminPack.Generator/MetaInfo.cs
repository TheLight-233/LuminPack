using Microsoft.CodeAnalysis.CSharp;

namespace LuminPack.SourceGenerator;

#nullable enable

/// <summary>Controls whether the manual-registration (Register) code is generated.</summary>
public enum LuminPackRegisterMode
{
    /// <summary>Follow the project's unsafe setting.</summary>
    Auto = 0,

    /// <summary>Never generate Register code, even when AllowUnsafeBlocks is enabled.</summary>
    Disabled = 1,
}

/// <summary>Describes the consuming compilation's language, target framework, and generation settings.</summary>
public sealed class MetaInfo
{
    /// <summary>Gets or sets the parse options.</summary>
    public CSharpParseOptions? ParseOptions { get; set; }

    /// <summary>Gets or sets the c sharp version.</summary>
    public LanguageVersion CSharpVersion { get; set; }

    /// <summary>Gets or sets the is net8.</summary>
    public bool IsNet8 { get; set; }

    /// <summary>Gets or sets the is net9 or greater.</summary>
    public bool IsNet9_OR_GREATER { get; set; }

    /// <summary>Gets or sets the is for unity.</summary>
    public bool IsForUnity { get; set; }

    /// <summary>Gets or sets the allow unsafe.</summary>
    public bool AllowUnsafe { get; set; }

    /// <summary>Gets or sets the register mode.</summary>
    public LuminPackRegisterMode RegisterMode { get; set; }

    /// <summary>Initializes a new instance of <c>MetaInfo</c>.</summary>
    /// <param name="options">The configuration to apply to this operation.</param>
    /// <param name="version">The version used by this operation.</param>
    /// <param name="isNet8">The is net8 used by this operation.</param>
    /// <param name="isNet9OrGreater">The is net9 or greater used by this operation.</param>
    /// <param name="allowUnsafe">The allow unsafe used by this operation.</param>
    /// <param name="registerMode">The register mode used by this operation.</param>
    public MetaInfo(CSharpParseOptions options, LanguageVersion version, bool isNet8, bool isNet9OrGreater, bool allowUnsafe, LuminPackRegisterMode registerMode = LuminPackRegisterMode.Auto)
    {
        ParseOptions = options;
        CSharpVersion = version;
        IsNet8 = isNet8;
        IsNet9_OR_GREATER = isNet9OrGreater;
        AllowUnsafe = allowUnsafe;
        RegisterMode = registerMode;
    }

}
