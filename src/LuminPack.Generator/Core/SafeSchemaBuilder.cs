using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using LuminPack.Code;
using LuminPack.Code.Core;
using LuminPack.SourceGenerator.CodeEmitters;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace LuminPack.SourceGenerator;

/// <summary>Derives an external-input graph from the same ordered members and storage plans as the formatter.</summary>
internal sealed class SafeSchemaBuilder
{
    private readonly Compilation _compilation;
    private readonly MetaInfo _metadata;
    private readonly List<string> _nodes = new List<string>();
    private readonly Dictionary<string, int> _indices = new Dictionary<string, int>(StringComparer.Ordinal);
    private readonly StringBuilder _identity = new StringBuilder("LuminPack.Safe.2|");
    private string _rejection;

    /// <summary>Retains symbols only during one uncached schema construction.</summary>
    /// <param name="compilation">The consumer compilation.</param>
    /// <param name="metadata">The formatter generation settings.</param>
    private SafeSchemaBuilder(Compilation compilation, MetaInfo metadata)
    { _compilation = compilation; _metadata = metadata; }

    /// <summary>Renders a self-contained immutable initializer without introducing another dispatch mechanism.</summary>
    /// <param name="type">The operation's exact root type.</param>
    /// <param name="compilation">The consumer compilation.</param>
    /// <param name="metadata">The current formatter settings.</param>
    /// <param name="options">The root's declaration-time profile and quotas.</param>
    /// <returns>The schema constructor expression.</returns>
    internal static string Build(ITypeSymbol type, Compilation compilation, MetaInfo metadata, CompiledOptions options)
    {
        var builder = new SafeSchemaBuilder(compilation, metadata);
        builder.Add(type, options);
        return "new global::LuminPack.Security.LuminPackSafeSchema(" + Literal(builder._identity.ToString()) +
            ", new global::LuminPack.Security.LuminPackSafeNode[] { " + string.Join(", ", builder._nodes) + " }, " + options.Encoding +
            ", " + (builder._rejection == null ? "null" : Literal(builder._rejection)) + ", " + options.Input.Arguments + ", maxJsonDepth: " +
            Math.Min(options.Input.Depth, options.Depth > 0 ? options.Depth : 64).ToString(CultureInfo.InvariantCulture) + ")";
    }

    /// <summary>Quotes compiler-produced identifiers as C# literals.</summary>
    /// <param name="text">The exact schema component.</param>
    /// <returns>The escaped literal.</returns>
    private static string Literal(string text) => SymbolDisplay.FormatLiteral(text, true);

