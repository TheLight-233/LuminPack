using Microsoft.CodeAnalysis.CSharp;

namespace LuminPack.SourceGenerator;

#nullable enable
public sealed class MetaInfo
{
    public CSharpParseOptions? ParseOptions { get; set; }
    
    public LanguageVersion CSharpVersion { get; set; }
    
    public bool IsNet8 { get; set; }
    
    public bool IsForUnity { get; set; }

    public MetaInfo(CSharpParseOptions options, LanguageVersion version, bool isNet8)
    {
        ParseOptions = options;
        CSharpVersion = version;
        IsNet8 = isNet8;
    }
}