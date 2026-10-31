using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;

namespace LuminPack.Code.Core;

/// <summary>Certifies state-independent Union payloads with an O(1) exact size or upper bound.</summary>
internal sealed class VariableUnionPayloadPlan
{
    private static readonly ConditionalWeakTable<Compilation, Dictionary<MetaInfo, State>> Cache = new();
    internal readonly string Root;
    internal readonly string Suffix;
    internal readonly List<Case> Cases;
    internal bool HasObjectElements => Cases.Any(item => item.Fields.Any(field => field.Kind >= 4));

    /// <summary>Captures a symbol-free root contract.</summary>
    /// <param name="root">The exact root name.</param>
    /// <param name="suffix">The existing virtual slot identity.</param>
    /// <param name="cases">The narrow cases in tag order.</param>
    private VariableUnionPayloadPlan(string root, string suffix, List<Case> cases)
    { Root = root; Suffix = suffix; Cases = cases; }

    /// <summary>Gets the compilation-local payload policy independently from target-specific kernel availability.</summary>
    /// <param name="type">The proposed Union root.</param>
    /// <param name="compilation">The owning compilation.</param>
    /// <param name="metadata">The complete generation settings.</param>
    /// <returns>The eligible variable root, or null for the existing operation.</returns>
    internal static VariableUnionPayloadPlan GetRoot(ITypeSymbol type, Compilation compilation, MetaInfo metadata)
    {
        if (type is not INamedTypeSymbol { TypeKind: TypeKind.Class, IsAbstract: true, IsGenericType: false } root ||
            !SymbolEqualityComparer.Default.Equals(root.ContainingAssembly, compilation.Assembly)) return null;
        var settings = Cache.GetValue(compilation, _ => new Dictionary<MetaInfo, State>(MetaInfoValueComparer.Instance));
        State state;
        lock (settings)
        {
            if (!settings.TryGetValue(metadata, out state))
            {
                var snapshot = new MetaInfo(metadata.ParseOptions, metadata.CSharpVersion, metadata.IsNet8,
                    metadata.IsNet9_OR_GREATER, metadata.AllowUnsafe, metadata.RegisterMode) { IsForUnity = metadata.IsForUnity };
                settings.Add(snapshot, state = new State(snapshot));
            }
        }
        return state.Roots.GetOrAdd(root, current => new Lazy<VariableUnionPayloadPlan>(() => Build(current, compilation, state.Metadata), LazyThreadSafetyMode.ExecutionAndPublication)).Value;
    }

    /// <summary>Finds the eligible owning root of a concrete payload without changing unrelated classes.</summary>
    /// <param name="type">The concrete payload.</param>
    /// <param name="compilation">The owning compilation.</param>
    /// <param name="metadata">The complete generation settings.</param>
    /// <returns>The case contract, or null.</returns>
    internal static Case GetCase(ITypeSymbol type, Compilation compilation, MetaInfo metadata)
    {
        if (type is not INamedTypeSymbol { IsAbstract: false, IsGenericType: false } named) return null;
        string name = FormatterTypeName.Get(named);
        for (var parent = named.BaseType; parent != null; parent = parent.BaseType)
        {
            var root = GetRoot(parent, compilation, metadata);
            var result = root?.Cases.FirstOrDefault(item => item.Type == name);
            if (result != null) return result;
        }
        return null;
    }

