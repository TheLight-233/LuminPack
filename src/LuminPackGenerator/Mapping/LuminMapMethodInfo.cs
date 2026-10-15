using System.Collections.Generic;

namespace LuminPack.SourceGenerator.Mapping;

/// <summary>Provides lumin map method info functionality.</summary>
public sealed class LuminMapMethodInfo
{
    /// <summary>Gets or sets the method name.</summary>
    public string MethodName { get; set; } = "";
    /// <summary>Gets or sets the source type full name.</summary>
    public string SourceTypeFullName { get; set; } = "";
    /// <summary>Gets or sets the dest type full name.</summary>
    public string DestTypeFullName { get; set; } = "";
    /// <summary>Gets or sets the source is in.</summary>
    public bool SourceIsIn { get; set; }
    /// <summary>Gets or sets the is create new.</summary>
    public bool IsCreateNew { get; set; }
    /// <summary>Gets or sets the dest is value type.</summary>
    public bool DestIsValueType { get; set; }
    /// <summary>Gets or sets the dest has default ctor.</summary>
    public bool DestHasDefaultCtor { get; set; }
    /// <summary>Gets the bindings.</summary>
    public List<LuminMapFieldBinding> Bindings { get; } = new();
    /// <summary>Gets or sets the collection kind.</summary>
    public CollectionMapKind CollectionKind { get; set; } = CollectionMapKind.None;
    /// <summary>Gets or sets the element method.</summary>
    public LuminMapMethodInfo? ElementMethod { get; set; }
}
