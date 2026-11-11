using System;
using System.Globalization;

namespace LuminPack.SourceGenerator.Next.Model;

/// <summary>The external-input constants a declaration freezes into its generated reads.</summary>
/// <remarks>Only the limits enforced by generated checks are modeled. The remaining runtime budgets, such as decompressed bytes,
/// allocation bytes, object counts and read work, are enforced by the runtime and select no distinct generated code.</remarks>
internal sealed class InputLimits : IEquatable<InputLimits>
{
    /// <summary>The policy of a declaration that states no limit: every check is unbounded and folds away.</summary>
    internal static readonly InputLimits Unbounded = new InputLimits(int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue, explicitInput: false);

    /// <summary>The encoded input cap, which also bounds facade-owned staging.</summary>
    internal readonly int Input;

    /// <summary>The largest accepted collection count.</summary>
    internal readonly int Collection;

    /// <summary>The largest accepted string length.</summary>
    internal readonly int String;

    /// <summary>The largest accepted nesting depth of a recursive graph.</summary>
    internal readonly int Depth;

    /// <summary>Whether a declaration explicitly selects hostile-input handling.</summary>
    /// <remarks>Only an explicit declaration switches JSON reads to the strict grammar and rejects input-selected shapes.</remarks>
    internal readonly bool Explicit;

    /// <summary>Captures one immutable limit set.</summary>
    internal InputLimits(int input, int collection, int @string, int depth, bool explicitInput)
    {
        Input = input;
        Collection = collection;
        String = @string;
        Depth = depth;
        Explicit = explicitInput;
    }

    /// <summary>Whether a collection, string or depth limit needs read-site checks.</summary>
    internal bool Bounded => Collection != int.MaxValue || String != int.MaxValue || Depth != int.MaxValue;

    /// <summary>The stable identity of the generated checks, used to name limit-specific helpers.</summary>
    internal string Identity => Input.ToString(CultureInfo.InvariantCulture) + "_" + Collection.ToString(CultureInfo.InvariantCulture) + "_" +
        String.ToString(CultureInfo.InvariantCulture) + "_" + Depth.ToString(CultureInfo.InvariantCulture) + (Explicit ? "_x" : string.Empty);

    /// <summary>Copies the set with one changed constant.</summary>
    internal InputLimits With(int? input = null, int? collection = null, int? @string = null, int? depth = null, bool? explicitInput = null)
        => new InputLimits(input ?? Input, collection ?? Collection, @string ?? String, depth ?? Depth, explicitInput ?? Explicit);

    /// <inheritdoc/>
    public bool Equals(InputLimits? other) => ReferenceEquals(this, other) || other != null && Input == other.Input &&
        Collection == other.Collection && String == other.String && Depth == other.Depth && Explicit == other.Explicit;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as InputLimits);

    /// <inheritdoc/>
    public override int GetHashCode() => Input ^ (Collection << 7) ^ (String << 13) ^ (Depth << 19) ^ (Explicit ? 1 : 0);
}
