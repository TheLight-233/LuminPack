using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace LuminPack.SourceGenerator;

/// <summary>Retains generated text and diagnostic values without retaining semantic objects.</summary>
internal sealed class FrozenTypeEmission : IEquatable<FrozenTypeEmission>
{
    internal readonly string HintName;
    internal readonly string Extension;
    internal readonly string Union;
    internal readonly FrozenLayoutGroup Layouts;
    internal readonly ImmutableArray<FrozenDiagnostic> Diagnostics;

    /// <summary>Captures the completed outputs for one annotated type.</summary>
    /// <param name="hintName">The existing generated hint-name prefix.</param>
    /// <param name="extension">The complete extension source.</param>
    /// <param name="union">The complete union source, if any.</param>
    /// <param name="layouts">Ordered, independently deduplicated layout fragments.</param>
    /// <param name="diagnostics">Diagnostics represented as stable values.</param>
    internal FrozenTypeEmission(string hintName, string extension, string union, FrozenLayoutGroup layouts, IEnumerable<Diagnostic> diagnostics)
    {
        HintName = hintName;
        Extension = extension;
        Union = union;
        Layouts = layouts;
        Diagnostics = diagnostics.Select(FrozenDiagnostic.From).ToImmutableArray();
    }

    /// <summary>Compares the values observed by source production and diagnostic reporting.</summary>
    /// <param name="other">The previous emission plan.</param>
    /// <returns>Whether all observable outputs are equal.</returns>
    public bool Equals(FrozenTypeEmission other) => ReferenceEquals(this, other) || other != null &&
        HintName == other.HintName && Extension == other.Extension && Union == other.Union && Layouts.Equals(other.Layouts) && Diagnostics.SequenceEqual(other.Diagnostics);

    /// <summary>Compares an arbitrary object with this emission plan.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns>Whether the object represents the same outputs.</returns>
    public override bool Equals(object obj) => Equals(obj as FrozenTypeEmission);

    /// <summary>Hashes the stable output identity.</summary>
    /// <returns>A hash consistent with value equality.</returns>
    public override int GetHashCode() => HintName.GetHashCode();
}

/// <summary>Stores one layout declaration with the legacy registry's deduplication key.</summary>
internal sealed class FrozenLayoutFragment : IEquatable<FrozenLayoutFragment>
{
    internal readonly string Key;
    internal readonly string Text;

    /// <summary>Creates a frozen layout contribution.</summary>
    /// <param name="key">The existing registry key.</param>
    /// <param name="text">The declaration including its exact original whitespace.</param>
    internal FrozenLayoutFragment(string key, string text) { Key = key; Text = text; }

    /// <summary>Compares the declaration identity and complete text.</summary>
    /// <param name="other">The contribution to compare.</param>
    /// <returns>Whether the contributions are equal.</returns>
    public bool Equals(FrozenLayoutFragment other) => ReferenceEquals(this, other) || other != null && Key == other.Key && Text == other.Text;

    /// <summary>Compares an arbitrary object with this contribution.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns>Whether the object is an equal contribution.</returns>
    public override bool Equals(object obj) => Equals(obj as FrozenLayoutFragment);

    /// <summary>Hashes the declaration's stable registry key.</summary>
    /// <returns>A hash consistent with value equality.</returns>
    public override int GetHashCode() => Key.GetHashCode();
}

/// <summary>Projects type output down to only the facts needed by the legacy layout aggregate.</summary>
internal sealed class FrozenLayoutGroup : IEquatable<FrozenLayoutGroup>
{
    internal readonly string ClassName;
    internal readonly CSharpParseOptions Options;
    internal readonly ImmutableArray<FrozenLayoutFragment> Fragments;
    internal readonly string Error;

