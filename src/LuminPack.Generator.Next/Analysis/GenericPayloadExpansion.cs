using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using LuminPack.SourceGenerator.Next.Model;
using LuminPack.SourceGenerator.Next.Pipeline;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LuminPack.SourceGenerator.Next.Analysis;

/// <summary>Closes serializer payloads that source generic methods forward from their type parameters.</summary>
/// <remarks>This ports the original generator's API payload expansion. A facade call inside a generic method, such as
/// <c>Check&lt;T&gt;(T value) =&gt; LuminPackSerializer.Serialize(value)</c>, names an open payload. Every source call that closes the
/// method, such as <c>Check(new ReadOnlyMemory&lt;double&gt;())</c>, substitutes its type arguments, and an argument that is itself a
/// type parameter continues to that method's own callers. The closed results publish shared owner roots like closed facade calls,
/// so registrations that bind the generated roots by name find them. Only invocations whose name can reach the facade are bound.</remarks>
internal static class GenericPayloadExpansion
{
    private const string SerializerName = "LuminPack.LuminPackSerializer";

    private static readonly HashSet<string> SerializerNames = new(StringComparer.Ordinal)
    {
        "Serialize", "Deserialize", "SerializeJson", "DeserializeJson", "SerializeAsync", "DeserializeAsync", "SerializeJsonAsync",
        "DeserializeJsonAsync", "Register",
    };

    /// <summary>Finds the closed roots that generic forwarding reaches.</summary>
    /// <param name="compilation">The consuming compilation.</param>
    /// <param name="cancellationToken">Cancels analysis.</param>
    /// <returns>The value-equal root members, in ordinal order of their payload spelling.</returns>
    internal static RootMembers Roots(Compilation compilation, CancellationToken cancellationToken)
    {
        HashSet<string> relevant = RelevantNames(compilation, cancellationToken);
        var seeds = new List<ITypeSymbol>();
        var invocations = new List<IMethodSymbol>();
        foreach (SyntaxTree tree in compilation.SyntaxTrees)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SemanticModel? model = null;
            foreach (InvocationExpressionSyntax call in tree.GetRoot(cancellationToken).DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                string? name = InvocationName(call);
                if (name == null || !relevant.Contains(name))
                    continue;
                model ??= compilation.GetSemanticModel(tree);
                if (model.GetSymbolInfo(call, cancellationToken).Symbol is not IMethodSymbol method)
                    continue;
                if (method.ContainingType?.ToDisplayString() == SerializerName)
                {
                    if (method.IsGenericMethod && SerializerNames.Contains(method.Name) && ContainsTypeParameter(method.TypeArguments[0]))
                        seeds.Add(method.TypeArguments[0]);
                }
                else if (method.IsGenericMethod || method.ContainingType is { IsGenericType: true })
                {
                    invocations.Add(method);
                }
            }
        }
        if (seeds.Count == 0)
            return RootMembers.Empty;

        var index = new Dictionary<ISymbol, List<IMethodSymbol>>(SymbolEqualityComparer.Default);
        foreach (IMethodSymbol method in invocations)
            foreach (ISymbol key in new ISymbol[] { method.OriginalDefinition, method.ContainingType.OriginalDefinition })
            {
                if (!index.TryGetValue(key, out List<IMethodSymbol>? methods))
                    index[key] = methods = new List<IMethodSymbol>();
                methods.Add(method);
            }

        var closed = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
        var pending = new Queue<Slot>();
        var visited = new HashSet<Slot>();
        foreach (ITypeSymbol seed in seeds)
            Collect(seed, closed, pending, direct: true);
        while (pending.Count != 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Slot slot = pending.Dequeue();
            if (!visited.Add(slot) || !index.TryGetValue((ISymbol?)slot.Method ?? slot.Type!, out List<IMethodSymbol>? methods))
                continue;
            foreach (IMethodSymbol method in methods)
                if (slot.Matches(method))
                    Collect(Substitute(compilation, slot.Payload, Map(slot, method)), closed, pending, direct: false);
        }