    /// <summary>Builds the complete root only when every generated case has a compatible payload.</summary>
    /// <param name="root">The local abstract root.</param>
    /// <param name="compilation">The current compilation.</param>
    /// <param name="metadata">The current generation settings.</param>
    /// <returns>A variable root plan, or null.</returns>
    private static VariableUnionPayloadPlan Build(INamedTypeSymbol root, Compilation compilation, MetaInfo metadata)
    {
        if (!CompilationTypeAnalysisCache.GetOrCreate(compilation).IsPackable(root)) return null;
        var data = LuminPackSourceGenerator.GetSerializationMetadata(root, compilation, metadata);
        if (!data.isUnion || !data.CanGenerateUnionDispatch || data.IsWideTag || data.UnionMembers.Count is < 2 or > 64 ||
            data.callBackMethods.Count != 0 || data.RentPoolMethod != null || CompiledOptions.Resolve(root, compilation).Input.Enabled) return null;
        var cases = new List<Case>();
        var seen = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
        foreach (var member in data.UnionMembers.OrderBy(item => item.Id))
        {
            if (member.Id >= 250 || member.Type is not INamedTypeSymbol { TypeKind: TypeKind.Class, IsAbstract: false, IsGenericType: false } concrete ||
                !seen.Add(concrete) || !SymbolEqualityComparer.Default.Equals(concrete.ContainingAssembly, compilation.Assembly) ||
                !LuminPackUnionDispatchCodeGenerator.IsFastPathMember(data, member)) return null;
            var payload = LuminPackSourceGenerator.GetSerializationMetadata(concrete, compilation, metadata);
            if (!IsOrdinary(payload, compilation)) return null;
            var fields = new List<Field>();
            foreach (var field in payload.fields)
            {
                var descriptor = Describe(field, compilation, metadata);
                if (descriptor == null) return null;
                fields.Add(descriptor);
            }
            cases.Add(new Case(member.Id, FormatterTypeName.Get(concrete), fields,
                fields.All(field => field.Kind == 0) && FixedUnionCursorPlan.CanUsePayload(concrete, compilation, metadata)));
        }
        return cases.Any(item => item.Fields.Any(field => field.Kind != 0))
            ? new VariableUnionPayloadPlan(FormatterTypeName.Get(root), LuminPackUnionDispatchCodeGenerator.GetSlotSuffix(root), cases) : null;
    }

    /// <summary>Rejects callbacks, construction policies and filtered or specialized storage boundaries.</summary>
    /// <param name="data">The canonical object metadata.</param>
    /// <param name="compilation">The current compilation.</param>
    /// <returns>Whether direct ordinary member operations are available.</returns>
    private static bool IsOrdinary(LuminDataInfo data, Compilation compilation)
        => data.generatorType == GeneratorType.Object && !data.isUnion && data.callBackMethods.Count == 0 && data.RentPoolMethod == null &&
            data.SelectedConstructor is not { Parameters.Count: > 0 } && !CompiledOptions.Resolve(data.TypeSymbol, compilation).Input.Enabled &&
            !data.Diagnostics.Any(item => item.Severity == DiagnosticSeverity.Error) &&
            data.fields.All(field => !field.IsPrivate && !field.isProperty && !field.IsVolatile && !field.IsCompress);

    /// <summary>Describes a field whose complete reservation uses only its captured length or count.</summary>
    /// <param name="field">The canonical field.</param>
    /// <param name="compilation">The current compilation.</param>
    /// <param name="metadata">The current generation settings.</param>
    /// <returns>A symbol-free descriptor, or null.</returns>
    private static Field Describe(LuminDataField field, Compilation compilation, MetaInfo metadata)
    {
        var type = field.TypeSymbol;
        string profile = field.Options?.IsDeclared == true ? field.Options.ScopeTypeName : null;
        if (IsScalar(type)) return new Field(field.Identifier, FormatterTypeName.Get(type), 0, null, null, profile);
        if (type.SpecialType == SpecialType.System_String)
            return field.Options?.IsDeclared == true && field.Options.Recording != 0 ? null : new Field(field.Identifier, "string", 1, null, null, profile);
        ITypeSymbol element;
        int kind;
        if (type is IArrayTypeSymbol { Rank: 1, IsSZArray: true } array) { element = array.ElementType; kind = 2; }
        else if (type is INamedTypeSymbol { MetadataName: "List`1", TypeArguments.Length: 1 } list &&
            list.ContainingNamespace.ToDisplayString() == "System.Collections.Generic") { element = list.TypeArguments[0]; kind = 3; }
        else return null;
        if (IsScalar(element)) return new Field(field.Identifier, FormatterTypeName.Get(type), kind, FormatterTypeName.Get(element), null, profile);
        if (element is not INamedTypeSymbol { TypeKind: TypeKind.Class, IsSealed: true, IsGenericType: false } child ||
            !SymbolEqualityComparer.Default.Equals(child.ContainingAssembly, compilation.Assembly) ||
            !CompilationTypeAnalysisCache.GetOrCreate(compilation).IsPackable(child)) return null;
        var nested = LuminPackSourceGenerator.GetSerializationMetadata(child, compilation, metadata);
        if (!IsOrdinary(nested, compilation) || nested.fields.Any(item => !IsScalar(item.TypeSymbol))) return null;
        string width = "1" + string.Concat(nested.fields.Select(item => " + global::System.Runtime.CompilerServices.Unsafe.SizeOf<" + FormatterTypeName.Get(item.TypeSymbol) + ">()"));
        return new Field(field.Identifier, FormatterTypeName.Get(type), kind + 2, FormatterTypeName.Get(element), width, profile,
            nested.fields.Select(item => "global::System.Runtime.CompilerServices.Unsafe.SizeOf<" + FormatterTypeName.Get(item.TypeSymbol) + ">()").ToArray());
    }

