using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LuminPack.Code.Core;

/// <summary>Places each payload's execution roots in a payload-specific container nested in the Serializer.</summary>
/// <remarks>Every closed payload declares roots with the same names, so a call to <c>RootWrite</c> on the Serializer
/// makes C# overload resolution examine one candidate per payload in the assembly. The forwarders and extension shells
/// calling a root live in the same contribution and know its payload, so they call the nested container directly and bind
/// against that payload's roots only. Root names, bodies, attributes and the forwarders' signatures are unchanged.</remarks>
internal static class RootContainerProjection
{
    private const string Serializer = "global::LuminPack.LuminPackSerializer";
    /// <summary>The name prefix of every payload root container.</summary>
    internal const string ContainerPrefix = "__LuminRoots_";

    private static readonly HashSet<string> Roots = new(StringComparer.Ordinal)
    {
        "RootWrite", "RootRead", "RootWriteUnion", "RootReadUnion", "RootWriteJson", "RootReadJson",
        "RootReadFresh", "RootWriteCompressed", "RootReadCompressed"
    };

    /// <summary>Moves the roots of one contribution and retargets their callers.</summary>
    /// <param name="root">The projected contribution after every name-based operation projection.</param>
    /// <returns>The contribution with nested root containers, or the unchanged contribution when a caller's payload is unknown.</returns>
    internal static SyntaxNode Apply(SyntaxNode root)
    {
        var serializers = root.DescendantNodes().OfType<ClassDeclarationSyntax>()
            .Where(type => type.Identifier.ValueText == "LuminPackSerializer" && type.Parent is BaseNamespaceDeclarationSyntax).ToArray();
        var containers = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var serializer in serializers)
            foreach (var method in serializer.Members.OfType<MethodDeclarationSyntax>())
                if (IsRoot(method)) containers[PayloadKey(method)] = string.Empty;
        if (containers.Count == 0) return root;
        foreach (string payload in containers.Keys.ToArray()) containers[payload] = ContainerPrefix + Hash(payload);

        // Every root call must name a payload whose roots move; otherwise the contribution keeps its original form.
        var calls = new Dictionary<InvocationExpressionSyntax, string>();
        foreach (var call in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            var name = call.Expression switch
            {
                SimpleNameSyntax simple => simple,
                MemberAccessExpressionSyntax { Expression: var owner, Name: var member } when owner.ToString() == Serializer => member,
                _ => null
            };
            if (name == null || !Roots.Contains(name.Identifier.ValueText)) continue;
            // A local function may forward to another payload's root, so the nearest declaration names the payload.
            var caller = call.Ancestors().FirstOrDefault(node => node is LocalFunctionStatementSyntax or BaseMethodDeclarationSyntax);
            var parameters = caller switch { LocalFunctionStatementSyntax local => local.ParameterList, MethodDeclarationSyntax method => method.ParameterList, _ => null };
            if (parameters == null || parameters.Parameters.Count != 2 || !containers.TryGetValue(PayloadKey(parameters.Parameters[1].Type), out string container))
                return root;
            calls.Add(call, container);
        }

        return root.ReplaceNodes(calls.Keys.Concat<SyntaxNode>(serializers), (original, rewritten) =>
        {
            if (rewritten is InvocationExpressionSyntax call)
            {
                var name = call.Expression as SimpleNameSyntax ?? ((MemberAccessExpressionSyntax)call.Expression).Name;
                // Members of the Serializer find its nested containers by simple name.
                bool nested = original.Ancestors().OfType<ClassDeclarationSyntax>().Any(type => type.Identifier.ValueText == "LuminPackSerializer");
                string container = calls[(InvocationExpressionSyntax)original];
                var target = SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.ParseExpression(nested ? container : Serializer + "." + container), name.WithoutTrivia());
                return call.WithExpression(target.WithTriviaFrom(call.Expression));
            }
            return Nest((ClassDeclarationSyntax)rewritten, containers);
        });
    }

    /// <summary>Moves the roots of one Serializer declaration into their payload containers.</summary>
    /// <param name="serializer">The Serializer declaration with retargeted calls.</param>
    /// <param name="containers">The container name of each payload.</param>
    /// <returns>The declaration with each container at the position of its first root.</returns>
    private static ClassDeclarationSyntax Nest(ClassDeclarationSyntax serializer, Dictionary<string, string> containers)
    {
        var groups = new Dictionary<string, List<MemberDeclarationSyntax>>(StringComparer.Ordinal);
        var members = new List<MemberDeclarationSyntax>();
        var positions = new List<(int Index, string Container)>();
        foreach (var member in serializer.Members)
        {
            if (member is not MethodDeclarationSyntax method || !IsRoot(method)) { members.Add(member); continue; }
            string container = containers[PayloadKey(method)];
            if (!groups.TryGetValue(container, out var group))
            {
                groups.Add(container, group = new List<MemberDeclarationSyntax>());
                positions.Add((members.Count, container));
                members.Add(null);
            }
            group.Add(method);
        }
        foreach (var (index, container) in positions)
            members[index] = SyntaxFactory.ClassDeclaration(container)
                .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.InternalKeyword), SyntaxFactory.Token(SyntaxKind.StaticKeyword), SyntaxFactory.Token(SyntaxKind.PartialKeyword)))
                .WithMembers(SyntaxFactory.List(groups[container]));
        return serializer.WithMembers(SyntaxFactory.List(members));
    }

    /// <summary>Identifies a complete execution root of a payload.</summary>
    /// <param name="method">A Serializer member.</param>
    /// <returns>Whether the member is a two-parameter root.</returns>
    private static bool IsRoot(MethodDeclarationSyntax method)
        => Roots.Contains(method.Identifier.ValueText) && method.ParameterList.Parameters.Count == 2 && method.Body != null;

    /// <summary>Compares payload spellings independently of qualification, nullable annotations and spacing.</summary>
    /// <param name="method">A root or a method forwarding to one.</param>
    /// <returns>The payload key.</returns>
    private static string PayloadKey(MethodDeclarationSyntax method) => PayloadKey(method.ParameterList.Parameters[1].Type);

    /// <summary>Compares payload spellings independently of qualification, nullable annotations and spacing.</summary>
    /// <param name="type">A payload parameter type.</param>
    /// <returns>The payload key.</returns>
    internal static string PayloadKey(TypeSyntax type) => PayloadKey(type.ToString());

    /// <summary>Compares payload spellings independently of qualification, nullable annotations and spacing.</summary>
    /// <param name="type">A payload type spelling.</param>
    /// <returns>The payload key.</returns>
    internal static string PayloadKey(string type) => type.Replace("global::", string.Empty).Replace("?", string.Empty).Replace(" ", string.Empty);

    /// <summary>Derives a stable container suffix from a payload key.</summary>
    /// <param name="payload">The payload key.</param>
    /// <returns>Sixteen hexadecimal digits of the key's 64-bit FNV-1a hash.</returns>
    internal static string Hash(string payload)
    {
        ulong hash = 14695981039346656037UL;
        foreach (char character in payload)
        {
            hash ^= character;
            hash *= 1099511628211UL;
        }
        return hash.ToString("x16");
    }
}
