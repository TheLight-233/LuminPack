using LuminPack.Attribute;

namespace LuminPack.Fuzzing;

/// <summary>Exercises checked scalar batches, strings, bulk arrays, containers and recursive reads.</summary>
[LuminPackable]
[LuminPackOption(MaxInputBytes = 16384, MaxCollectionLength = 128, MaxStringLength = 512, MaxDepth = 16,
    ValidateJsonSyntax = true, MaxJsonDepth = 16)]
public partial class FuzzRecord
{
    public int Number;
    public bool Flag;
    public long Stamp;
    public int? Optional;
    public string? Text = "";
    public int[]? Numbers = [];
    public bool[]? Bits = [];
    public List<bool>? Flags = [];
    public List<FuzzChild>? Items = [];
    public Dictionary<int, string?>? Map = new();
    public FuzzChild? Child;
}

/// <summary>Declares the UTF-8 VarInt contract independently of operation profile overrides.</summary>
[LuminPackable]
[LuminPackOption(MaxInputBytes = 16384, MaxCollectionLength = 128, MaxStringLength = 512, MaxDepth = 16,
    ValidateJsonSyntax = true, MaxJsonDepth = 16, StringEncoding = LuminPack.Option.LuminPackStringEncoding.UTF8,
    StringRecording = LuminPack.Option.LuminPackStringRecording.VarInt)]
public partial class FuzzVarIntRecord : FuzzRecord;

/// <summary>Declares a strict UTF-8 JSON document independently of operation profile overrides.</summary>
[LuminPackable]
[LuminPackOption(MaxInputBytes = 16384, MaxCollectionLength = 128, MaxStringLength = 512, MaxDepth = 16,
    ValidateJsonSyntax = true, MaxJsonDepth = 16, StringEncoding = LuminPack.Option.LuminPackStringEncoding.UTF8)]
public partial class FuzzUtf8Record : FuzzRecord;

/// <summary>Provides nested strings, checked Boolean storage and a bounded recursive edge.</summary>
[LuminPackable]
public partial class FuzzChild
{
    public int Number;
    public bool Flag;
    public string? Text;
    public FuzzChild? Next;
}
