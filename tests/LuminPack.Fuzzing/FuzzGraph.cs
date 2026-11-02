using LuminPack.Attribute;

namespace LuminPack.Fuzzing;

/// <summary>Combines version-tolerant, reference-tracked and Union members under one bounded root.</summary>
/// <remarks>Untrusted reads are the default; the root's limits apply to every nested value.</remarks>
[LuminPackable]
[LuminPackOption(MaxInputBytes = 16384, MaxCollectionLength = 128, MaxStringLength = 512, MaxDepth = 16)]
public partial class FuzzGraph
{
    public FuzzVersioned? Versioned;
    public FuzzNode? Nodes;
    public List<FuzzShape?>? Shapes = [];
}

/// <summary>Exercises version-tolerant prefixes, member widths, strings, arrays and a recursive member.</summary>
[LuminPackable(GeneratorType.VersionTolerant)]
public partial class FuzzVersioned
{
    public int Id;
    public bool Flag;
    public string? Name;
    public int[]? Values;
    public FuzzVersioned? Next;
}

/// <summary>Exercises reference-tracked slot tables, back references and cycles.</summary>
[LuminPackable(GeneratorType.CircleReference)]
public partial class FuzzNode
{
    [LuminPackOrder(0)] public int Id;
    [LuminPackOrder(1)] public bool Flag;
    [LuminPackOrder(2)] public string? Text;
    [LuminPackOrder(3)] public FuzzNode? Next;
    [LuminPackOrder(4)] public FuzzNode? Other;
}

/// <summary>Exercises Union tags, case payloads and a recursive case.</summary>
[LuminPackable]
[LuminPackUnion(0, typeof(FuzzCircle))]
[LuminPackUnion(1, typeof(FuzzSquare))]
public abstract partial class FuzzShape;

/// <summary>Provides a fixed Union case with Boolean storage.</summary>
[LuminPackable]
public sealed partial class FuzzCircle : FuzzShape
{
    public bool Filled;
    public double Radius;
}

/// <summary>Provides a variable Union case with a nested Union member.</summary>
[LuminPackable]
public sealed partial class FuzzSquare : FuzzShape
{
    public int Side;
    public string? Label;
    public FuzzShape? Inner;
}
