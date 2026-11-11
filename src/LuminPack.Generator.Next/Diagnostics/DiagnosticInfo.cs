using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace LuminPack.SourceGenerator.Next.Diagnostics;

/// <summary>A declaration diagnostic captured as value-equal facts during analysis.</summary>
/// <remarks>The pipeline carries the descriptor identity, the formatted arguments and the declaration's source position.
/// Reconstructing the Roslyn diagnostic happens in the reporting boundary, so cached models never retain a symbol, a syntax
/// tree or a compilation.</remarks>
internal readonly struct DiagnosticInfo : IEquatable<DiagnosticInfo>
{
    /// <summary>The diagnostic identifier, such as <c>LuminPack001</c>.</summary>
    internal readonly string Id;

    /// <summary>The message arguments the descriptor formats.</summary>
    internal readonly string[] Arguments;

    /// <summary>The source file the declaration comes from, or null when the diagnostic has no location.</summary>
    internal readonly string? FilePath;

    /// <summary>The declaration's source span start.</summary>
    internal readonly int SpanStart;

    /// <summary>The declaration's source span length.</summary>
    internal readonly int SpanLength;

    /// <summary>The declaration's first line.</summary>
    internal readonly int StartLine;

    /// <summary>The declaration's first character.</summary>
    internal readonly int StartCharacter;

    /// <summary>The declaration's last line.</summary>
    internal readonly int EndLine;

    /// <summary>The declaration's last character.</summary>
    internal readonly int EndCharacter;

    /// <summary>Related source declarations used by the existing code-fix contracts.</summary>
    internal readonly DiagnosticInfo[] AdditionalLocations;

    private DiagnosticInfo(string id, string[] arguments, string? filePath, int spanStart, int spanLength, int startLine,
        int startCharacter, int endLine, int endCharacter, DiagnosticInfo[]? additionalLocations = null)
    {
        AdditionalLocations = additionalLocations ?? Array.Empty<DiagnosticInfo>();
        EndCharacter = endCharacter;
        EndLine = endLine;
        StartCharacter = startCharacter;
        StartLine = startLine;
        SpanLength = spanLength;
        SpanStart = spanStart;
        FilePath = filePath;
        Arguments = arguments;
        Id = id;
    }

    /// <summary>Whether the diagnostic points at a source declaration.</summary>
    internal bool HasLocation => FilePath != null;

    /// <summary>Captures a diagnostic at a source declaration.</summary>
    /// <param name="id">The diagnostic identifier.</param>
    /// <param name="filePath">The declaration's file.</param>
    /// <param name="spanStart">The declaration's span start.</param>
    /// <param name="spanLength">The declaration's span length.</param>
    /// <param name="startLine">The declaration's first line.</param>
    /// <param name="startCharacter">The declaration's first character.</param>
    /// <param name="endLine">The declaration's last line.</param>
    /// <param name="endCharacter">The declaration's last character.</param>
    /// <param name="arguments">The message arguments.</param>
    /// <returns>The captured diagnostic.</returns>
    internal static DiagnosticInfo Located(string id, string filePath, int spanStart, int spanLength, int startLine,
        int startCharacter, int endLine, int endCharacter, params string[] arguments)
        => new DiagnosticInfo(id, arguments, filePath, spanStart, spanLength, startLine, startCharacter, endLine, endCharacter);

    /// <summary>Captures a diagnostic without a declaration location.</summary>
    /// <param name="id">The diagnostic identifier.</param>
    /// <param name="arguments">The message arguments.</param>
    /// <returns>The captured diagnostic.</returns>
    internal static DiagnosticInfo Unlocated(string id, params string[] arguments)
        => new DiagnosticInfo(id, arguments, null, 0, 0, 0, 0, 0, 0);

    /// <summary>Captures source coordinates without retaining a syntax tree or compiler location.</summary>
    /// <param name="id">The stable descriptor identifier.</param>
    /// <param name="location">The original declaration location.</param>
    /// <param name="arguments">The formatted message arguments.</param>
    /// <param name="additional">Related declarations for code fixes.</param>
    /// <returns>The value-equal diagnostic facts.</returns>
    internal static DiagnosticInfo Capture(string id, Location? location, string[] arguments, IEnumerable<Location>? additional = null)
    {
        DiagnosticInfo[] related = additional == null ? Array.Empty<DiagnosticInfo>() : additional.Where(item => item.IsInSource)
            .Select(item => Capture(id, item, Array.Empty<string>())).ToArray();
        if (location?.IsInSource != true)
            return new DiagnosticInfo(id, arguments, null, 0, 0, 0, 0, 0, 0, related);
        FileLinePositionSpan line = location.GetLineSpan();
        return new DiagnosticInfo(id, arguments, line.Path, location.SourceSpan.Start, location.SourceSpan.Length,
            line.StartLinePosition.Line, line.StartLinePosition.Character, line.EndLinePosition.Line, line.EndLinePosition.Character, related);
    }

    /// <inheritdoc/>
    public bool Equals(DiagnosticInfo other)
    {
        if (Id != other.Id || FilePath != other.FilePath || SpanStart != other.SpanStart || SpanLength != other.SpanLength ||
            StartLine != other.StartLine || StartCharacter != other.StartCharacter || EndLine != other.EndLine ||
            EndCharacter != other.EndCharacter)
            return false;
        string[] left = Arguments ?? Array.Empty<string>(), right = other.Arguments ?? Array.Empty<string>();
        if (left.Length != right.Length)
            return false;
        for (int index = 0; index < left.Length; index++)
            if (left[index] != right[index])
                return false;
        DiagnosticInfo[] relatedLeft = AdditionalLocations ?? Array.Empty<DiagnosticInfo>();
        DiagnosticInfo[] relatedRight = other.AdditionalLocations ?? Array.Empty<DiagnosticInfo>();
        return relatedLeft.SequenceEqual(relatedRight);
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is DiagnosticInfo other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        int hash = (Id == null ? 0 : Id.GetHashCode()) ^ (FilePath == null ? 0 : FilePath.GetHashCode()) ^ SpanStart ^
            SpanLength ^ StartLine ^ EndLine;
        if (Arguments != null)
            for (int index = 0; index < Arguments.Length; index++)
                hash ^= Arguments[index] == null ? 0 : Arguments[index].GetHashCode();
        return hash;
    }
}
