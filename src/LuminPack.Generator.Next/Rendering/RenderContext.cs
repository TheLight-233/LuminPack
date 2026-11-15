using LuminPack.SourceGenerator.Next.Model;
using LuminPack.SourceGenerator.Next.Rendering.Emitters;

namespace LuminPack.SourceGenerator.Next.Rendering;

/// <summary>The immutable facts one rendered file depends on.</summary>
internal sealed class RenderContext
{
    /// <summary>The project facts.</summary>
    internal readonly GeneratorOptions Options;

    /// <summary>The alias resolution for this file.</summary>
    internal readonly AliasTable Aliases;

    /// <summary>The operations to emit.</summary>
    internal readonly GeneratedOperations Operations;

    /// <summary>Captures a render context.</summary>
    /// <param name="options">The options governing the operation.</param>
    /// <param name="aliases">The aliases supplied to <c>RenderContext</c>.</param>
    /// <param name="operations">The serialization operations selected for emission.</param>
    internal RenderContext(GeneratorOptions options, AliasTable aliases, GeneratedOperations operations = GeneratedOperations.All)
    {
        Operations = operations;
        Aliases = aliases;
        Options = options;
    }
}