        GeneratorOptions options = GeneratorOptionsFactory.From(compilation);
        var roots = new SortedDictionary<string, MemberModel>(StringComparer.Ordinal);
        foreach (ITypeSymbol payload in closed)
        {
            if (ContainsTypeParameter(payload) || !Accessible(payload))
                continue;
            string spelling = payload.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            if (!roots.ContainsKey(spelling) && TypeModelFactory.TryCreateRoot(payload, compilation, options) is { } root)
                roots.Add(spelling, root);
        }
        return new RootMembers(roots.Values.ToArray());
    }

    /// <summary>Lists the method names from which a serializer call is reachable by name, as the original bound them.</summary>
    private static HashSet<string> RelevantNames(Compilation compilation, CancellationToken cancellationToken)
    {
        var names = new HashSet<string>(SerializerNames, StringComparer.Ordinal);
        var callers = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (SyntaxTree tree in compilation.SyntaxTrees)
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (InvocationExpressionSyntax call in tree.GetRoot(cancellationToken).DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                string? owner = call.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault()?.Identifier.ValueText;
                string? target = InvocationName(call);
                if (owner == null || target == null)
                    continue;
                if (!callers.TryGetValue(target, out HashSet<string>? incoming))
                    callers[target] = incoming = new HashSet<string>(StringComparer.Ordinal);
                incoming.Add(owner);
            }
        }
        var pending = new Queue<string>(names);
        while (pending.Count != 0)
            if (callers.TryGetValue(pending.Dequeue(), out HashSet<string>? incoming))
                foreach (string owner in incoming)
                    if (names.Add(owner))
                        pending.Enqueue(owner);
        return names;
    }

    private static string? InvocationName(InvocationExpressionSyntax call) => call.Expression switch
    {
        MemberAccessExpressionSyntax access => access.Name.Identifier.ValueText,
        MemberBindingExpressionSyntax binding => binding.Name.Identifier.ValueText,
        SimpleNameSyntax simple => simple.Identifier.ValueText,
        _ => null,
    };

    /// <summary>Records a closed payload, or the source owners of its type parameters for substitution.</summary>
    private static void Collect(ITypeSymbol payload, HashSet<ITypeSymbol> closed, Queue<Slot> pending, bool direct)
    {
        var owners = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        foreach (ITypeParameterSymbol parameter in TypeParameters(payload))
            if (parameter.ContainingSymbol is IMethodSymbol { DeclaringSyntaxReferences.Length: > 0 } method)
                owners.Add(method.OriginalDefinition);
            else if (parameter.ContainingSymbol is INamedTypeSymbol { DeclaringSyntaxReferences.Length: > 0 } type)
                owners.Add(type.OriginalDefinition);
        if (owners.Count == 0)
        {
            // A direct closed facade call already binds its own root.
            if (!direct)
                closed.Add(payload);
            return;
        }
        foreach (ISymbol owner in owners)
            pending.Enqueue(new Slot(owner as IMethodSymbol, owner as INamedTypeSymbol, payload));
    }

    private static IEnumerable<ITypeParameterSymbol> TypeParameters(ITypeSymbol type)
    {
        switch (type)
        {
            case ITypeParameterSymbol parameter:
                yield return parameter;
                break;
            case IArrayTypeSymbol array:
                foreach (ITypeParameterSymbol parameter in TypeParameters(array.ElementType))
                    yield return parameter;
                break;
            case IPointerTypeSymbol pointer:
                foreach (ITypeParameterSymbol parameter in TypeParameters(pointer.PointedAtType))
                    yield return parameter;
                break;
            case INamedTypeSymbol named:
                foreach (ITypeSymbol argument in named.TypeArguments)
                    foreach (ITypeParameterSymbol parameter in TypeParameters(argument))
                        yield return parameter;
                if (named.ContainingType != null)
                    foreach (ITypeParameterSymbol parameter in TypeParameters(named.ContainingType))
                        yield return parameter;
                break;
        }
    }

    private static bool ContainsTypeParameter(ITypeSymbol type) => TypeParameters(type).Any();

    /// <summary>Whether generated code outside the declaring type can name the payload.</summary>
    private static bool Accessible(ITypeSymbol type)
    {
        switch (type)
        {
            case IArrayTypeSymbol array:
                return Accessible(array.ElementType);
            case IPointerTypeSymbol:
                return false;
            case INamedTypeSymbol named:
                for (INamedTypeSymbol? current = named; current != null; current = current.ContainingType)
                    if (current.DeclaredAccessibility is Accessibility.Private or Accessibility.Protected or Accessibility.ProtectedAndInternal)
                        return false;
                return named.TypeArguments.All(Accessible);
            default:
                return type.TypeKind is not (TypeKind.Error or TypeKind.Dynamic);
        }
    }

    private static Dictionary<ITypeParameterSymbol, ITypeSymbol> Map(Slot slot, IMethodSymbol invoked)
    {
        var map = new Dictionary<ITypeParameterSymbol, ITypeSymbol>(SymbolEqualityComparer.Default);
        if (slot.Method != null)
            for (int index = 0; index < slot.Method.TypeParameters.Length && index < invoked.TypeArguments.Length; index++)
                map[slot.Method.TypeParameters[index]] = invoked.TypeArguments[index];
        if (slot.Type != null && invoked.ContainingType != null)
            for (int index = 0; index < slot.Type.TypeParameters.Length && index < invoked.ContainingType.TypeArguments.Length; index++)
                map[slot.Type.TypeParameters[index]] = invoked.ContainingType.TypeArguments[index];
        return map;
    }

    private static ITypeSymbol Substitute(Compilation compilation, ITypeSymbol type, Dictionary<ITypeParameterSymbol, ITypeSymbol> map)
    {
        switch (type)
        {
            case ITypeParameterSymbol parameter:
                return map.TryGetValue(parameter, out ITypeSymbol? replacement) ? replacement : parameter;
            case IArrayTypeSymbol array:
                return compilation.CreateArrayTypeSymbol(Substitute(compilation, array.ElementType, map), array.Rank);
            case IPointerTypeSymbol pointer:
                return compilation.CreatePointerTypeSymbol(Substitute(compilation, pointer.PointedAtType, map));
            case INamedTypeSymbol named:
            {
                ITypeSymbol[] arguments = named.TypeArguments.Select(argument => Substitute(compilation, argument, map)).ToArray();
                if (named.ContainingType == null)
                    return arguments.Length == 0 || arguments.SequenceEqual(named.TypeArguments, SymbolEqualityComparer.Default)
                        ? named : named.OriginalDefinition.Construct(arguments);
                if (Substitute(compilation, named.ContainingType, map) is not INamedTypeSymbol containing)
                    return named;
                INamedTypeSymbol nested = containing.GetTypeMembers(named.Name, named.Arity).FirstOrDefault() ?? named.OriginalDefinition;
                return nested.TypeParameters.Length == 0 ? nested : nested.Construct(arguments);
            }
            default:
                return type;
        }
    }

    /// <summary>An open payload waiting for the constructions of the method or type that owns its type parameters.</summary>
    private readonly struct Slot : IEquatable<Slot>
    {
        internal readonly IMethodSymbol? Method;
        internal readonly INamedTypeSymbol? Type;
        internal readonly ITypeSymbol Payload;

        internal Slot(IMethodSymbol? method, INamedTypeSymbol? type, ITypeSymbol payload)
        {
            Method = method;
            Type = type;
            Payload = payload;
        }

        /// <summary>Whether an invocation constructs this slot's owner.</summary>
        internal bool Matches(IMethodSymbol invoked) => Method != null
            ? SymbolEqualityComparer.Default.Equals(invoked.OriginalDefinition, Method)
            : Type != null && SymbolEqualityComparer.Default.Equals(invoked.ContainingType.OriginalDefinition, Type);

        public bool Equals(Slot other) => SymbolEqualityComparer.Default.Equals(Method, other.Method) &&
            SymbolEqualityComparer.Default.Equals(Type, other.Type) && SymbolEqualityComparer.Default.Equals(Payload, other.Payload);

        public override bool Equals(object? obj) => obj is Slot other && Equals(other);

        public override int GetHashCode() => SymbolEqualityComparer.Default.GetHashCode(Payload);
    }
}

/// <summary>A value-equal list of closed root members.</summary>
internal sealed class RootMembers : IEquatable<RootMembers>
{
    internal static readonly RootMembers Empty = new(Array.Empty<MemberModel>());

    internal readonly MemberModel[] Members;

    internal RootMembers(MemberModel[] members) => Members = members;

    public bool Equals(RootMembers? other) => other != null && Members.SequenceEqual(other.Members);

    public override bool Equals(object? obj) => Equals(obj as RootMembers);

    public override int GetHashCode() => Members.Length;
}
