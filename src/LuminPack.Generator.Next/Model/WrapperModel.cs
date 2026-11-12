using System;
using System.Linq;

namespace LuminPack.SourceGenerator.Next.Model;

/// <summary>A closed copy of a source generic method whose body reaches the serializer facade.</summary>
/// <remarks>A closed call of such a method, such as <c>Read&lt;double&gt;(bytes)</c> over
/// <c>Deserialize&lt;Box&lt;T&gt;&gt;(bytes)</c>, is redirected to this copy, whose facade calls then bind concrete entries as the
/// original generator's wrappers did. The body spells each facade call as a placeholder, <c>__LuminCall_N</c>, that the interceptor
/// renderer replaces with the call's entry shim, or with <see cref="Fallbacks"/> when no entry binds.</remarks>
internal sealed class WrapperModel : IEquatable<WrapperModel>
{
    /// <summary>The generated method name.</summary>
    internal readonly string Name;

    /// <summary>The declaration line without attributes, such as <c>internal static T Name(int value)</c>.</summary>
    internal readonly string Declaration;

    /// <summary>The method body block with facade calls spelled as placeholders.</summary>
    internal readonly string Body;

    /// <summary>The interception or IL mapping attributes of the source calls this copy replaces, in ordinal order.</summary>
    internal readonly string[] Locations;

    /// <summary>The closed facade calls of the body, indexed by their placeholder number.</summary>
    internal readonly SerializerCallModel[] Calls;

    /// <summary>The closed facade spelling of each placeholder, used when its call has no concrete entry.</summary>
    internal readonly string[] Fallbacks;

    /// <summary>Captures a wrapper.</summary>
    internal WrapperModel(string name, string declaration, string body, string[] locations, SerializerCallModel[] calls, string[] fallbacks)
    {
        Name = name;
        Declaration = declaration;
        Body = body;
        Locations = locations;
        Calls = calls;
        Fallbacks = fallbacks;
    }

    /// <summary>The placeholder of one body call.</summary>
    /// <param name="index">The call index.</param>
    /// <returns>The placeholder identifier.</returns>
    internal static string Placeholder(int index) => "__LuminCall_" + index.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <inheritdoc/>
    public bool Equals(WrapperModel? other) => ReferenceEquals(this, other) || other != null && Name == other.Name &&
        Declaration == other.Declaration && Body == other.Body && Locations.SequenceEqual(other.Locations) && Calls.SequenceEqual(other.Calls) &&
        Fallbacks.SequenceEqual(other.Fallbacks);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as WrapperModel);

    /// <inheritdoc/>
    public override int GetHashCode() => Name.GetHashCode();
}

/// <summary>The value-equal set of wrappers a compilation publishes.</summary>
internal sealed class WrapperSet : IEquatable<WrapperSet>
{
    /// <summary>The set without wrappers.</summary>
    internal static readonly WrapperSet Empty = new(Array.Empty<WrapperModel>());

    /// <summary>The wrappers in ordinal name order.</summary>
    internal readonly WrapperModel[] Wrappers;

    /// <summary>Captures the wrappers.</summary>
    internal WrapperSet(WrapperModel[] wrappers) => Wrappers = wrappers;

    /// <inheritdoc/>
    public bool Equals(WrapperSet? other) => ReferenceEquals(this, other) || other != null && Wrappers.SequenceEqual(other.Wrappers);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as WrapperSet);

    /// <inheritdoc/>
    public override int GetHashCode() => Wrappers.Length;
}