    /// <summary>Creates a pure-value layout projection.</summary>
    /// <param name="className">The existing assembly-specific extension class name.</param>
    /// <param name="options">The immutable documentation parsing options.</param>
    /// <param name="fragments">The ordered contributions.</param>
    /// <param name="error">The legacy aggregate failure message, if generation failed.</param>
    internal FrozenLayoutGroup(string className, CSharpParseOptions options, IEnumerable<FrozenLayoutFragment> fragments, string error)
    { ClassName = className; Options = options; Fragments = fragments.ToImmutableArray(); Error = error; }

    /// <summary>Compares only layout output dependencies.</summary>
    /// <param name="other">The previous projection.</param>
    /// <returns>Whether layout assembly can remain cached.</returns>
    public bool Equals(FrozenLayoutGroup other) => ReferenceEquals(this, other) || other != null &&
        ClassName == other.ClassName && object.Equals(Options, other.Options) && Error == other.Error && Fragments.SequenceEqual(other.Fragments);

    /// <summary>Compares an arbitrary object with this projection.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns>Whether the projections are equal.</returns>
    public override bool Equals(object obj) => Equals(obj as FrozenLayoutGroup);

    /// <summary>Hashes the common generated class identity.</summary>
    /// <returns>A hash consistent with value equality.</returns>
    public override int GetHashCode() => ClassName.GetHashCode();
}

/// <summary>Freezes diagnostics while reconstructing source-tree locations only during reporting.</summary>
internal sealed class FrozenDiagnostic : IEquatable<FrozenDiagnostic>
{
    private readonly DiagnosticDescriptor _descriptor;
    private readonly string _message;
    private readonly FrozenLocation _location;
    private readonly ImmutableArray<FrozenLocation> _additional;
    private readonly ImmutableDictionary<string, string> _properties;
    private readonly string _equality;

    /// <summary>Captures diagnostic metadata and location values.</summary>
    /// <param name="diagnostic">The diagnostic being frozen.</param>
    private FrozenDiagnostic(Diagnostic diagnostic)
    {
        var descriptor = diagnostic.Descriptor;
        _descriptor = new DiagnosticDescriptor(descriptor.Id, descriptor.Title, "{0}", descriptor.Category, diagnostic.Severity,
            descriptor.IsEnabledByDefault, descriptor.Description, descriptor.HelpLinkUri, descriptor.CustomTags.ToArray());
        _message = diagnostic.GetMessage();
        _location = new FrozenLocation(diagnostic.Location);
        _additional = diagnostic.AdditionalLocations.Select(location => new FrozenLocation(location)).ToImmutableArray();
        _properties = diagnostic.Properties;
        _equality = diagnostic.Id + "|" + diagnostic.Severity + "|" + _message + "|" + _location.Identity + "|" +
            string.Join(";", _additional.Select(location => location.Identity)) + "|" + string.Join(";", _properties.OrderBy(pair => pair.Key).Select(pair => pair.Key + "=" + pair.Value));
    }

    /// <summary>Freezes one compiler diagnostic.</summary>
    /// <param name="diagnostic">The diagnostic to freeze.</param>
    /// <returns>The frozen value.</returns>
    internal static FrozenDiagnostic From(Diagnostic diagnostic) => new FrozenDiagnostic(diagnostic);

    /// <summary>Reconstructs source locations against the current compilation.</summary>
    /// <param name="compilation">The compilation currently receiving diagnostics.</param>
    /// <returns>A diagnostic with current source-tree identities.</returns>
    internal Diagnostic Materialize(Compilation compilation) => Diagnostic.Create(_descriptor, _location.Materialize(compilation),
        _additional.Select(location => location.Materialize(compilation)), _properties, _message);

    /// <summary>Reconstructs a value-bound diagnostic using its exact immutable input tree.</summary>
    /// <returns>The diagnostic without introducing a compilation-wide output dependency.</returns>
    internal Diagnostic MaterializeSource() => Diagnostic.Create(_descriptor, _location.MaterializeSource(),
        _additional.Select(location => location.MaterializeSource()), _properties, _message);

