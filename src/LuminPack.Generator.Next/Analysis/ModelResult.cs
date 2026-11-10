using System;
using LuminPack.SourceGenerator.Next.Model;

namespace LuminPack.SourceGenerator.Next.Analysis;

/// <summary>The outcome of modeling one declared payload type.</summary>
internal sealed class ModelResult : IEquatable<ModelResult>
{
    /// <summary>The model when one was produced, otherwise null.</summary>
    internal readonly TypeModel? Model;

    /// <summary>Whether the model covers every generated member, so its renderer may run.</summary>
    internal readonly bool IsComplete;

    /// <summary>The reason a shape is unsupported or still incomplete, or null when the model is complete.</summary>
    internal readonly string? Reason;

    private ModelResult(TypeModel? model, bool complete, string? reason)
    {
        Reason = reason;
        IsComplete = complete;
        Model = model;
    }

    /// <summary>A complete model.</summary>
    internal static ModelResult Ready(TypeModel model) => new ModelResult(model, complete: true, reason: null);

    /// <summary>A model whose analysis is scaffolded but not yet complete, which emits nothing.</summary>
    internal static ModelResult Incomplete(TypeModel model, string reason) => new ModelResult(model, complete: false, reason: reason);

    /// <summary>No model, because the declaration is outside the supported surface.</summary>
    internal static ModelResult Unsupported(string reason) => new ModelResult(null, complete: false, reason: reason);

    /// <inheritdoc/>
    public bool Equals(ModelResult? other) => ReferenceEquals(this, other) || other != null && IsComplete == other.IsComplete &&
        Reason == other.Reason && Equals(Model, other.Model);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as ModelResult);

    /// <inheritdoc/>
    public override int GetHashCode() => Model?.GetHashCode() ?? 0;
}