    /// <summary>Closes recursive reference graphs while preserving per-member wire profiles.</summary>
    /// <param name="type">The field or element type.</param>
    /// <param name="options">The actual profile inherited by this operation.</param>
    /// <returns>The root-first graph index.</returns>
    private int Add(ITypeSymbol type, CompiledOptions options)
    {
        string name = FormatterTypeName.Get(type);
        string profile = options.Identity.Split('|')[0];
        string key = name + "|" + profile;
        if (_indices.TryGetValue(key, out int found)) return found;
        int index = _nodes.Count;
        _indices.Add(key, index);
        _nodes.Add(null);
        _identity.Append(index).Append(':').Append(key).Append(';');
        string kind = "Unsupported", storage = "8", extra = "";
        var children = new List<int>();
        var names = new List<string>();
        var jsonChildren = new List<int>();
        int rank = 0;
        if (type is IArrayTypeSymbol array)
        {
            kind = array.Rank == 1 ? "Sequence" : "MultiArray";
            rank = array.Rank;
            children.Add(Add(array.ElementType, options));
            RejectRawNullable(array.ElementType);
        }
        else if (type is INamedTypeSymbol named)
        {
            string full = named.OriginalDefinition.ToDisplayString();
            kind = named.SpecialType switch
            {
                SpecialType.System_Boolean => "Boolean", SpecialType.System_Byte => "Byte", SpecialType.System_SByte => "SByte",
                SpecialType.System_Int16 => "Int16", SpecialType.System_UInt16 => "UInt16", SpecialType.System_Int32 => "Int32",
                SpecialType.System_UInt32 => "UInt32", SpecialType.System_Int64 => "Int64", SpecialType.System_UInt64 => "UInt64",
                SpecialType.System_Single => "Single", SpecialType.System_Double => "Double", SpecialType.System_Char => "Char",
                SpecialType.System_String => "String", SpecialType.System_Decimal => "Decimal", SpecialType.System_DateTime => "DateTime",
                _ => full == "System.Guid" ? "Guid" : full == "System.TimeSpan" ? "TimeSpan" : "Unsupported"
            };
            if (kind != "Unsupported") storage = kind == "String" ? "8" : "global::System.Runtime.CompilerServices.Unsafe.SizeOf<" + name + ">()";
            else if (named.TypeKind == TypeKind.Enum)
            {
                kind = "Enum";
                var underlying = named.EnumUnderlyingType;
                rank = underlying.SpecialType is SpecialType.System_Byte or SpecialType.System_SByte ? 1 :
                    underlying.SpecialType is SpecialType.System_Int16 or SpecialType.System_UInt16 ? 2 :
                    underlying.SpecialType is SpecialType.System_Int32 or SpecialType.System_UInt32 ? 4 : 8;
                storage = rank.ToString(CultureInfo.InvariantCulture);
                bool signed = underlying.SpecialType is SpecialType.System_SByte or SpecialType.System_Int16 or SpecialType.System_Int32 or SpecialType.System_Int64;
                var values = named.GetMembers().OfType<IFieldSymbol>().Where(f => f.HasConstantValue).Select(f =>
                    (signed ? unchecked((ulong)Convert.ToInt64(f.ConstantValue, CultureInfo.InvariantCulture)) : Convert.ToUInt64(f.ConstantValue, CultureInfo.InvariantCulture)) &
                    (rank == 8 ? ulong.MaxValue : (1UL << (rank * 8)) - 1)).Distinct().OrderBy(v => v).ToArray();
                extra = ", values: new ulong[] { " + string.Join(", ", values.Select(v => v.ToString(CultureInfo.InvariantCulture) + "UL")) +
                    " }, flags: " + (named.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == "System.FlagsAttribute") ? "true" : "false") + ", signed: " + (signed ? "true" : "false");
                _identity.Append("enum=").Append(string.Join(",", values)).Append(extra);
            }
            else if (named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
            { kind = "Nullable"; children.Add(Add(named.TypeArguments[0], options)); storage = "global::System.Runtime.CompilerServices.Unsafe.SizeOf<" + name + ">()"; }
            else if (named.TypeArguments.Length != 0 && (named.ContainingNamespace.ToDisplayString() == "System" ||
                named.ContainingNamespace.ToDisplayString().StartsWith("System.", StringComparison.Ordinal)))
            {
                string simple = named.OriginalDefinition.MetadataName;
                if (simple is "Dictionary`2" or "IDictionary`2" or "IReadOnlyDictionary`2" or "SortedDictionary`2" or "SortedList`2" or "ConcurrentDictionary`2" or "ImmutableDictionary`2" or "ImmutableSortedDictionary`2" or "FrozenDictionary`2") kind = "Dictionary";
                else if (simple is "HashSet`1" or "SortedSet`1" or "ISet`1" or "IReadOnlySet`1" or "ImmutableHashSet`1" or "ImmutableSortedSet`1" or "FrozenSet`1") kind = "Set";
                else if (simple == "Queue`1") kind = "Queue";
                else if (simple is "List`1" or "IList`1" or "IReadOnlyList`1" or "IEnumerable`1" or "ICollection`1" or "IReadOnlyCollection`1" or "Stack`1" or "LinkedList`1" or "Memory`1" or "ReadOnlyMemory`1" or "ArraySegment`1" or "ImmutableArray`1" or "ImmutableList`1" or "ImmutableQueue`1" or "ImmutableStack`1" or "ConcurrentQueue`1" or "ConcurrentStack`1" or "ConcurrentBag`1" or "Collection`1" or "ObservableCollection`1" or "ReadOnlyCollection`1") kind = "Sequence";
                string expectedNamespace = simple is "Memory`1" or "ReadOnlyMemory`1" or "ArraySegment`1" ? "System" :
                    simple.StartsWith("Immutable", StringComparison.Ordinal) ? "System.Collections.Immutable" :
                    simple.StartsWith("Concurrent", StringComparison.Ordinal) ? "System.Collections.Concurrent" :
                    simple.StartsWith("Frozen", StringComparison.Ordinal) ? "System.Collections.Frozen" :
                    simple is "Collection`1" or "ObservableCollection`1" or "ReadOnlyCollection`1" ? "System.Collections.ObjectModel" : "System.Collections.Generic";
                if (named.ContainingNamespace.ToDisplayString() != expectedNamespace) kind = "Unsupported";
                if (kind != "Unsupported")
                {
                    foreach (var argument in named.TypeArguments) { children.Add(Add(argument, options)); RejectRawNullable(argument); }
                    if (kind is "Dictionary" or "Set")
                    {
                        var element = named.TypeArguments[0];
                        if (!IsKey(element) || simple.IndexOf("Sorted", StringComparison.Ordinal) >= 0 && element.SpecialType == SpecialType.System_String)
                            Reject("External collection keys must have a reviewed, culture-independent framework equality contract: " + name);
                    }
                }
            }
            else if (named.TypeKind is TypeKind.Class or TypeKind.Struct &&
                CompilationTypeAnalysisCache.GetOrCreate(_compilation).IsPackable(named))
            {
                var data = LuminPackSourceGenerator.GetSerializationMetadata(named, _compilation, _metadata);
                if (data.isUnion || data.generatorType != GeneratorType.Object || named.IsGenericType)
                    Reject("This external contract requires a separately reviewed Union, generic or specialized schema: " + name);
                else
                {
                    kind = named.IsUnmanagedType ? "Struct" : "Object";
                    rank = named.IsValueType ? 1 : 0;
                    storage = named.IsValueType ? "global::System.Runtime.CompilerServices.Unsafe.SizeOf<" + name + ">()" : ObjectStorage(named);
                    if (named.IsUnmanagedType)
                    {
                        if (!PortableStructPlan.IsOptedIn(named)) Reject("External unmanaged structs must explicitly select Packed layout: " + name);
                        var plan = PortableStructPlan.TryCreate(named);
                        if (plan != null)
                            foreach (var field in plan.Fields) children.Add(Add(field.Type, options));
                        else
                            foreach (var field in named.GetMembers().OfType<IFieldSymbol>().Where(f => !f.IsStatic)) children.Add(Add(field.Type, options));
                    }
                    foreach (var field in data.fields)
                    {
                        if (field.IsCompress) Reject("Compressed members require a separately reviewed bounded member schema: " + name + "." + field.Name);
                        var memberOptions = field.Options ?? options;
                        int child = Add(field.TypeSymbol, memberOptions);
                        if (!named.IsUnmanagedType) children.Add(child);
                        jsonChildren.Add(child); names.Add(field.Name);
                    }
                    _identity.Append("members=").Append(string.Join(",", names)).Append(';');
                }
            }
        }
        if (kind == "Unsupported") Reject("The external-input option does not accept this unreviewed type: " + name);
        _identity.Append("shape=").Append(kind).Append(':').Append(rank).Append(':').Append(string.Join(",", children)).Append(';');
        _nodes[index] = "new global::LuminPack.Security.LuminPackSafeNode(global::LuminPack.Security.LuminPackSafeKind." + kind + ", " + storage +
            ", mode: " + options.Mode + ", rank: " + rank + ", children: new int[] { " + string.Join(", ", children) +
            " }, jsonChildren: new int[] { " + string.Join(", ", jsonChildren) + " }, names: new string[] { " + string.Join(", ", names.Select(Literal)) + " }" + extra + ")";
        return index;
    }

