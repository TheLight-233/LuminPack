using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using LuminPack.SourceGenerator;
using LuminPack.SourceGenerator.CodeEmitters;
using Microsoft.CodeAnalysis;

namespace LuminPack.Code.Core;

/// <summary>Selects the approved fixed Union cursor protocol from canonical serialization metadata.</summary>
internal sealed class FixedUnionCursorPlan
{
    private static readonly ConditionalWeakTable<Compilation, Settings> Cache = new();
    private readonly HashSet<ITypeSymbol> _cases;
    internal readonly string Suffix;
    /// <summary>The fixed cases and their narrow tags in tag order, tested exactly at dictionary call sites before the cursor slot.</summary>
    internal readonly List<(int Id, string Type)> Ordered;

    /// <summary>Captures only the current compilation's eligible cases and original slot identity.</summary>
    /// <param name="root">The canonical Union root.</param>
    /// <param name="cases">Cases whose complete binary payload contains only raw fixed fields.</param>
    /// <param name="ordered">The same cases with their tags in tag order.</param>
    private FixedUnionCursorPlan(INamedTypeSymbol root, HashSet<ITypeSymbol> cases, List<(int Id, string Type)> ordered)
    {
        Suffix = LuminPackUnionDispatchCodeGenerator.GetSlotSuffix(root);
        _cases = cases;
        Ordered = ordered;
    }

    /// <summary>Checks the already selected canonical case without rediscovering registrations or tags.</summary>
    /// <param name="type">The exact case selected by Union analysis.</param>
    /// <returns>Whether its payload can return a local cursor.</returns>
    internal bool Contains(ITypeSymbol type) => _cases.Contains(type);

    /// <summary>Obtains a plan for a local, narrow, non-generic class Union with at least one fixed case.</summary>
    /// <param name="type">The proposed dictionary value or dispatch root.</param>
    /// <param name="compilation">The compilation owning all cached symbols.</param>
    /// <param name="metadata">The complete current emission settings.</param>
    /// <returns>The eligible root plan, or null for the original protocol.</returns>
    internal static FixedUnionCursorPlan GetRoot(ITypeSymbol type, Compilation compilation, MetaInfo metadata)
    {
        if (type is not INamedTypeSymbol { TypeKind: TypeKind.Class, IsAbstract: true, IsGenericType: false } root ||
            !IsLocal(root, compilation) || !CompilationTypeAnalysisCache.GetOrCreate(compilation).IsPackable(root)) return null;
        var state = Cache.GetValue(compilation, _ => new Settings()).Get(metadata);
        return state.Roots.GetOrAdd(root, current => new Lazy<FixedUnionCursorPlan>(() =>
            BuildRoot(current, compilation, state), LazyThreadSafetyMode.ExecutionAndPublication)).Value;
    }

    /// <summary>Checks the payload independently of sibling-case count, tag widths and emission order.</summary>
    /// <param name="type">A concrete case body being projected.</param>
    /// <param name="compilation">The current compilation.</param>
    /// <param name="metadata">Current language and generation settings.</param>
    /// <returns>Whether the canonical field operation can use the shared returning body.</returns>
    internal static bool CanUsePayload(ITypeSymbol type, Compilation compilation, MetaInfo metadata)
    {
        if (type is not INamedTypeSymbol { TypeKind: TypeKind.Class, IsAbstract: false, IsGenericType: false } named ||
            !IsLocal(named, compilation)) return false;
        bool hasRoot = false;
        for (var parent = named.BaseType; parent != null && parent.SpecialType != SpecialType.System_Object; parent = parent.BaseType)
        {
            if (parent.IsGenericType) return false;
            hasRoot |= parent.IsAbstract && IsLocal(parent, compilation) &&
                CompilationTypeAnalysisCache.GetOrCreate(compilation).IsPackable(parent);
        }
        if (!hasRoot) return false;
        var state = Cache.GetValue(compilation, _ => new Settings()).Get(metadata);
        return state.Payloads.GetOrAdd(named, current => new Lazy<bool>(() =>
            AnalyzePayload(current, compilation, state.Metadata), LazyThreadSafetyMode.ExecutionAndPublication)).Value;
    }

    /// <summary>Requires source ownership so no member is injected into an older referenced root.</summary>
    /// <param name="type">The root or case definition.</param>
    /// <param name="compilation">The owning compilation.</param>
    /// <returns>Whether partial dispatch can be emitted into the current source type.</returns>
    private static bool IsLocal(INamedTypeSymbol type, Compilation compilation)
        => SymbolEqualityComparer.Default.Equals(type.ContainingAssembly, compilation.Assembly) &&
           LuminPackUnionDispatchUtilities.CanGeneratePartial(type, compilation);

