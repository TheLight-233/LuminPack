using System.ComponentModel;

namespace LuminPack.Code;

/// <summary>Selects the ordinary object's null marker and member-count header in generated operations.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public readonly struct LuminPackObjectFraming
{
}

/// <summary>Selects the payload following an already processed Union tag in generated operations.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public readonly struct LuminPackPayloadFraming
{
}