    /// <summary>Rejects raw nullable aggregates whose collection wire differs from their ordinary nullable formatter.</summary>
    /// <param name="type">The contiguous element contract.</param>
    private void RejectRawNullable(ITypeSymbol type)
    { if (type.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T) Reject("Raw nullable collection elements require a separately reviewed storage schema."); }

    /// <summary>Restricts duplicate checking to equality contracts independent of application callbacks.</summary>
    /// <param name="type">The key or set element.</param>
    /// <returns>Whether the validator exactly reproduces framework equality.</returns>
    private static bool IsKey(ITypeSymbol type) => type.TypeKind == TypeKind.Enum || type.SpecialType is
        SpecialType.System_Boolean or SpecialType.System_Byte or SpecialType.System_SByte or SpecialType.System_Int16 or SpecialType.System_UInt16 or
        SpecialType.System_Int32 or SpecialType.System_UInt32 or SpecialType.System_Int64 or SpecialType.System_UInt64 or SpecialType.System_Single or
        SpecialType.System_Double or SpecialType.System_Char or SpecialType.System_String || type.ToDisplayString() is "System.Guid" or "System.TimeSpan";

    /// <summary>Includes ignored private and inherited storage in the conservative application-object allocation estimate.</summary>
    /// <param name="type">The constructed class.</param>
    /// <returns>A platform-neutral upper bound expression.</returns>
    private string ObjectStorage(INamedTypeSymbol type)
    {
        var widths = new List<string> { "64" };
        for (var current = type; current != null; current = current.BaseType)
        {
            foreach (var attribute in current.GetAttributes().Where(a => a.AttributeClass?.ToDisplayString() == "System.Runtime.InteropServices.StructLayoutAttribute"))
            foreach (var argument in attribute.NamedArguments.Where(a => a.Key == "Size" && a.Value.Value is int size && size > 0))
                widths.Add(Convert.ToInt32(argument.Value.Value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture));
            foreach (var field in current.GetMembers().OfType<IFieldSymbol>().Where(f => !f.IsStatic))
            {
                if (field.Type.IsValueType && !_compilation.IsSymbolAccessibleWithin(field.Type, _compilation.Assembly))
                    Reject("External object allocation cannot prove inaccessible value storage: " + FormatterTypeName.Get(type) + "." + field.Name);
                widths.Add(field.Type.IsValueType && _compilation.IsSymbolAccessibleWithin(field.Type, _compilation.Assembly) ?
                    "global::System.Runtime.CompilerServices.Unsafe.SizeOf<" + FormatterTypeName.Get(field.Type) + ">() * 2" : "16");
                foreach (var attribute in field.GetAttributes().Where(a => a.AttributeClass?.ToDisplayString() == "System.Runtime.InteropServices.FieldOffsetAttribute"))
                    if (attribute.ConstructorArguments.Length == 1 && attribute.ConstructorArguments[0].Value is int offset && offset > 0)
                        widths.Add(offset.ToString(CultureInfo.InvariantCulture));
            }
        }
        return "checked(" + string.Join(" + ", widths) + ")";
    }

    /// <summary>Keeps the first unsupported shape as a deterministic fail-closed explanation.</summary>
    /// <param name="reason">The unreviewed mechanism.</param>
    private void Reject(string reason) { _rejection ??= reason; }
}