    /// <summary>Uses the original analyzer's complete Union members, including automatic registrations.</summary>
    /// <param name="root">The already ownership-checked root.</param>
    /// <param name="compilation">The current compilation.</param>
    /// <param name="state">Compilation-local facts for an immutable settings snapshot.</param>
    /// <returns>A narrow fixed-case plan, or null without changing the original dispatch.</returns>
    private static FixedUnionCursorPlan BuildRoot(INamedTypeSymbol root, Compilation compilation, State state)
    {
        var data = LuminPackSourceGenerator.GetSerializationMetadata(root, compilation, state.Metadata);
        if (!data.isUnion || !data.CanGenerateUnionDispatch || data.generatorType != GeneratorType.Object || data.IsWideTag ||
            data.Diagnostics.Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error) ||
            data.UnionMembers.Any(member => member.Id >= 250)) return null;
        var cases = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
        var ordered = new List<(int Id, string Type)>();
        foreach (var member in data.UnionMembers.OrderBy(member => member.Id))
            if (LuminPackUnionDispatchCodeGenerator.IsFastPathMember(data, member) &&
                CanUsePayload(member.Type, compilation, state.Metadata) && cases.Add(member.Type))
                ordered.Add((member.Id, member.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)));
        return cases.Count == 0 ? null : new FixedUnionCursorPlan(root, cases, ordered);
    }

    /// <summary>Certifies the filtered member contract and excludes all user-code and specialized boundaries.</summary>
    /// <param name="type">The source case whose complete payload is checked.</param>
    /// <param name="compilation">The current compilation.</param>
    /// <param name="metadata">The settings snapshot used by the canonical analyzer.</param>
    /// <returns>Whether every emitted field operation is a fixed raw storage operation.</returns>
    private static bool AnalyzePayload(INamedTypeSymbol type, Compilation compilation, MetaInfo metadata)
    {
        // Keep the deliberately conservative storage restrictions of the approved experiment.
        for (var current = type; current != null && current.SpecialType != SpecialType.System_Object; current = current.BaseType)
        {
            if (TypeMetaChecker.CheckGeneratorType(current) != GeneratorType.Object) return false;
            foreach (var member in current.GetMembers())
            {
                if (member is IMethodSymbol callback && callback.GetAttributes().Any(attribute => attribute.AttributeClass?.ToDisplayString() is
                    "LuminPack.Attribute.LuminPackOnSerializingAttribute" or "LuminPack.Attribute.LuminPackOnSerializedAttribute")) return false;
                if (member.IsStatic) continue;
                if (member is IPropertySymbol) return false;
                if (member is IFieldSymbol field && (field.DeclaredAccessibility != Accessibility.Public || field.IsVolatile ||
                    field.GetAttributes().Any(attribute => attribute.AttributeClass?.ContainingNamespace.ToDisplayString() == "LuminPack.Attribute"))) return false;
            }
        }
        var data = LuminPackSourceGenerator.GetSerializationMetadata(type, compilation, metadata);
        if (data.generatorType != GeneratorType.Object || data.isUnion || data.fields.Count == 0 ||
            data.Diagnostics.Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error) ||
            data.callBackMethods.Any(callback => callback.Item2 is SerializeCallBackType.OnSerializing or SerializeCallBackType.OnSerialized)) return false;
        var visited = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
        return data.fields.All(field => !field.IsPrivate && !field.isProperty && !field.IsVolatile && !field.IsCompress &&
            HasRawContract(field.TypeSymbol, visited));
    }

    /// <summary>Rejects nullable, nested Custom and specialized value contracts before raw payload selection.</summary>
    /// <param name="type">The complete field storage type.</param>
    /// <param name="visited">Finite value storage already inspected.</param>
    /// <returns>Whether its original selected operation is ordinary raw fixed storage.</returns>
    private static bool HasRawContract(ITypeSymbol type, HashSet<ITypeSymbol> visited)
    {
        if (!EmitterTypeTraits.CanUseUnmanagedSerialization(type)) return false;
        if (!visited.Add(type)) return true;
        if (type is not INamedTypeSymbol named) return true;
        if (named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T ||
            TypeMetaChecker.CheckGeneratorType(named) != GeneratorType.Object ||
            named.GetAttributes().Any(attribute => attribute.AttributeClass?.ToDisplayString() is
                LuminPackSourceGenerator.LUMIN_UNION_ATTRIBUTE or "System.Runtime.CompilerServices.UnionAttribute")) return false;
        return named.TypeKind != TypeKind.Struct || named.GetMembers().OfType<IFieldSymbol>()
            .Where(field => !field.IsStatic).All(field => HasRawContract(field.Type, visited));
    }

    /// <summary>Separates mutable public settings by complete immutable snapshots within one compilation.</summary>
    private sealed class Settings
    {
        private readonly Dictionary<MetaInfo, State> _states = new(MetaInfoValueComparer.Instance);

        /// <summary>Gets facts for precisely the current settings without retaining a mutable cache key.</summary>
        /// <param name="metadata">Current emission settings.</param>
        /// <returns>A state whose settings will not change under an existing plan.</returns>
        internal State Get(MetaInfo metadata)
        {
            lock (_states)
            {
                if (_states.TryGetValue(metadata, out var state)) return state;
                var snapshot = new MetaInfo(metadata.ParseOptions, metadata.CSharpVersion, metadata.IsNet8,
                    metadata.IsNet9_OR_GREATER, metadata.AllowUnsafe, metadata.RegisterMode) { IsForUnity = metadata.IsForUnity };
                state = new State(snapshot);
                _states.Add(snapshot, state);
                return state;
            }
        }
    }

    /// <summary>Retains semantic objects only through the owning compilation's weak-table entry.</summary>
    private sealed class State
    {
        internal readonly MetaInfo Metadata;
        internal readonly ConcurrentDictionary<INamedTypeSymbol, Lazy<FixedUnionCursorPlan>> Roots = new(SymbolEqualityComparer.Default);
        internal readonly ConcurrentDictionary<INamedTypeSymbol, Lazy<bool>> Payloads = new(SymbolEqualityComparer.Default);

        /// <summary>Captures the complete immutable settings snapshot.</summary>
        /// <param name="metadata">The cloned settings used by all facts in this state.</param>
        internal State(MetaInfo metadata) => Metadata = metadata;
    }
}
