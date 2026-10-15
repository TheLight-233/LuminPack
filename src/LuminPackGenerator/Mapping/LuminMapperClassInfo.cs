using System.Collections.Generic;

namespace LuminPack.SourceGenerator.Mapping;

/// <summary>Provides lumin mapper class info functionality.</summary>
public sealed class LuminMapperClassInfo
{
    /// <summary>Gets or sets the class name.</summary>
    public string ClassName { get; set; } = "";
    /// <summary>Gets or sets the class namespace.</summary>
    public string ClassNamespace { get; set; } = "";
    /// <summary>Gets or sets the class full name.</summary>
    public string ClassFullName { get; set; } = "";
    /// <summary>Gets or sets the is global namespace.</summary>
    public bool IsGlobalNamespace { get; set; }
    /// <summary>Gets or sets the is unity project.</summary>
    public bool IsUnityProject { get; set; }
    /// <summary>Gets the methods.</summary>
    public List<LuminMapMethodInfo> Methods { get; } = new();
}
