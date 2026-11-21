using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using LuminPack.SourceGenerator.Next.Model;
using LuminPack.SourceGenerator.Next.Rendering.Emitters;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LuminPack.SourceGenerator.Next.Analysis;

/// <summary>Selects the operations a project calls and the payload identities each call and declaration reaches.</summary>
/// <remarks>This ports the original generator's operation selection (<c>GeneratedOperationUsage</c>). Only an application, or a
/// Unity predefined assembly, without friend assemblies is closed: no other assembly can call its generated code, so an operation
/// it never calls is left out. Every other project, and every operation named through
/// <c>LuminPackGeneratorOptions.ForceGenerate*</c>, keeps all payloads. A facade call whose payload is not provable, a
/// registration, a direct state call and a generated extension name keep their operation for every payload.</remarks>
internal static class OperationUsageAnalysis
{
    private const string SerializerName = "LuminPack.LuminPackSerializer";

    private static readonly ConditionalWeakTable<Compilation, StrongBox<GeneratedOperations>> FixedCache = new();

    /// <summary>The operations every payload keeps regardless of calls.</summary>
    /// <param name="compilation">The consumer compilation.</param>
    /// <returns>Every operation for an open project, otherwise the forced operations.</returns>
    internal static GeneratedOperations Fixed(Compilation compilation) =>
        FixedCache.GetValue(compilation, static item => new StrongBox<GeneratedOperations>(CreateFixed(item))).Value;

    /// <summary>Creates fixed.</summary>
    /// <param name="compilation">The compilation that supplies source symbols and references.</param>
    /// <returns>The fixed.</returns>
    private static GeneratedOperations CreateFixed(Compilation compilation)
    {
        bool application = compilation.Options.OutputKind is OutputKind.ConsoleApplication or OutputKind.WindowsApplication or
            OutputKind.WindowsRuntimeApplication || compilation.AssemblyName is "Assembly-CSharp" or "Assembly-CSharp-Editor";
        if (!application)
            return GeneratedOperations.All;
        var forced = GeneratedOperations.None;
        foreach (AttributeData attribute in compilation.Assembly.GetAttributes())
        {
            string? name = attribute.AttributeClass?.ToDisplayString();
            if (name == "System.Runtime.CompilerServices.InternalsVisibleToAttribute")
                return GeneratedOperations.All;
            if (name != "LuminPack.Attribute.LuminPackGeneratorOptionsAttribute")
                continue;
            foreach (KeyValuePair<string, TypedConstant> argument in attribute.NamedArguments)
                if (argument.Value.Value is true)
                    forced |= argument.Key switch
                    {
                        "ForceGenerateSerialize" => GeneratedOperations.BinaryWrite,
                        "ForceGenerateDeserialize" => GeneratedOperations.BinaryRead,
                        "ForceGenerateSerializeJson" => GeneratedOperations.JsonWrite,
                        "ForceGenerateDeserializeJson" => GeneratedOperations.JsonRead,
                        _ => GeneratedOperations.None,
                    };
        }
        return forced;
    }

    /// <summary>Maps a bound facade call to its operation.</summary>
    /// <param name="call">The bound call.</param>
    /// <returns>The call's operation.</returns>
    internal static GeneratedOperations Of(SerializerCallModel call) => call.Json
        ? call.Writes ? GeneratedOperations.JsonWrite : GeneratedOperations.JsonRead
        : call.Writes ? GeneratedOperations.BinaryWrite : GeneratedOperations.BinaryRead;

    /// <summary>Maps a facade member name to its operation.</summary>
    /// <param name="name">The facade member name.</param>
    /// <returns>The operation; compression needs none, and registration and other members keep every operation.</returns>
    internal static GeneratedOperations Of(string name) => name switch
    {
        "Serialize" or "SerializeAsync" => GeneratedOperations.BinaryWrite,
        "Deserialize" or "DeserializeAsync" => GeneratedOperations.BinaryRead,
        "SerializeJson" or "SerializeJsonAsync" => GeneratedOperations.JsonWrite,
        "DeserializeJson" or "DeserializeJsonAsync" => GeneratedOperations.JsonRead,
        "Compress" or "Decompress" => GeneratedOperations.None,
        _ => GeneratedOperations.All,
    };

