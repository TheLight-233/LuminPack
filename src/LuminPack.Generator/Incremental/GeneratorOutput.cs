using System;

namespace LuminPack.SourceGenerator;

/// <summary>Identifies the source groups owned by the registered generators.</summary>
[Flags]
internal enum GeneratorOutput
{
    /// <summary>The binary serializer facade and shared assembly support.</summary>
    Binary = 1,

    /// <summary>The JSON serializer facade.</summary>
    Json = 2,

    /// <summary>Formatter implementations, dispatch, and storage declarations for both formats.</summary>
    Formatter = 4,

    /// <summary>All groups published by a complete driver.</summary>
    All = Binary | Json | Formatter
}
