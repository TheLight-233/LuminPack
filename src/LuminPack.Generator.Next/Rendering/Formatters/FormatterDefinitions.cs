using System;
using System.Collections.Generic;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;
/// <summary>Names the intrinsic formatter contracts and registers closed generic framework families.</summary>
internal static class FormatterDefinitions
{
    internal const string CompilerUnion = "CompilerUnion";
    internal const string Scalar = "$scalar";
    internal const string FrameworkScalar = "$framework-scalar";
    internal const string Array = "$array";
    internal const string Object = "$object";
    internal const string Registered = "$registered";
    internal const string CollectionSubclass = "$collection-subclass";

    /// <summary>A closed construction of a local generic Union, which forwards to its definition's generic Union roots; the
    /// formatter's <c>Underlying</c> holds the definition's root stem and the closed type arguments, separated by a bar.</summary>
    internal const string GenericUnion = "$genericunion";
    internal const string PayloadReference = "$payload";
    private static readonly HashSet<string> Generics = new HashSet<string>(StringComparer.Ordinal)
    {
        GenericIdentity.Nullable,
        GenericIdentity.KeyValuePair,
        GenericIdentity.Lazy,
        GenericIdentity.Tuple,
        GenericIdentity.ValueTuple,
        GenericIdentity.Memory,
        GenericIdentity.ReadOnlyMemory,
        GenericIdentity.ArraySegment,
        GenericIdentity.ReadOnlySequence,
        GenericIdentity.Grouping,
        GenericIdentity.Lookup,
    };
    private static readonly Dictionary<string, ContiguousShape> Contiguous = new Dictionary<string, ContiguousShape>(StringComparer.Ordinal)
    {
        [GenericIdentity.Memory] = ContiguousShape.Memory,
        [GenericIdentity.ReadOnlyMemory] = ContiguousShape.ReadOnlyMemory,
        [GenericIdentity.ArraySegment] = ContiguousShape.ArraySegment,
        [GenericIdentity.ReadOnlySequence] = ContiguousShape.ReadOnlySequence,
    };
    /// <summary>Finds the default storage operations of a contiguous framework family.</summary>
    /// <param name = "definition">The definition used by this operation.</param>
    /// <param name = "shape">The shape used by this operation.</param>
    /// <returns>Whether the requested contract or capability is available.</returns>
    internal static bool TryContiguous(string definition, out ContiguousShape shape) => Contiguous.TryGetValue(definition, out shape);
    /// <summary>Recognizes a framework family independently of its argument types.</summary>
    /// <param name = "definition">The definition used by this operation.</param>
    /// <returns>Whether the requested contract or capability is available.</returns>
    internal static bool HasGeneric(string definition) => Generics.Contains(definition);
}
