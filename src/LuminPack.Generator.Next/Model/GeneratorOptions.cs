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

    /// <summary>Whether the referenced runtime uses a one-element span cursor.</summary>
    internal readonly bool SpanCursor;

    /// <summary>The simple type names the compilation declares in more than one namespace, or beside a common framework type,
    /// in ordinal order; generated names keep the namespace of these types.</summary>
    internal readonly string[] AmbiguousNames;

    /// <summary>Whether the referenced runtime declares the formatter interfaces: 1 when shared generic formatters can call
    /// owners through <c>default(T)</c>, 2 when laid-out elements can transfer contiguous blocks through them too, 0 when closed
    /// owners are kept.</summary>
    internal readonly int FormatterInterfaces;

    /// <summary>Whether the referenced Core supplies the built-in formatter table.</summary>
    internal readonly bool KnownFormatters;

    /// <summary>Whether the referenced Core exposes a shared reader for quoted framework scalars.</summary>
    internal readonly bool QuotedScalarReader;

    /// <summary>Whether the referenced Core supplies the generated JSON property-head writer.</summary>
    internal readonly bool GeneratedJsonPropertyWriter;

    /// <summary>Whether the referenced Core exposes the generated integer-run reader.</summary>
    internal readonly bool GeneratedJsonInt32Reader;

    /// <summary>Whether the referenced Core can prepare a reused hash set for a direct refill.</summary>
    internal readonly bool HashSetRefill;

    /// <summary>Whether the referenced Core can link linked-list nodes while elements are decoded.</summary>
    internal readonly bool LinkedListAppender;

    /// <summary>Whether the referenced Core can fill a fresh concurrent dictionary while pairs are decoded.</summary>
    internal readonly bool ConcurrentDictionaryBuilder;

    /// <summary>Whether the referenced Core can refill a queue's backing array in place.</summary>
    internal readonly bool QueueRefill;

    /// <summary>Whether the referenced Core can refill a stack's backing array in place.</summary>
    internal readonly bool StackRefill;

    /// <summary>Whether the referenced Core exposes the array behind an existing read-only collection.</summary>
    internal readonly bool ReadOnlyCollectionRefill;

    /// <summary>Whether the referenced Core exposes the dictionary behind an existing read-only dictionary.</summary>
    internal readonly bool ReadOnlyDictionaryRefill;

    /// <summary>Whether the referenced Core can refill an existing concurrent dictionary in place.</summary>
    internal readonly bool ConcurrentDictionaryRefill;

    /// <summary>Whether the referenced Core can seed a concurrent stack read buffer with the existing items.</summary>
    internal readonly bool ConcurrentStackSeed;

    /// <summary>Captures the project facts.</summary>
    /// <param name="net8">Whether to enable net8 for this operation.</param>
    /// <param name="csharpVersion">The csharp version supplied to <c>GeneratorOptions</c>.</param>
    /// <param name="unsafe">Whether to enable unsafe for this operation.</param>
    /// <param name="unity">Whether to enable unity for this operation.</param>
    /// <param name="registerMode">The register mode supplied to <c>GeneratorOptions</c>.</param>
    /// <param name="availableAliases">The available aliases supplied to <c>GeneratorOptions</c>.</param>
    /// <param name="assemblyName">The name of the assembly.</param>
    /// <param name="interceptors">Whether to enable interceptors for this operation.</param>
    /// <param name="ilRewrite">Whether to enable IL rewrite for this operation.</param>
    /// <param name="pointerCursor">Whether to enable pointer cursor for this operation.</param>
    /// <param name="ambiguousNames">The ambiguous names supplied to <c>GeneratorOptions</c>.</param>
    /// <param name="formatterInterfaces">The formatter interfaces supplied to <c>GeneratorOptions</c>.</param>
    /// <param name="knownFormatters">Whether to enable known formatters for this operation.</param>
    /// <param name="quotedScalarReader">Whether to enable quoted scalar reader for this operation.</param>
    /// <param name="spanCursor">Whether to enable span cursor for this operation.</param>
    /// <param name="generatedJsonPropertyWriter">Whether the referenced Core exposes the generated property-head writer.</param>
    /// <param name="generatedJsonInt32Reader">Whether the referenced Core exposes the generated integer-run reader.</param>
    /// <param name="hashSetRefill">Whether the referenced Core exposes the reused hash-set refill preparation.</param>
    /// <param name="linkedListAppender">Whether the referenced Core exposes the direct linked-list appender.</param>
    /// <param name="concurrentDictionaryBuilder">Whether the referenced Core exposes the concurrent dictionary builder.</param>
    /// <param name="queueRefill">Whether the referenced Core exposes the in-place queue refill.</param>
    /// <param name="stackRefill">Whether the referenced Core exposes the in-place stack refill.</param>
    /// <param name="readOnlyCollectionRefill">Whether the referenced Core exposes a read-only collection's backing array.</param>
    /// <param name="readOnlyDictionaryRefill">Whether the referenced Core exposes a read-only dictionary's backing dictionary.</param>
    /// <param name="concurrentDictionaryRefill">Whether the referenced Core exposes the in-place concurrent dictionary refill.</param>
    /// <param name="concurrentStackSeed">Whether the referenced Core exposes the concurrent stack read seeding.</param>
    internal GeneratorOptions(bool net8, int csharpVersion, bool @unsafe, bool unity, RegisterMode registerMode, int availableAliases,
        string assemblyName, bool interceptors, bool ilRewrite = false, bool pointerCursor = false, string[]? ambiguousNames = null,
        int formatterInterfaces = 0, bool knownFormatters = false, bool quotedScalarReader = false, bool spanCursor = false,
        bool generatedJsonPropertyWriter = false, bool generatedJsonInt32Reader = false, bool hashSetRefill = false,
        bool linkedListAppender = false, bool concurrentDictionaryBuilder = false,
        bool queueRefill = false, bool stackRefill = false, bool readOnlyCollectionRefill = false,
        bool readOnlyDictionaryRefill = false, bool concurrentDictionaryRefill = false,
        bool concurrentStackSeed = false)
    {
        ConcurrentStackSeed = concurrentStackSeed;
        ConcurrentDictionaryRefill = concurrentDictionaryRefill;
        ReadOnlyDictionaryRefill = readOnlyDictionaryRefill;
        ReadOnlyCollectionRefill = readOnlyCollectionRefill;
        StackRefill = stackRefill;
        QueueRefill = queueRefill;
        ConcurrentDictionaryBuilder = concurrentDictionaryBuilder;
        LinkedListAppender = linkedListAppender;
        HashSetRefill = hashSetRefill;
        GeneratedJsonPropertyWriter = generatedJsonPropertyWriter;
        GeneratedJsonInt32Reader = generatedJsonInt32Reader;
        QuotedScalarReader = quotedScalarReader;
        KnownFormatters = knownFormatters;
        FormatterInterfaces = formatterInterfaces;
        AmbiguousNames = ambiguousNames ?? Array.Empty<string>();
        PointerCursor = pointerCursor;
        SpanCursor = spanCursor;
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
        Interceptors == other.Interceptors && IlRewrite == other.IlRewrite && PointerCursor == other.PointerCursor && SpanCursor == other.SpanCursor && FormatterInterfaces == other.FormatterInterfaces &&
        KnownFormatters == other.KnownFormatters && QuotedScalarReader == other.QuotedScalarReader &&
        GeneratedJsonPropertyWriter == other.GeneratedJsonPropertyWriter && GeneratedJsonInt32Reader == other.GeneratedJsonInt32Reader &&
        HashSetRefill == other.HashSetRefill && LinkedListAppender == other.LinkedListAppender &&
        ConcurrentDictionaryBuilder == other.ConcurrentDictionaryBuilder && QueueRefill == other.QueueRefill &&
        StackRefill == other.StackRefill && ReadOnlyCollectionRefill == other.ReadOnlyCollectionRefill &&
        ReadOnlyDictionaryRefill == other.ReadOnlyDictionaryRefill &&
        ConcurrentDictionaryRefill == other.ConcurrentDictionaryRefill && ConcurrentStackSeed == other.ConcurrentStackSeed &&
        System.Linq.Enumerable.SequenceEqual(AmbiguousNames, other.AmbiguousNames);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as GeneratorOptions);

    /// <inheritdoc/>
    public override int GetHashCode() => (Net8 ? 1 : 0) | (CSharpVersion << 1);
}
