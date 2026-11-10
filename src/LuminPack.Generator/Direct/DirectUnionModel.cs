using System;

namespace LuminPack.SourceGenerator.Direct;

/// <summary>One declared case of a covered Union root, reduced to the facts its dispatch text depends on.</summary>
internal sealed class DirectUnionCase : IEquatable<DirectUnionCase>
{
    /// <summary>The narrow Union tag.</summary>
    internal readonly int Id;
    /// <summary>The fully qualified case type.</summary>
    internal readonly string Type;
    /// <summary>Whether the case is a local reference type with a variable payload, whose binary read entry the switch calls
    /// instead of expanding its members.</summary>
    internal readonly bool OwnEntry;
    /// <summary>Whether the case's binary read entry takes the operation's profile.</summary>
    internal readonly bool ReadGeneric;
    /// <summary>Whether this case's generated slot can be sealed, since no other case derives from it.</summary>
    internal readonly bool Sealed;
    /// <summary>Whether the case's complete fixed payload writes through the shared returning cursor kernel.</summary>
    internal readonly bool FixedCursor;
    /// <summary>The direct model whose fixed member reads this case's switch arm expands, or null when the arm calls the
    /// case's own entry.</summary>
    internal readonly DirectTypeModel Model;
    /// <summary>The readable payload name of the case, which names its own Union entry.</summary>
    internal readonly string Readable;
    /// <summary>The partial declarations from the outermost containing type to the case.</summary>
    internal readonly string[] Chain;
    /// <summary>The case's namespace, or null in the global namespace.</summary>
    internal readonly string Namespace;

    /// <summary>Captures one case.</summary>
    internal DirectUnionCase(int id, string type, bool ownEntry, bool readGeneric, bool sealedCase, bool fixedCursor, DirectTypeModel model,
        string readable, string[] chain, string @namespace)
    {
        Namespace = @namespace;
        Chain = chain;
        Readable = readable;
        Model = model;
        FixedCursor = fixedCursor;
        Sealed = sealedCase;
        ReadGeneric = readGeneric;
        OwnEntry = ownEntry;
        Type = type;
        Id = id;
    }

    /// <inheritdoc/>
    public bool Equals(DirectUnionCase other) => other != null && Id == other.Id && Type == other.Type && OwnEntry == other.OwnEntry &&
        ReadGeneric == other.ReadGeneric && Sealed == other.Sealed && FixedCursor == other.FixedCursor && Namespace == other.Namespace &&
        Readable == other.Readable && Same(Chain, other.Chain) && Equals(Model, other.Model);

    private static bool Same(string[] left, string[] right) => left == null ? right == null : right != null && System.Linq.Enumerable.SequenceEqual(left, right);

    /// <inheritdoc/>
    public override bool Equals(object obj) => Equals(obj as DirectUnionCase);

    /// <inheritdoc/>
    public override int GetHashCode() => Id;
}

/// <summary>One compiled wire-profile slot of a covered Union root.</summary>
internal sealed class DirectUnionSlot : IEquatable<DirectUnionSlot>
{
    /// <summary>The method-name suffix: empty for the declared profile, otherwise <c>_Options_&lt;identity&gt;</c>.</summary>
    internal readonly string Suffix;
    /// <summary>The imported name of the profile's string options type.</summary>
    internal readonly string Profile;
    /// <summary>The complete compiled-profile identity.</summary>
    internal readonly string Identity;
    /// <summary>The profile's string encoding, zero for UTF-8 or one for UTF-16.</summary>
    internal readonly int Encoding;
    /// <summary>The profile's string recording, zero for lengths, one for tokens or two for variable lengths.</summary>
    internal readonly int Recording;

    /// <summary>Captures one slot.</summary>
    internal DirectUnionSlot(string suffix, string profile, string identity, int encoding, int recording)
    {
        Recording = recording;
        Encoding = encoding;
        Identity = identity;
        Profile = profile;
        Suffix = suffix;
    }

    /// <inheritdoc/>
    public bool Equals(DirectUnionSlot other) => other != null && Suffix == other.Suffix && Profile == other.Profile &&
        Identity == other.Identity && Encoding == other.Encoding && Recording == other.Recording;

    /// <inheritdoc/>
    public override bool Equals(object obj) => Equals(obj as DirectUnionSlot);

    /// <inheritdoc/>
    public override int GetHashCode() => Suffix.GetHashCode();
}
