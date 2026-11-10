using System;
using LuminPack.SourceGenerator.Next.Model;

namespace LuminPack.SourceGenerator.Next.Rendering;

/// <summary>The operations the consumer calls, which select the emitted entries.</summary>
[Flags]
internal enum OperationSet
{
    /// <summary>No operation is referenced.</summary>
    None = 0,

    /// <summary>Binary write roots.</summary>
    BinaryWrite = 1,

    /// <summary>Binary read roots.</summary>
    BinaryRead = 2,

    /// <summary>JSON write roots.</summary>
    JsonWrite = 4,

    /// <summary>JSON read roots.</summary>
    JsonRead = 8,

    /// <summary>Every operation.</summary>
    All = BinaryWrite | BinaryRead | JsonWrite | JsonRead,
}

/// <summary>The immutable facts one rendered file depends on.</summary>
internal sealed class RenderContext
{
    /// <summary>The project facts.</summary>
    internal readonly GeneratorOptions Options;

    /// <summary>The alias resolution for this file.</summary>
    internal readonly AliasTable Aliases;

    /// <summary>The operations to emit.</summary>
    internal readonly OperationSet Operations;

    /// <summary>Captures a render context.</summary>
    internal RenderContext(GeneratorOptions options, AliasTable aliases, OperationSet operations = OperationSet.All)
    {
        Operations = operations;
        Aliases = aliases;
        Options = options;
    }
}