    /// <summary>Classifies a facade invocation that does not bind to a concrete root.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="invocation">The invocation.</param>
    /// <param name="symbol">The bound method, or null.</param>
    /// <param name="cancellationToken">Cancels analysis.</param>
    /// <returns>The operations every payload keeps for this call.</returns>
    internal static GeneratedOperations Unbound(SemanticModel model, InvocationExpressionSyntax invocation, IMethodSymbol? symbol,
        CancellationToken cancellationToken)
    {
        if (Fixed(model.Compilation) == GeneratedOperations.All)
            return GeneratedOperations.None;
        if (symbol != null)
            return IsFacade(symbol) ? Of(symbol.Name) : GeneratedOperations.None;
        // Another generator can supply missing argument types, but cannot change the operation named by this call.
        if (!IsFacadeExpression(model, invocation.Expression, cancellationToken))
            return GeneratedOperations.None;
        return invocation.Expression switch
        {
            MemberAccessExpressionSyntax access => Of(access.Name.Identifier.ValueText),
            SimpleNameSyntax name => Of(name.Identifier.ValueText),
            _ => GeneratedOperations.All,
        };
    }

    /// <summary>Checks the syntax shape of a usage that selects operations outside closed facade calls.</summary>
    /// <param name="node">The candidate node.</param>
    /// <returns>Whether the node names a generated extension, a state call, a registration or a facade method group.</returns>
    internal static bool IsUsageCandidate(SyntaxNode node)
    {
        if (node is not SimpleNameSyntax name)
            return false;
        string text = name.Identifier.ValueText;
        if (IsExtensionName(text))
            return true;
        bool invoked = Invoked(name, out _);
        return text switch
        {
            "Write" or "Read" or "Register" => invoked,
            "Serialize" or "Deserialize" or "SerializeJson" or "DeserializeJson" or "SerializeAsync" or "DeserializeAsync" or
                "SerializeJsonAsync" or "DeserializeJsonAsync" => !invoked,
            _ => false,
        };
    }

    /// <summary>Selects the operations a usage keeps for every payload.</summary>
    /// <param name="context">The syntax and its semantic model.</param>
    /// <param name="cancellationToken">Cancels analysis.</param>
    /// <returns>The operations every payload keeps.</returns>
    internal static GeneratedOperations Usage(GeneratorSyntaxContext context, CancellationToken cancellationToken)
    {
        Compilation compilation = context.SemanticModel.Compilation;
        if (Fixed(compilation) == GeneratedOperations.All)
            return GeneratedOperations.None;
        var name = (SimpleNameSyntax)context.Node;
        string text = name.Identifier.ValueText;
        SemanticModel model = BindingSemanticModel.For(compilation, name.SyntaxTree);
        bool invoked = Invoked(name, out ExpressionSyntax expression);
        if (IsExtensionName(text))
        {
            // A bound field or property reference is independent of generated extension methods.
            if (!invoked && model.GetSymbolInfo(expression, cancellationToken).Symbol is IFieldSymbol or IPropertySymbol)
                return GeneratedOperations.None;
            return GeneratedOperations.All;
        }
        if (invoked && text is "Write" or "Read")
        {
            if (expression is MemberAccessExpressionSyntax access && expression.Parent is InvocationExpressionSyntax { ArgumentList.Arguments.Count: 1 })
                return OfState(model.GetTypeInfo(access.Expression, cancellationToken).Type);
            return GeneratedOperations.None;
        }
        SymbolInfo info = model.GetSymbolInfo(invoked ? expression.Parent! : expression, cancellationToken);
        if (info.Symbol is IMethodSymbol method)
            return IsFacade(method) ? Of(method.Name) : GeneratedOperations.None;
        if (!IsFacadeExpression(model, expression, cancellationToken))
            return GeneratedOperations.None;
        // An unresolved method group can bind after generation, but every overload has the same direction.
        return Of(text);
    }

    /// <summary>Collects the identities a packable declaration reaches through its members, bases, interfaces and attribute types.</summary>
    /// <param name="symbol">The packable declaration.</param>
    /// <param name="compilation">The consumer compilation.</param>
    /// <returns>The reached identities, or none when the project keeps every operation.</returns>
    internal static OperationReach Declaration(INamedTypeSymbol symbol, Compilation compilation)
    {
        if (Fixed(compilation) == GeneratedOperations.All)
            return OperationReach.Empty;
        var walk = new ReachWalk(compilation);
        walk.Members(symbol);
        foreach (INamedTypeSymbol contract in symbol.AllInterfaces)
            walk.Visit(contract);
        foreach (AttributeData attribute in symbol.GetAttributes())
        {
            foreach (TypedConstant argument in attribute.ConstructorArguments)
                walk.Constant(argument);
            foreach (KeyValuePair<string, TypedConstant> argument in attribute.NamedArguments)
                walk.Constant(argument.Value);
        }
        // A Union root discovers its derived cases, so each source base and interface reaches this declaration.
        var bases = new List<string>();
        for (INamedTypeSymbol? current = symbol.BaseType; current != null && InSource(current.OriginalDefinition); current = current.BaseType)
            AddBase(bases, current);
        foreach (INamedTypeSymbol contract in symbol.AllInterfaces)
            if (InSource(contract.OriginalDefinition))
                AddBase(bases, contract);
        return walk.Result(bases);
    }

