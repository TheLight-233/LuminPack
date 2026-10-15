using System.Collections.Generic;

namespace LuminPack.SourceGenerator.Mapping;

// ─────────────────────────────────────────────────────────────────────────────
//  Metadata for LuminMapTo automatic mapping.
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// The analyzed result of one LuminMapTo destination declaration.
/// Contains all member bindings from the source type to its destination.
/// </summary>
public sealed class AutoMapPairInfo
{
    /// <summary>Gets or sets the source type full name.</summary>
    public string SourceTypeFullName { get; set; } = "";
    /// <summary>Gets or sets the dest type full name.</summary>
    public string DestTypeFullName { get; set; } = "";
    /// <summary>Gets or sets the dest is value type.</summary>
    public bool DestIsValueType { get; set; }
    /// <summary>Gets or sets the dest has default ctor.</summary>
    public bool DestHasDefaultCtor { get; set; }
    /// <summary>
    /// The inferred or explicitly configured method suffix.
    /// Generated names: To{Suffix}, MapInto{Suffix}, To{Suffix}Array, and To{Suffix}List.
    /// </summary>
    public string MethodSuffix { get; set; } = "";
    /// <summary>Gets or sets the generate collection methods.</summary>
    public bool GenerateCollectionMethods { get; set; } = true;
    /// <summary>Gets the bindings.</summary>
    public List<LuminMapFieldBinding> Bindings { get; } = new();
}
