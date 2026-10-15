namespace LuminPack.SourceGenerator.Mapping;

// ─────────────────────────────────────────────────────────────────────────────
//  Binding models shared by both mapping paths.
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Describes the source binding for one destination member.</summary>
public sealed class LuminMapFieldBinding
{
    /// <summary>Gets or sets the dest member name.</summary>
    public string DestMemberName { get; set; } = "";
    /// <summary>Gets or sets the dest is property.</summary>
    public bool DestIsProperty { get; set; }
    /// <summary>
    /// The source expression inserted directly into generated assignments.
    /// Example:"source.Name" / "global::NS.Conv.Parse(source.Raw)" / "true"
    /// </summary>
    public string SourceExpression { get; set; } = "";
    /// <summary>Gets or sets the is ignored.</summary>
    public bool IsIgnored { get; set; }
    /// <summary>When present, guards the destination assignment with the configured condition.</summary>
    public string? ConditionExpression { get; set; }
    /// <summary>Gets or sets the source member found.</summary>
    public bool SourceMemberFound { get; set; }
}
