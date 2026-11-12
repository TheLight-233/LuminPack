using System;

namespace LuminPack.SourceGenerator.Next.Model;

/// <summary>How case types register with a Union root.</summary>
internal enum RegisterMode
{
    /// <summary>The generated registry accepts runtime registrations.</summary>
    Enabled,

    /// <summary>Registration slots throw instead of accepting a case.</summary>
    Disabled,
}

/// <summary>The project-level facts a payload model and its renderer depend on.</summary>
internal sealed class GeneratorOptions : IEquatable<GeneratorOptions>
{
    /// <summary>Whether the target framework is .NET 8 or later.</summary>
    internal readonly bool Net8;

    /// <summary>The C# language version number, such as 1200 for C# 12.</summary>
    internal readonly int CSharpVersion;

    /// <summary>Whether unsafe code is allowed.</summary>
    internal readonly bool Unsafe;

    /// <summary>Whether the consuming project is a Unity project.</summary>
    internal readonly bool Unity;

    /// <summary>The Union registration mode.</summary>
    internal readonly RegisterMode RegisterMode;

    /// <summary>The alias mask the consumer leaves available.</summary>
    internal readonly int AvailableAliases;

    /// <summary>The consuming assembly name, used by generated container and formatter names.</summary>
    internal readonly string AssemblyName;

    /// <summary>Whether the consumer binds closed calls statically, which gives entries payload-specific names.</summary>
    internal readonly bool Interceptors;

    /// <summary>Whether static binding uses the installed IL rewriter instead of compiler interception.</summary>
    internal readonly bool IlRewrite;

    /// <summary>Whether the referenced runtime's advancing states expose a <c>byte*</c> cursor (netstandard2.1).</summary>
    internal readonly bool PointerCursor;

    /// <summary>Captures the project facts.</summary>
    internal GeneratorOptions(bool net8, int csharpVersion, bool @unsafe, bool unity, RegisterMode registerMode, int availableAliases,
        string assemblyName, bool interceptors, bool ilRewrite = false, bool pointerCursor = false)
    {
        PointerCursor = pointerCursor;
        IlRewrite = ilRewrite;
        Interceptors = interceptors;
        AssemblyName = assemblyName;
        AvailableAliases = availableAliases;
        RegisterMode = registerMode;
        Unity = unity;
        Unsafe = @unsafe;
        CSharpVersion = csharpVersion;
        Net8 = net8;
    }

    /// <inheritdoc/>
    public bool Equals(GeneratorOptions? other) => ReferenceEquals(this, other) || other != null && Net8 == other.Net8 &&
        CSharpVersion == other.CSharpVersion && Unsafe == other.Unsafe && Unity == other.Unity &&
        RegisterMode == other.RegisterMode && AvailableAliases == other.AvailableAliases && AssemblyName == other.AssemblyName &&
        Interceptors == other.Interceptors && IlRewrite == other.IlRewrite && PointerCursor == other.PointerCursor;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as GeneratorOptions);

    /// <inheritdoc/>
    public override int GetHashCode() => (Net8 ? 1 : 0) | (CSharpVersion << 1);
}