    /// <summary>Collects the identities a closed call payload reaches.</summary>
    /// <param name="payload">The closed call payload.</param>
    /// <param name="compilation">The consumer compilation.</param>
    /// <returns>The reached identities, or none when the project keeps every operation.</returns>
    internal static OperationReach Payload(ITypeSymbol payload, Compilation compilation)
    {
        if (Fixed(compilation) == GeneratedOperations.All)
            return OperationReach.Empty;
        var walk = new ReachWalk(compilation);
        walk.Visit(payload);
        return walk.Result();
    }

    /// <summary>Adds base.</summary>
    /// <param name="bases">The bases supplied to <c>AddBase</c>.</param>
    /// <param name="type">The type being analyzed or emitted.</param>
    private static void AddBase(List<string> bases, INamedTypeSymbol type)
    {
        if (TypeModelFactory.ReachKey(type) is { } key)
            bases.Add(key);
        if (!type.IsDefinition && TypeModelFactory.ReachKey(type.OriginalDefinition) is { } definition)
            bases.Add(definition);
    }

    /// <summary>Handles of state for <c>OperationUsageAnalysis</c>.</summary>
    /// <param name="receiver">The receiver supplied to <c>OfState</c>.</param>
    /// <returns>The resulting <c>GeneratedOperations</c>.</returns>
    private static GeneratedOperations OfState(ITypeSymbol? receiver) => receiver?.ToDisplayString() switch
    {
        "LuminPack.Core.LuminPackWriter" => GeneratedOperations.BinaryWrite,
        "LuminPack.Core.LuminPackReader" => GeneratedOperations.BinaryRead,
        "LuminPack.Core.LuminPackJsonWriter" => GeneratedOperations.JsonWrite,
        "LuminPack.Core.LuminPackJsonReader" => GeneratedOperations.JsonRead,
        _ => GeneratedOperations.None,
    };

    /// <summary>Checks whether extension name.</summary>
    /// <param name="name">The name to look up or emit.</param>
    /// <returns>Whether the input is extension name.</returns>
    private static bool IsExtensionName(string name) => name is "WriteValue" or "ReadValue" or "WriteMemberValue" or
        "WritePolymorphismValue" or "ReadPolymorphismValue" or "ReadFreshValue" or "WriteValueWithCompress" or "ReadValueWithCompress" or
        "WriteValueJson" or "ReadValueJson" or "MeasureRecordWidth";

    /// <summary>Handles invoked for <c>OperationUsageAnalysis</c>.</summary>
    /// <param name="name">The name to look up or emit.</param>
    /// <param name="expression">Receives the expression when the operation succeeds.</param>
    /// <returns>Whether the input satisfies the invoked check.</returns>
    private static bool Invoked(SimpleNameSyntax name, out ExpressionSyntax expression)
    {
        expression = name.Parent is MemberAccessExpressionSyntax access && access.Name == name ? access : name;
        return expression.Parent is InvocationExpressionSyntax invocation && invocation.Expression == expression;
    }

    /// <summary>Checks whether facade.</summary>
    /// <param name="method">The method being inspected.</param>
    /// <returns>Whether the input is facade.</returns>
    private static bool IsFacade(IMethodSymbol method) => method.ContainingType?.ToDisplayString() == SerializerName;

    /// <summary>Checks whether facade expression.</summary>
    /// <param name="model">The normalized model for this contract.</param>
    /// <param name="expression">The expression supplied to <c>IsFacadeExpression</c>.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Whether the input is facade expression.</returns>
    private static bool IsFacadeExpression(SemanticModel model, ExpressionSyntax expression, CancellationToken cancellationToken)
    {
        SymbolInfo info = model.GetSymbolInfo(expression, cancellationToken);
        if (info.Symbol is IMethodSymbol method && IsFacade(method))
            return true;
        foreach (ISymbol candidate in info.CandidateSymbols)
            if (candidate is IMethodSymbol option && IsFacade(option))
                return true;
        if (expression is not MemberAccessExpressionSyntax access)
            return false;
        ISymbol? receiver = model.GetSymbolInfo(access.Expression, cancellationToken).Symbol;
        if (receiver is IAliasSymbol alias)
            receiver = alias.Target;
        return receiver?.ToDisplayString() == SerializerName;
    }