    /// <summary>Recognizes scalar contracts whose selected wire operation copies their fixed native storage.</summary>
    /// <param name="type">The exact field or element type.</param>
    /// <returns>Whether its original scalar operation is fixed raw storage.</returns>
    private static bool IsScalar(ITypeSymbol type) => LuminPackCodeGenerator.IsReservedElement(type);

    /// <summary>Owns semantic keys only within the weakly held compilation and an immutable settings snapshot.</summary>
    private sealed class State
    {
        internal readonly MetaInfo Metadata;
        internal readonly ConcurrentDictionary<INamedTypeSymbol, Lazy<VariableUnionPayloadPlan>> Roots = new(SymbolEqualityComparer.Default);

        /// <summary>Captures the generation settings used for every plan in this state.</summary>
        /// <param name="metadata">The immutable settings snapshot.</param>
        internal State(MetaInfo metadata) { Metadata = metadata; }
    }

    /// <summary>Stores one eligible case without compiler objects.</summary>
    internal sealed class Case
    {
        internal readonly int Id;
        internal readonly string Type;
        internal readonly List<Field> Fields;
        internal readonly bool FixedCursor;
        internal bool HasObjects => Fields.Any(field => field.Kind >= 4);

        /// <summary>Captures the original case identity and fields.</summary>
        /// <param name="id">The narrow tag.</param>
        /// <param name="type">The exact case name.</param>
        /// <param name="fields">The canonical ordered fields.</param>
        /// <param name="fixedCursor">Whether the original fixed address kernel already owns this case.</param>
        internal Case(int id, string type, List<Field> fields, bool fixedCursor) { Id = id; Type = type; Fields = fields; FixedCursor = fixedCursor; }

        /// <summary>Builds the complete payload bound from captured member expressions.</summary>
        /// <param name="value">The case expression.</param>
        /// <param name="profile">The ambient compiled profile.</param>
        /// <returns>An O(1), widened upper-bound expression without the Union tag.</returns>
        internal string Bound(string value, string profile) => "0L" + string.Concat(Fields.Select(field => " + (" + field.Bound(value + "." + field.Name, profile) + ")"));
    }

    /// <summary>Stores one primitive, string, raw sequence or fixed-object sequence field.</summary>
    internal sealed class Field
    {
        internal readonly string Name;
        internal readonly string Type;
        internal readonly int Kind;
        internal readonly string Element;
        internal readonly string ObjectWidth;
        internal readonly string Profile;
        internal readonly string[] ObjectMembers;

        /// <summary>Captures the field's symbol-free storage and wire facts.</summary>
        /// <param name="name">The escaped field identifier.</param>
        /// <param name="type">The exact storage type.</param>
        /// <param name="kind">Zero scalar, one string, two/three raw array/list or four/five fixed-object array/list.</param>
        /// <param name="element">The sequence element type.</param>
        /// <param name="objectWidth">The complete fixed object bound, or null.</param>
        /// <param name="profile">The declaration-owned profile, or null.</param>
        /// <param name="objectMembers">The fixed widths of a nested object's ordered members.</param>
        internal Field(string name, string type, int kind, string element, string objectWidth, string profile, string[] objectMembers = null)
        { Name = name; Type = type; Kind = kind; Element = element; ObjectWidth = objectWidth; Profile = profile; ObjectMembers = objectMembers; }

        /// <summary>Builds an overflow-safe bound from one member or captured snapshot.</summary>
        /// <param name="value">The member expression.</param>
        /// <param name="profile">The ambient compiled profile.</param>
        /// <returns>The complete field bound.</returns>
        internal string Bound(string value, string profile)
        {
            if (Kind == 0) return "global::System.Runtime.CompilerServices.Unsafe.SizeOf<" + Type + ">()";
            if (Kind == 1) return "(default(" + (Profile ?? profile) + ").StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF16 ? 4L + 2L * (" + value + "?.Length ?? 0) : 8L + 3L * (" + value + "?.Length ?? 0))";
            string count = value + "?." + (Kind is 3 or 5 ? "Count" : "Length") + " ?? 0";
            return "4L + (long)(" + count + ") * (" + (ObjectWidth ?? "global::System.Runtime.CompilerServices.Unsafe.SizeOf<" + Element + ">()") + ")";
        }
    }
}