    /// <summary>Compares the observable diagnostic value.</summary>
    /// <param name="other">The diagnostic value to compare.</param>
    /// <returns>Whether both diagnostics have equal messages and positions.</returns>
    public bool Equals(FrozenDiagnostic other) => ReferenceEquals(this, other) || other != null && _equality == other._equality;

    /// <summary>Compares an arbitrary object with this diagnostic value.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns>Whether both values are equal.</returns>
    public override bool Equals(object obj) => Equals(obj as FrozenDiagnostic);

    /// <summary>Hashes the complete diagnostic identity.</summary>
    /// <returns>A hash consistent with value equality.</returns>
    public override int GetHashCode() => _equality.GetHashCode();

    /// <summary>Stores a source position and weakly assigned syntax identity without retaining its old tree.</summary>
    private sealed class FrozenLocation
    {
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<SyntaxTree, TreeIdentity> Trees = new System.Runtime.CompilerServices.ConditionalWeakTable<SyntaxTree, TreeIdentity>();
        private static long _nextTreeIdentity;
        private readonly LocationKind _kind;
        private readonly long _treeIdentity;
        private readonly WeakReference<SyntaxTree> _sourceTree;
        private readonly string _path;
        private readonly TextSpan _span;
        private readonly LinePositionSpan _lines;
        internal readonly string Identity;

        /// <summary>Captures a diagnostic position.</summary>
        /// <param name="location">The original position.</param>
        internal FrozenLocation(Location location)
        {
            _kind = location.Kind;
            _treeIdentity = location.SourceTree == null ? 0 : GetTreeIdentity(location.SourceTree);
            _sourceTree = location.SourceTree == null ? null : new WeakReference<SyntaxTree>(location.SourceTree);
            _path = location.GetLineSpan().Path ?? string.Empty;
            _span = location.SourceSpan;
            _lines = location.GetLineSpan().Span;
            Identity = _kind + ":" + _treeIdentity + ":" + _path + ":" + _span + ":" + _lines;
        }

        /// <summary>Gets a unique process-local identity while allowing the syntax tree to be collected.</summary>
        /// <param name="tree">The immutable input tree.</param>
        /// <returns>A stable identity for that exact tree instance.</returns>
        private static long GetTreeIdentity(SyntaxTree tree) => Trees.GetValue(tree, _ => new TreeIdentity(System.Threading.Interlocked.Increment(ref _nextTreeIdentity))).Value;

        /// <summary>Rebinds a source position to its exact current tree, including empty or duplicate paths.</summary>
        /// <param name="compilation">The current compilation.</param>
        /// <returns>The current source location or the original external position.</returns>
        internal Location Materialize(Compilation compilation)
        {
            if (_kind == LocationKind.None) return Location.None;
            if (_kind == LocationKind.SourceFile)
            {
                var tree = compilation.SyntaxTrees.FirstOrDefault(candidate => GetTreeIdentity(candidate) == _treeIdentity);
                if (tree != null) return Location.Create(tree, _span);
            }
            return Location.Create(_path, _span, _lines);
        }

        /// <summary>Resolves the exact tree already validated by the type's dependency boundary.</summary>
        /// <returns>The original source position, or its external representation if the tree is no longer alive.</returns>
        internal Location MaterializeSource()
        {
            if (_kind == LocationKind.None) return Location.None;
            if (_kind == LocationKind.SourceFile && _sourceTree != null && _sourceTree.TryGetTarget(out var tree))
                return Location.Create(tree, _span);
            return Location.Create(_path, _span, _lines);
        }

        /// <summary>Stores only the identity value in the weak table.</summary>
        private sealed class TreeIdentity
        {
            internal readonly long Value;

            /// <summary>Captures one exact syntax-tree identity.</summary>
            /// <param name="value">The unique allocated identity.</param>
            internal TreeIdentity(long value) => Value = value;
        }
    }
}