    /// <summary>Handles in source for <c>OperationUsageAnalysis</c>.</summary>
    /// <param name="type">The type being analyzed or emitted.</param>
    /// <returns>Whether the input satisfies the in source check.</returns>
    private static bool InSource(INamedTypeSymbol type)
    {
        foreach (Location location in type.Locations)
            if (location.IsInSource)
                return true;
        return false;
    }

    /// <summary>Walks member types through unannotated source types, which are expanded into the code that reaches them.</summary>
    private sealed class ReachWalk
    {
        private readonly List<string> _keys = new();
        private readonly HashSet<string> _seen = new(StringComparer.Ordinal);
        private readonly HashSet<ITypeSymbol> _walked = new(SymbolEqualityComparer.Default);
        private readonly IReadOnlyList<INamedTypeSymbol> _external;
        private readonly Compilation _compilation;

        /// <summary>Creates a reach walk with the consumer's assembly-marked metadata roots.</summary>
        /// <param name="compilation">The consumer compilation and assembly markers.</param>
        internal ReachWalk(Compilation compilation)
        {
            _compilation = compilation;
            _external = TypeModelFactory.MarkedTypes(compilation);
        }

        /// <summary>Handles result for <c>ReachWalk</c>.</summary>
        /// <param name="bases">The bases supplied to <c>Result</c>.</param>
        /// <returns>The resulting <c>OperationReach</c>.</returns>
        internal OperationReach Result(List<string>? bases = null) => _keys.Count == 0 && (bases == null || bases.Count == 0)
            ? OperationReach.Empty
            : new OperationReach(_keys.ToArray(), bases == null || bases.Count == 0 ? Array.Empty<string>() : bases.ToArray());

        /// <summary>Handles constant for <c>ReachWalk</c>.</summary>
        /// <param name="constant">The constant supplied to <c>Constant</c>.</param>
        internal void Constant(TypedConstant constant)
        {
            if (constant.Kind == TypedConstantKind.Type && constant.Value is ITypeSymbol type)
                Visit(type);
            else if (constant.Kind == TypedConstantKind.Array && !constant.IsNull)
                foreach (TypedConstant item in constant.Values)
                    Constant(item);
        }

        /// <summary>Handles visit for <c>ReachWalk</c>.</summary>
        /// <param name="type">The type being analyzed or emitted.</param>
        internal void Visit(ITypeSymbol type)
        {
            while (type is IArrayTypeSymbol array)
                type = array.ElementType;
            if (type is not INamedTypeSymbol named || named.TypeKind is TypeKind.Enum or TypeKind.Delegate or TypeKind.Error)
                return;
            if (named.IsGenericType)
                foreach (ITypeSymbol argument in named.TypeArguments)
                    Visit(argument);
            INamedTypeSymbol definition = named.OriginalDefinition;
            if (!InSource(definition))
            {
                bool open = false;
                for (int index = 0; index < _external.Count; index++)
                {
                    if (SymbolEqualityComparer.Default.Equals(_external[index], named))
                    {
                        Add(TypeModelFactory.ReachKey(named));
                        return;
                    }
                    open |= SymbolEqualityComparer.Default.Equals(_external[index], definition);
                }
                if (open)
                {
                    Add(TypeModelFactory.ReachKey(named));
                    if (TypeModelFactory.IsUnion(definition))
                        Add(TypeModelFactory.ReachKey(definition));
                }
                return;
            }
            Add(TypeModelFactory.ReachKey(named));
            if (!named.IsDefinition)
                Add(TypeModelFactory.ReachKey(definition));
            if (!TypeModelFactory.IsPackable(definition) || CompilerUnion.IsUnion(definition, _compilation))
                Members(named);
        }

        /// <summary>Handles members for <c>ReachWalk</c>.</summary>
        /// <param name="type">The type being analyzed or emitted.</param>
        internal void Members(INamedTypeSymbol type)
        {
            if (!_walked.Add(type))
                return;
            if (CompilerUnion.IsUnion(type, _compilation))
            {
                foreach (ITypeSymbol item in CompilerUnion.Cases(type, _compilation))
                    Visit(item);
                return;
            }
            foreach (ISymbol member in type.GetMembers())
            {
                if (member.IsStatic)
                    continue;
                if (member is IFieldSymbol { IsConst: false } field)
                    Visit(field.Type);
                else if (member is IPropertySymbol { IsIndexer: false } property)
                    Visit(property.Type);
            }
            if (type.BaseType is { } baseType)
                Visit(baseType);
        }

        /// <summary>Handles add for <c>ReachWalk</c>.</summary>
        /// <param name="key">The key identifying the entry.</param>
        private void Add(string? key)
        {
            if (key != null && _seen.Add(key))
                _keys.Add(key);
        }
    }
}
