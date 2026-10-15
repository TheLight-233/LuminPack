using System.Collections.Generic;

namespace LuminPack.SourceGenerator.Mapping;

/// <summary>
/// Aggregates every LuminMapTo declaration on one source type,
/// The generator emits one {SourceName}Mapper.g.cs file per source type.
/// </summary>
public sealed class AutoMapSourceInfo
{
    /// <summary>Gets or sets the source class name.</summary>
    public string SourceClassName { get; set; } = "";
    /// <summary>Gets or sets the source namespace.</summary>
    public string SourceNamespace { get; set; } = "";
    /// <summary>Gets or sets the source full name.</summary>
    public string SourceFullName { get; set; } = "";
    /// <summary>Gets or sets the is global namespace.</summary>
    public bool IsGlobalNamespace { get; set; }
    /// <summary>Gets or sets the is unity project.</summary>
    public bool IsUnityProject { get; set; }
    /// <summary>Gets the pairs.</summary>
    public List<AutoMapPairInfo> Pairs { get; } = new();
}
