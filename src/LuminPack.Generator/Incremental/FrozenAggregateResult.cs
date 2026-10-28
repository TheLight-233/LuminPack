using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace LuminPack.SourceGenerator;

/// <summary>Collects aggregate generation within the semantic transform before freezing its outputs.</summary>
internal sealed class AggregateResultBuilder
{
    private readonly List<FrozenGeneratedSource> _files = new List<FrozenGeneratedSource>();
    private readonly List<FrozenDiagnostic> _diagnostics = new List<FrozenDiagnostic>();

    /// <summary>Records an unchanged legacy filename and complete source text.</summary>
    /// <param name="name">The existing generated hint name.</param>
    /// <param name="source">The complete generated source.</param>
    internal void AddSource(string name, string source) => _files.Add(new FrozenGeneratedSource(name, source));

    /// <summary>Freezes a diagnostic while current semantic locations are available.</summary>
    /// <param name="diagnostic">The diagnostic to record.</param>
    internal void ReportDiagnostic(Diagnostic diagnostic) => _diagnostics.Add(FrozenDiagnostic.From(diagnostic));

    /// <summary>Records an already frozen current-dependency diagnostic without reconstructing old semantic objects.</summary>
    /// <param name="diagnostic">The immutable diagnostic contribution.</param>
    internal void AddDiagnostic(FrozenDiagnostic diagnostic) => _diagnostics.Add(diagnostic);

    /// <summary>Completes the immutable aggregate projection.</summary>
    /// <returns>The complete source and diagnostic values.</returns>
    internal FrozenAggregateResult Freeze() => new FrozenAggregateResult(_files.ToImmutableArray(), _diagnostics.ToImmutableArray());
}

/// <summary>Projects aggregate work into individually comparable generated files and diagnostic values.</summary>
internal sealed class FrozenAggregateResult : IEquatable<FrozenAggregateResult>
{
    internal readonly ImmutableArray<FrozenGeneratedSource> Files;
    internal readonly ImmutableArray<FrozenDiagnostic> Diagnostics;

    /// <summary>Captures source and diagnostic values in legacy emission order.</summary>
    /// <param name="files">The complete generated files.</param>
    /// <param name="diagnostics">The diagnostics to publish separately.</param>
    internal FrozenAggregateResult(ImmutableArray<FrozenGeneratedSource> files, ImmutableArray<FrozenDiagnostic> diagnostics)
    { Files = files; Diagnostics = diagnostics; }

    /// <summary>Compares every observable aggregate value.</summary>
    /// <param name="other">The previous output batch.</param>
    /// <returns>Whether output projection remains cached.</returns>
    public bool Equals(FrozenAggregateResult other) => ReferenceEquals(this, other) || other != null &&
        Files.SequenceEqual(other.Files) && Diagnostics.SequenceEqual(other.Diagnostics);

    /// <summary>Compares an arbitrary object with this batch.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns>Whether both batches are equal.</returns>
    public override bool Equals(object obj) => Equals(obj as FrozenAggregateResult);

    /// <summary>Hashes the batch's stable filenames.</summary>
    /// <returns>A hash consistent with value equality.</returns>
    public override int GetHashCode() => Files.Length;
}

/// <summary>Stores a generated file without semantic dependencies.</summary>
internal sealed class FrozenGeneratedSource : IEquatable<FrozenGeneratedSource>
{
    internal readonly string Name;
    internal readonly string Text;

    /// <summary>Creates an individual source-output value.</summary>
    /// <param name="name">The existing generated hint name.</param>
    /// <param name="text">The exact complete source text.</param>
    internal FrozenGeneratedSource(string name, string text) { Name = name; Text = text; }

    /// <summary>Compares the filename and complete source.</summary>
    /// <param name="other">The previous file value.</param>
    /// <returns>Whether source production can remain cached.</returns>
    public bool Equals(FrozenGeneratedSource other) => ReferenceEquals(this, other) || other != null && Name == other.Name && Text == other.Text;

    /// <summary>Compares an arbitrary object with this source.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns>Whether both values represent the same file.</returns>
    public override bool Equals(object obj) => Equals(obj as FrozenGeneratedSource);

    /// <summary>Hashes the stable hint name.</summary>
    /// <returns>A hash consistent with value equality.</returns>
    public override int GetHashCode() => Name.GetHashCode();
}
