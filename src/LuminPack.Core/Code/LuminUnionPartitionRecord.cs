using System.ComponentModel;

namespace LuminPack.Code;

/// <summary>Holds one variable record's reservation and actual width in generated partition scratch.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public struct LuminUnionPartitionRecord
{
    /// <summary>The absolute reserved output offset.</summary>
    public int Offset;
    /// <summary>The complete record upper bound, including its narrow tag.</summary>
    public int Bound;
    /// <summary>The actual complete written width.</summary>
    public int Actual;
}
