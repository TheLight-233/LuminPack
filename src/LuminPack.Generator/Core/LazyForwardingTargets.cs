using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LuminPack.Code.Core;

/// <summary>Reads the execution roots that one contribution's state forwarders call.</summary>
/// <remarks>Lazy formatter entries call the declaration-default and explicit-profile state forwarders by name, which makes
/// C# overload resolution examine every payload's overloads. When the payload's contribution shows that a forwarder is
/// a single call to its nested root container, the entry calls that root directly: the same operation the forwarder
/// would run, bound against one payload. Entries of other payloads keep calling the forwarders.</remarks>
internal sealed class LazyForwardingTargets
{
    private static readonly ConditionalWeakTable<string, LazyForwardingTargets> Cache = new();
    private static readonly LazyForwardingTargets Empty = new(new Dictionary<string, Target>(StringComparer.Ordinal));
    private readonly Dictionary<string, Target> _targets;

    /// <summary>Gets a stable description of every recognized target, for cache identities.</summary>
    internal string Key { get; }

    /// <summary>Captures recognized targets.</summary>
    /// <param name="targets">The targets by operation name and profile form.</param>
    private LazyForwardingTargets(Dictionary<string, Target> targets)
    {
        _targets = targets;
        var key = new StringBuilder();
        foreach (var pair in targets.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            key.Append(pair.Key).Append('=').Append(pair.Value.Call).Append('|');
        Key = key.ToString();
    }

    /// <summary>Reads the targets of one payload's contribution once per contribution text.</summary>
    /// <param name="source">The projected contribution.</param>
    /// <param name="payload">The canonical formatter name of the payload.</param>
    /// <returns>The recognized targets, possibly none.</returns>
    internal static LazyForwardingTargets Read(string source, string payload)
    {
        if (string.IsNullOrEmpty(source) || source.IndexOf(RootContainerProjection.ContainerPrefix, StringComparison.Ordinal) < 0) return Empty;
        return Cache.GetValue(source, text => Parse(text, payload));
    }

    /// <summary>Finds single-call forwarders to nested roots of the payload.</summary>
    /// <param name="source">The projected contribution.</param>
    /// <param name="payload">The canonical formatter name of the payload.</param>
    /// <returns>The recognized targets.</returns>
    private static LazyForwardingTargets Parse(string source, string payload)
    {
        var targets = new Dictionary<string, Target>(StringComparer.Ordinal);
        string key = RootContainerProjection.PayloadKey(payload);
        var root = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview, DocumentationMode.None)).GetRoot();
        var serializers = root.DescendantNodes(node => node is not MethodDeclarationSyntax).OfType<ClassDeclarationSyntax>()
            .Where(type => type.Identifier.ValueText == "LuminPackSerializer");
        foreach (var method in serializers.SelectMany(type => type.Members).OfType<MethodDeclarationSyntax>())
        {
            string operation = method.Identifier.ValueText;
            if (operation is not ("Serialize" or "Deserialize" or "SerializeJson" or "DeserializeJson") ||
                method.ParameterList.Parameters.Count != 2 || method.TypeParameterList?.Parameters.Count > 1 ||
                RootContainerProjection.PayloadKey(method.ParameterList.Parameters[1].Type) != key ||
                method.Body?.Statements.Count != 1 || method.Body.Statements[0] is not ExpressionStatementSyntax { Expression: InvocationExpressionSyntax call } ||
                call.Expression is not MemberAccessExpressionSyntax { Expression: ExpressionSyntax container } access ||
                !(container.ToString().StartsWith(RootContainerProjection.ContainerPrefix, StringComparison.Ordinal) ||
                  container.ToString().StartsWith("global::LuminPack.LuminPackSerializer." + RootContainerProjection.ContainerPrefix, StringComparison.Ordinal)) ||
                !access.Name.Identifier.ValueText.StartsWith("Root", StringComparison.Ordinal) || call.ArgumentList.Arguments.Count != 2)
                continue;
            string state = method.ParameterList.Parameters[0].Identifier.ValueText;
            string value = method.ParameterList.Parameters[1].Identifier.ValueText;
            var arguments = call.ArgumentList.Arguments;
            if (arguments[0].Expression is not IdentifierNameSyntax first || first.Identifier.ValueText != state || !arguments[0].RefKindKeyword.IsKind(SyntaxKind.RefKeyword) ||
                arguments[1].Expression is not IdentifierNameSyntax second || second.Identifier.ValueText != value)
                continue;
            string profile = method.TypeParameterList?.Parameters[0].Identifier.ValueText;
            string target = access.WithoutTrivia().ToString();
            // Profile arguments are compiler-visible type names; only the method's own profile parameter is substituted.
            if (profile != null && access.Name is GenericNameSyntax generic)
                target = access.WithName(generic.WithTypeArgumentList(generic.TypeArgumentList.WithArguments(SyntaxFactory.SeparatedList(
                    generic.TypeArgumentList.Arguments.Select(argument => argument is IdentifierNameSyntax name && name.Identifier.ValueText == profile
                        ? (TypeSyntax)SyntaxFactory.IdentifierName("{profile}") : argument))))).WithoutTrivia().ToString();
            string passing = arguments[1].RefKindKeyword.IsKind(SyntaxKind.RefKeyword) ? "ref " : arguments[1].RefKindKeyword.IsKind(SyntaxKind.InKeyword) ? "in " : string.Empty;
            targets[operation + (profile == null ? string.Empty : "<>")] = new Target(target, passing);
        }
        return targets.Count == 0 ? Empty : new LazyForwardingTargets(targets);
    }

    /// <summary>Renders a direct root call for one lazy entry branch.</summary>
    /// <param name="operation">The forwarder name: Serialize, Deserialize, SerializeJson or DeserializeJson.</param>
    /// <param name="profile">The explicit profile argument, or null for the declaration-default branch.</param>
    /// <param name="state">The entry's state expression.</param>
    /// <param name="value">The entry's payload storage expression.</param>
    /// <returns>The call statement, or null when the forwarder was not recognized.</returns>
    internal string Call(string operation, string profile, string state, string value)
    {
        if (!_targets.TryGetValue(operation + (profile == null ? string.Empty : "<>"), out var target)) return null;
        return target.Call.Replace("{profile}", profile) + "(ref " + state + ", " + target.Passing + value + ");";
    }

    /// <summary>One forwarder's root and payload passing convention.</summary>
    private sealed class Target
    {
        internal readonly string Call;
        internal readonly string Passing;

        /// <summary>Captures one recognized root call.</summary>
        /// <param name="call">The qualified root, with <c>{profile}</c> marking the forwarder's profile argument.</param>
        /// <param name="passing">The payload argument's passing keyword and trailing space, or empty for by-value.</param>
        internal Target(string call, string passing) { Call = call; Passing = passing; }
    }
}
