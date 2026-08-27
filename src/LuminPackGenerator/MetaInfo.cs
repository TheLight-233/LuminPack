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

public sealed class MetaInfo
{
    public CSharpParseOptions? ParseOptions { get; set; }
    
    public LanguageVersion CSharpVersion { get; set; }
    
    public bool IsNet8 { get; set; }
    
    public bool IsNet9_OR_GREATER { get; set; }
    
    public bool IsForUnity { get; set; }
    
    public bool AllowUnsafe { get; set; } 
    
    public LuminPackRegisterMode RegisterMode { get; set; }

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