using System;
using System.Collections.Generic;
using System.Linq;
using LuminPack.SourceGenerator.Mapping;
using Microsoft.CodeAnalysis;

namespace LuminPack.Code.Core;

// ═════════════════════════════════════════════════════════════════════════════
//  Analysis utilities shared by both mapping paths.
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>Provides lumin map binding analyzer functionality.</summary>
public static class LuminMapBindingAnalyzer
{
    private const string LuminMapIgnoreAttr = "LuminPack.Attribute.LuminMapIgnoreAttribute";
    private const string LuminMapFromMemberAttr = "LuminPack.Attribute.LuminMapFromMemberAttribute";
    private const string LuminMapUsingAttr = "LuminPack.Attribute.LuminMapUsingAttribute";
    private const string LuminMapConditionAttr = "LuminPack.Attribute.LuminMapConditionAttribute";
    private const string LuminMapConstantAttr = "LuminPack.Attribute.LuminMapConstantAttribute";

    /// <summary>Performs the build bindings step for the current operation.</summary>
    /// <param name="srcType">The src type used by this operation.</param>
    /// <param name="destType">The dest type used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    public static List<LuminMapFieldBinding> BuildBindings(
        INamedTypeSymbol srcType,
        INamedTypeSymbol destType)
    {
        var bindings = new List<LuminMapFieldBinding>();
        var srcMembers = CollectReadableMembers(srcType);

        foreach (var (destMember, isProperty) in CollectWritableMembers(destType))
        {
            var b = new LuminMapFieldBinding
            {
                DestMemberName = destMember.Name,
                DestIsProperty = isProperty,
            };
            var attrs = destMember.GetAttributes();

            // ── 1. [LuminMapIgnore] ──────────────────────────────────────
            if (HasAttr(attrs, LuminMapIgnoreAttr))
            {
                b.IsIgnored = true;
                b.SourceExpression = "/* ignored */";
                bindings.Add(b);
                continue;
            }

            // ── 2. [LuminMapConstant(value)] ─────────────────────────────
            var constAttr = GetAttr(attrs, LuminMapConstantAttr);
            if (constAttr != null)
            {
                b.SourceExpression = RenderConstant(constAttr.ConstructorArguments.FirstOrDefault().Value);
                b.SourceMemberFound = true;
                ApplyCondition(b, attrs);
                bindings.Add(b);
                continue;
            }

            // ── 3. Resolve the source name from LuminMapFromMember or the matching member name.───────────
            var fromAttr = GetAttr(attrs, LuminMapFromMemberAttr);
            string srcName = fromAttr?.ConstructorArguments.FirstOrDefault().Value as string
                             ?? destMember.Name;

            srcMembers.TryGetValue(srcName, out var srcMember);
            if (srcMember == null)
                srcMember = srcMembers.FirstOrDefault(
                    k => string.Equals(k.Key, srcName, StringComparison.OrdinalIgnoreCase)).Value;

            // ── 4. [LuminMapUsing(converterType, methodName)] ────────────
            //
            //  If the source member is missing, commonly because LuminMapFromMember was omitted,
            //  Skip an invalid parameterless Method() call and emit a diagnostic comment requesting the missing implementation.
            //
            var usingAttr = GetAttr(attrs, LuminMapUsingAttr);
            if (usingAttr != null)
            {
                var converterType = usingAttr.ConstructorArguments[0].Value as INamedTypeSymbol;
                var methodName = usingAttr.ConstructorArguments[1].Value as string;
                if (converterType != null && methodName != null)
                {
                    string fqn = converterType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    if (srcMember != null)
                    {
                        b.SourceExpression = $"{fqn}.{methodName}(source.{srcMember.Name})";
                        b.SourceMemberFound = true;
                        ApplyCondition(b, attrs);
                    }
                    else
                    {
                        // Skip missing source members and emit an actionable diagnostic comment.
                        b.SourceExpression = $"default /* [LuminMapUsing] on '{destMember.Name}': " +
                                              $"no source member named '{srcName}'. " +
                                              $"Add [LuminMapFromMember(\"<sourceMemberName>\")] to fix. */";
                        b.SourceMemberFound = false;
                    }
                    bindings.Add(b);
                    continue;
                }
            }

            // ── 5. Map members with matching names. ──────────────────────────────────────────
            if (srcMember != null)
            {
                b.SourceExpression = $"source.{srcMember.Name}";
                b.SourceMemberFound = true;
            }
            else
            {
                b.SourceExpression = $"default /* no source member '{srcName}' */";
                b.SourceMemberFound = false;
            }

            ApplyCondition(b, attrs);
            bindings.Add(b);
        }

        return bindings;
    }

    // ── Collect readable source members, including inherited members.──────────────────────────────────────

    /// <summary>Performs the collect readable members step for the current operation.</summary>
    /// <param name="type">The type or symbol to analyze.</param>
    /// <returns>The result of the operation.</returns>
    public static Dictionary<string, ISymbol> CollectReadableMembers(INamedTypeSymbol type)
    {
        var result = new Dictionary<string, ISymbol>(StringComparer.Ordinal);
        var current = type;
        while (current != null && current.SpecialType != SpecialType.System_Object)
        {
            foreach (var m in current.GetMembers())
            {
                if (m.IsStatic) continue;
                if (m is IPropertySymbol p
                    && p.DeclaredAccessibility == Accessibility.Public
                    && p.GetMethod != null
                    && !result.ContainsKey(p.Name))
                    result[p.Name] = p;
                else if (m is IFieldSymbol f
                    && f.DeclaredAccessibility == Accessibility.Public
                    && !f.IsImplicitlyDeclared
                    && !result.ContainsKey(f.Name))
                    result[f.Name] = f;
            }
            current = current.BaseType;
        }
        return result;
    }

    // ── Collect writable destination members, including inherited members.────────────────────────────────────

    /// <summary>Performs the collect writable members step for the current operation.</summary>
    /// <param name="type">The type or symbol to analyze.</param>
    /// <returns>The result of the operation.</returns>
    public static IEnumerable<(ISymbol member, bool isProperty)> CollectWritableMembers(INamedTypeSymbol type)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<(ISymbol, bool)>();
        var current = type;
        while (current != null && current.SpecialType != SpecialType.System_Object)
        {
            foreach (var m in current.GetMembers().OrderBy(
                x => x.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax()?.SpanStart ?? int.MaxValue))
            {
                if (m.IsStatic || !seen.Add(m.Name)) continue;
                if (m is IPropertySymbol p
                    && p.SetMethod != null
                    && p.DeclaredAccessibility == Accessibility.Public
                    && !p.IsReadOnly)
                    result.Add((p, true));
                else if (m is IFieldSymbol f
                    && !f.IsReadOnly && !f.IsConst
                    && !f.IsImplicitlyDeclared
                    && f.DeclaredAccessibility == Accessibility.Public)
                    result.Add((f, false));
            }
            current = current.BaseType;
        }
        return result;
    }

    /// <summary>Determines whether default ctor are present.</summary>
    /// <param name="type">The type or symbol to analyze.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    public static bool HasDefaultCtor(INamedTypeSymbol type)
        => type.IsValueType
           || type.Constructors.Any(c =>
               c.Parameters.IsEmpty
               && c.DeclaredAccessibility == Accessibility.Public
               && !c.IsStatic);

    /// <summary>Performs the detect collection step for the current operation.</summary>
    /// <param name="type">The type or symbol to analyze.</param>
    /// <returns>The result of the operation.</returns>
    public static (CollectionMapKind kind, INamedTypeSymbol? elemType) DetectCollection(INamedTypeSymbol type)
    {
        if (!type.IsGenericType) return (CollectionMapKind.None, null);
        var def = type.OriginalDefinition;
        var name = $"{def.ContainingNamespace}.{def.MetadataName}";
        if (name == "System.Collections.Generic.List`1" && type.TypeArguments.Length == 1)
            return (CollectionMapKind.List, type.TypeArguments[0] as INamedTypeSymbol);
        return (CollectionMapKind.None, null);
    }

    /// <summary>Determines whether attr are present.</summary>
    /// <param name="attrs">The attrs used by this operation.</param>
    /// <param name="name">The name used by this operation.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    private static bool HasAttr(
        System.Collections.Immutable.ImmutableArray<AttributeData> attrs, string name)
        => attrs.Any(a => a.AttributeClass?.ToDisplayString() == name);

    /// <summary>Obtains the attr for the supplied input.</summary>
    /// <param name="attrs">The attrs used by this operation.</param>
    /// <param name="name">The name used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    private static AttributeData? GetAttr(
        System.Collections.Immutable.ImmutableArray<AttributeData> attrs, string name)
        => attrs.FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == name);

    /// <summary>Performs the apply condition step for the current operation.</summary>
    /// <param name="b">The b used by this operation.</param>
    /// <param name="attrs">The attrs used by this operation.</param>
    private static void ApplyCondition(
        LuminMapFieldBinding b,
        System.Collections.Immutable.ImmutableArray<AttributeData> attrs)
    {
        var condAttr = GetAttr(attrs, LuminMapConditionAttr);
        if (condAttr == null) return;
        var t = condAttr.ConstructorArguments[0].Value as INamedTypeSymbol;
        var m = condAttr.ConstructorArguments[1].Value as string;
        if (t != null && m != null)
            b.ConditionExpression =
                $"{t.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}.{m}(in source)";
    }

    /// <summary>Performs the render constant step for the current operation.</summary>
    /// <param name="v">The v used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    private static string RenderConstant(object? v) => v switch
    {
        null => "null",
        string s => $"\"{s.Replace("\"", "\\\"")}\"",
        bool b => b ? "true" : "false",
        float f => $"{f}f",
        double d => $"{d}d",
        _ => v.ToString() ?? "default",
    };
}
