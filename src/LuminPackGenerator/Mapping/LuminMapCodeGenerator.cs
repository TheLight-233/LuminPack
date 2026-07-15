using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LuminPack.SourceGenerator;
using LuminPack.SourceGenerator.Mapping;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LuminPack.Code.Core;

// ═════════════════════════════════════════════════════════════════════════════
//  共享分析工具（两条路径复用）
// ═════════════════════════════════════════════════════════════════════════════

public static class LuminMapBindingAnalyzer
{
    private const string LuminMapIgnoreAttr     = "LuminPack.Attribute.LuminMapIgnoreAttribute";
    private const string LuminMapFromMemberAttr = "LuminPack.Attribute.LuminMapFromMemberAttribute";
    private const string LuminMapUsingAttr      = "LuminPack.Attribute.LuminMapUsingAttribute";
    private const string LuminMapConditionAttr  = "LuminPack.Attribute.LuminMapConditionAttribute";
    private const string LuminMapConstantAttr   = "LuminPack.Attribute.LuminMapConstantAttribute";

    public static List<LuminMapFieldBinding> BuildBindings(
        INamedTypeSymbol srcType,
        INamedTypeSymbol destType)
    {
        var bindings   = new List<LuminMapFieldBinding>();
        var srcMembers = CollectReadableMembers(srcType);

        foreach (var (destMember, isProperty) in CollectWritableMembers(destType))
        {
            var b     = new LuminMapFieldBinding
            {
                DestMemberName = destMember.Name,
                DestIsProperty = isProperty,
            };
            var attrs = destMember.GetAttributes();

            // ── 1. [LuminMapIgnore] ──────────────────────────────────────
            if (HasAttr(attrs, LuminMapIgnoreAttr))
            {
                b.IsIgnored        = true;
                b.SourceExpression = "/* ignored */";
                bindings.Add(b);
                continue;
            }

            // ── 2. [LuminMapConstant(value)] ─────────────────────────────
            var constAttr = GetAttr(attrs, LuminMapConstantAttr);
            if (constAttr != null)
            {
                b.SourceExpression  = RenderConstant(constAttr.ConstructorArguments.FirstOrDefault().Value);
                b.SourceMemberFound = true;
                ApplyCondition(b, attrs);
                bindings.Add(b);
                continue;
            }

            // ── 3. 确定源成员名（[LuminMapFromMember] 或同名）───────────
            var fromAttr   = GetAttr(attrs, LuminMapFromMemberAttr);
            string srcName = fromAttr?.ConstructorArguments.FirstOrDefault().Value as string
                             ?? destMember.Name;

            srcMembers.TryGetValue(srcName, out var srcMember);
            if (srcMember == null)
                srcMember = srcMembers.FirstOrDefault(
                    k => string.Equals(k.Key, srcName, StringComparison.OrdinalIgnoreCase)).Value;

            // ── 4. [LuminMapUsing(converterType, methodName)] ────────────
            //
            //  Bug fix: 若找不到源成员（通常是忘记写 [LuminMapFromMember]），
            //  不能生成 Method() 零参调用。改为 skip + 诊断注释，提示用户补全。
            //
            var usingAttr = GetAttr(attrs, LuminMapUsingAttr);
            if (usingAttr != null)
            {
                var converterType = usingAttr.ConstructorArguments[0].Value as INamedTypeSymbol;
                var methodName    = usingAttr.ConstructorArguments[1].Value as string;
                if (converterType != null && methodName != null)
                {
                    string fqn = converterType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    if (srcMember != null)
                    {
                        b.SourceExpression  = $"{fqn}.{methodName}(source.{srcMember.Name})";
                        b.SourceMemberFound = true;
                        ApplyCondition(b, attrs);
                    }
                    else
                    {
                        // 找不到源成员 → skip，给出可操作的诊断注释
                        b.SourceExpression  = $"default /* [LuminMapUsing] on '{destMember.Name}': " +
                                              $"no source member named '{srcName}'. " +
                                              $"Add [LuminMapFromMember(\"<sourceMemberName>\")] to fix. */";
                        b.SourceMemberFound = false;
                    }
                    bindings.Add(b);
                    continue;
                }
            }

            // ── 5. 普通同名映射 ──────────────────────────────────────────
            if (srcMember != null)
            {
                b.SourceExpression  = $"source.{srcMember.Name}";
                b.SourceMemberFound = true;
            }
            else
            {
                b.SourceExpression  = $"default /* no source member '{srcName}' */";
                b.SourceMemberFound = false;
            }

            ApplyCondition(b, attrs);
            bindings.Add(b);
        }

        return bindings;
    }

    // ── 收集源类型可读成员（含继承链）──────────────────────────────────────

    public static Dictionary<string, ISymbol> CollectReadableMembers(INamedTypeSymbol type)
    {
        var result  = new Dictionary<string, ISymbol>(StringComparer.Ordinal);
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

    // ── 收集目标类型可写成员（含继承链）────────────────────────────────────

    public static IEnumerable<(ISymbol member, bool isProperty)> CollectWritableMembers(INamedTypeSymbol type)
    {
        var seen    = new HashSet<string>(StringComparer.Ordinal);
        var result  = new List<(ISymbol, bool)>();
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

    public static bool HasDefaultCtor(INamedTypeSymbol type)
        => type.IsValueType
           || type.Constructors.Any(c =>
               c.Parameters.IsEmpty
               && c.DeclaredAccessibility == Accessibility.Public
               && !c.IsStatic);

    public static (CollectionMapKind kind, INamedTypeSymbol? elemType) DetectCollection(INamedTypeSymbol type)
    {
        if (!type.IsGenericType) return (CollectionMapKind.None, null);
        var def  = type.OriginalDefinition;
        var name = $"{def.ContainingNamespace}.{def.MetadataName}";
        if (name == "System.Collections.Generic.List`1" && type.TypeArguments.Length == 1)
            return (CollectionMapKind.List, type.TypeArguments[0] as INamedTypeSymbol);
        return (CollectionMapKind.None, null);
    }

    private static bool HasAttr(
        System.Collections.Immutable.ImmutableArray<AttributeData> attrs, string name)
        => attrs.Any(a => a.AttributeClass?.ToDisplayString() == name);

    private static AttributeData? GetAttr(
        System.Collections.Immutable.ImmutableArray<AttributeData> attrs, string name)
        => attrs.FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == name);

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

    private static string RenderConstant(object? v) => v switch
    {
        null     => "null",
        string s => $"\"{s.Replace("\"", "\\\"")}\"",
        bool b   => b ? "true" : "false",
        float f  => $"{f}f",
        double d => $"{d}d",
        _        => v.ToString() ?? "default",
    };
}

// ═════════════════════════════════════════════════════════════════════════════
//  ① 自动路径分析器（[LuminMapTo] → AutoMapSourceInfo）
// ═════════════════════════════════════════════════════════════════════════════

public static class LuminAutoMapAnalyzer
{
    private const string LuminMapToAttr = "LuminPack.Attribute.LuminMapToAttribute";

    public static AutoMapSourceInfo? Analyze(INamedTypeSymbol srcSymbol, Compilation compilation)
    {
        var mapToAttrs = srcSymbol.GetAttributes()
            .Where(a => a.AttributeClass?.ToDisplayString() == LuminMapToAttr)
            .ToList();
        if (mapToAttrs.Count == 0) return null;

        var info = new AutoMapSourceInfo
        {
            SourceClassName   = srcSymbol.Name,
            SourceNamespace   = srcSymbol.ContainingNamespace?.ToDisplayString() ?? "",
            SourceFullName    = srcSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            IsGlobalNamespace = srcSymbol.ContainingNamespace?.IsGlobalNamespace ?? true,
            IsUnityProject    = TypeMetaChecker.IsUnityProject(compilation),
        };

        foreach (var attr in mapToAttrs)
        {
            if (attr.ConstructorArguments.IsEmpty) continue;
            var destType = attr.ConstructorArguments[0].Value as INamedTypeSymbol;
            if (destType == null) continue;

            string? overrideSuffix = null;
            bool genCollections    = true;
            foreach (var na in attr.NamedArguments)
            {
                if (na.Key == "MethodSuffix")           overrideSuffix = na.Value.Value as string;
                if (na.Key == "GenerateCollectionMethods" && na.Value.Value is bool b) genCollections = b;
            }

            string suffix = overrideSuffix ?? InferSuffix(srcSymbol.Name, destType.Name);

            var pair = new AutoMapPairInfo
            {
                SourceTypeFullName        = srcSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                DestTypeFullName          = destType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                DestIsValueType           = destType.IsValueType,
                DestHasDefaultCtor        = LuminMapBindingAnalyzer.HasDefaultCtor(destType),
                MethodSuffix              = suffix,
                GenerateCollectionMethods = genCollections,
            };
            pair.Bindings.AddRange(LuminMapBindingAnalyzer.BuildBindings(srcSymbol, destType));
            info.Pairs.Add(pair);
        }

        return info;
    }

    // ── Bug fix: 按 PascalCase 单词边界推断后缀，不再按字符比较 ────────────
    //
    //   "PlayerData" → ["Player","Data"],  "PlayerDto" → ["Player","Dto"]
    //   公共前缀单词: ["Player"]，剩余 dest 单词: ["Dto"]，后缀 = "Dto" → "ToDto" ✓
    //
    //   旧逻辑（字符比较）: "PlayerD" 是最长公共字符串，后缀 = "to" → "Toto" ✗
    //
    private static string InferSuffix(string srcName, string destName)
    {
        var srcWords  = SplitPascalCase(srcName);
        var destWords = SplitPascalCase(destName);

        int common = 0;
        int limit  = Math.Min(srcWords.Count, destWords.Count);
        while (common < limit && srcWords[common] == destWords[common])
            common++;

        string suffix = string.Concat(destWords.Skip(common));
        // 若目标名与源名完全相同（suffix=""），或无法推断，直接用目标类型名
        return string.IsNullOrEmpty(suffix) ? destName : suffix;
    }

    private static List<string> SplitPascalCase(string name)
    {
        var words = new List<string>();
        int start = 0;
        for (int i = 1; i < name.Length; i++)
        {
            if (char.IsUpper(name[i]))
            {
                words.Add(name.Substring(start, i - start));
                start = i;
            }
        }
        if (start < name.Length)
            words.Add(name.Substring(start));
        return words;
    }
}

// ═════════════════════════════════════════════════════════════════════════════
//  ① 自动路径代码生成器
// ═════════════════════════════════════════════════════════════════════════════

public static class LuminAutoMapCodeGenerator
{
    public static string GenerateAutoMapperClass(AutoMapSourceInfo info, MetaInfo meta)
    {
        if (info.Pairs.Count == 0) return string.Empty;

        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("// LuminMapper — auto-generated from [LuminMapTo] attribute");
        sb.AppendLine($"// Source: {info.SourceFullName}");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("#pragma warning disable CS8600");
        sb.AppendLine("#pragma warning disable CS8601");
        sb.AppendLine("#pragma warning disable CS8618");
        sb.AppendLine();
        sb.AppendLine("using global::System;");
        sb.AppendLine("using global::System.Collections.Generic;");
        sb.AppendLine("using global::System.Runtime.CompilerServices;");
        sb.AppendLine("using global::LuminPack.Mapping;");
        sb.AppendLine();

        bool hasNs = !info.IsGlobalNamespace && !string.IsNullOrEmpty(info.SourceNamespace);
        if (hasNs) { sb.AppendLine($"namespace {info.SourceNamespace}"); sb.AppendLine("{"); }

        sb.AppendLine($"    /// <summary>Auto-generated mapper for <see cref=\"{info.SourceClassName}\"/>. Do not modify.</summary>");
        sb.AppendLine($"    public static class {info.SourceClassName}Mapper");
        sb.AppendLine("    {");

        foreach (var pair in info.Pairs)
            EmitPairMethods(sb, pair, meta);

        sb.AppendLine("    }");
        if (hasNs) sb.AppendLine("}");
        return sb.ToString();
    }

    private static void EmitPairMethods(StringBuilder sb, AutoMapPairInfo pair, MetaInfo meta)
    {
        string srcFqn = pair.SourceTypeFullName;
        string dstFqn = pair.DestTypeFullName;
        string suffix = pair.MethodSuffix;
        string srcIn  = meta.IsNet8 ? "scoped in " : "in ";

        // To{Suffix}
        sb.AppendLine("        [global::LuminPack.Attribute.Preserve]");
        sb.AppendLine("        [MethodImpl(MethodImplOptions.AggressiveInlining)]");
        sb.AppendLine($"        public static {dstFqn} To{suffix}({srcIn}{srcFqn} source)");
        sb.AppendLine("        {");
        EmitCtorLine(sb, pair.DestIsValueType, pair.DestHasDefaultCtor, dstFqn, "            ");
        EmitBindingLines(sb, pair.Bindings, "            ");
        sb.AppendLine("            return dest;");
        sb.AppendLine("        }");
        sb.AppendLine();

        // MapInto{Suffix}
        sb.AppendLine("        [global::LuminPack.Attribute.Preserve]");
        sb.AppendLine("        [MethodImpl(MethodImplOptions.AggressiveInlining)]");
        sb.AppendLine($"        public static void MapInto{suffix}({srcIn}{srcFqn} source, ref {dstFqn} dest)");
        sb.AppendLine("        {");
        EmitBindingLines(sb, pair.Bindings, "            ");
        sb.AppendLine("        }");
        sb.AppendLine();

        if (!pair.GenerateCollectionMethods) return;

        // To{Suffix}Array
        sb.AppendLine("        [global::LuminPack.Attribute.Preserve]");
        sb.AppendLine("        [MethodImpl(MethodImplOptions.AggressiveInlining)]");
        sb.AppendLine($"        public static {dstFqn}[] To{suffix}Array({srcFqn}[] sources)");
        sb.AppendLine("        {");
        sb.AppendLine($"            if (sources == null) return global::System.Array.Empty<{dstFqn}>();");
        sb.AppendLine($"            var result = new {dstFqn}[sources.Length];");
        sb.AppendLine("            for (int i = 0; i < sources.Length; i++)");
        sb.AppendLine($"                result[i] = To{suffix}(in sources[i]);");
        sb.AppendLine("            return result;");
        sb.AppendLine("        }");
        sb.AppendLine();

        // To{Suffix}List
        sb.AppendLine("        [global::LuminPack.Attribute.Preserve]");
        sb.AppendLine("        [MethodImpl(MethodImplOptions.AggressiveInlining)]");
        sb.AppendLine($"        public static global::System.Collections.Generic.List<{dstFqn}> To{suffix}List(");
        sb.AppendLine($"            global::System.Collections.Generic.List<{srcFqn}> sources)");
        sb.AppendLine("        {");
        sb.AppendLine($"            if (sources == null) return new global::System.Collections.Generic.List<{dstFqn}>(0);");
        sb.AppendLine($"            var result = new global::System.Collections.Generic.List<{dstFqn}>(sources.Count);");
        sb.AppendLine("            foreach (var item in sources)");
        sb.AppendLine($"                result.Add(To{suffix}(in item));");
        sb.AppendLine("            return result;");
        sb.AppendLine("        }");
        sb.AppendLine();
    }

    private static void EmitCtorLine(
        StringBuilder sb, bool isValue, bool hasCtor, string fqn, string indent)
    {
        if (isValue)      sb.AppendLine($"{indent}{fqn} dest = default;");
        else if (hasCtor) sb.AppendLine($"{indent}var dest = new {fqn}();");
        else              sb.AppendLine($"{indent}{fqn} dest = default!; // no public default ctor");
    }

    private static void EmitBindingLines(
        StringBuilder sb, List<LuminMapFieldBinding> bindings, string indent)
    {
        foreach (var b in bindings)
            EmitOneBinding(sb, b, "source", "dest", indent);
    }

    private static void EmitOneBinding(
        StringBuilder sb, LuminMapFieldBinding b,
        string srcVar, string dstVar, string indent)
    {
        if (b.IsIgnored)
        { sb.AppendLine($"{indent}// [LuminMapIgnore] {dstVar}.{b.DestMemberName} — skipped"); return; }

        if (!b.SourceMemberFound)
        { sb.AppendLine($"{indent}// {dstVar}.{b.DestMemberName} — {TrimDefaultComment(b.SourceExpression)}"); return; }

        string expr = b.SourceExpression.Replace("source.", srcVar + ".");
        if (b.ConditionExpression != null)
        {
            string cond = b.ConditionExpression.Replace("source", srcVar);
            sb.AppendLine($"{indent}if ({cond})");
            sb.AppendLine($"{indent}    {dstVar}.{b.DestMemberName} = {expr};");
        }
        else
        {
            sb.AppendLine($"{indent}{dstVar}.{b.DestMemberName} = {expr};");
        }
    }

    private static string TrimDefaultComment(string expr)
    {
        // expr 形如 "default /* ... */"，提取注释部分
        int start = expr.IndexOf("/*", StringComparison.Ordinal);
        return start >= 0 ? expr.Substring(start) : expr;
    }

    public static string GenerateAutoRegistrationLines(AutoMapSourceInfo info, string indent = "            ")
    {
        var sb      = new StringBuilder();
        string ns   = info.IsGlobalNamespace ? "" : info.SourceNamespace + ".";
        string mapperFqn = $"global::{ns}{info.SourceClassName}Mapper";

        foreach (var pair in info.Pairs)
        {
            string srcFqn = pair.SourceTypeFullName;
            string dstFqn = pair.DestTypeFullName;
            string suffix = pair.MethodSuffix;
            sb.AppendLine($"{indent}// {srcFqn} → {dstFqn}");
            sb.AppendLine($"{indent}global::LuminPack.Mapping.LuminMapper.Register<{srcFqn}, {dstFqn}>(");
            sb.AppendLine($"{indent}    static (in {srcFqn} s) => {mapperFqn}.To{suffix}(in s),");
            sb.AppendLine($"{indent}    static (in {srcFqn} s, ref {dstFqn} d) => {mapperFqn}.MapInto{suffix}(in s, ref d));");
        }
        return sb.ToString();
    }
}

// ═════════════════════════════════════════════════════════════════════════════
//  ② 手动路径分析器（[LuminMapper] partial class → LuminMapperClassInfo）
// ═════════════════════════════════════════════════════════════════════════════

public static class LuminMapAnalyzer
{
    public static LuminMapperClassInfo Analyze(
        INamedTypeSymbol mapperSymbol, Compilation compilation)
    {
        var info = new LuminMapperClassInfo
        {
            ClassName         = mapperSymbol.Name,
            ClassNamespace    = mapperSymbol.ContainingNamespace?.ToDisplayString() ?? "",
            ClassFullName     = mapperSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            IsGlobalNamespace = mapperSymbol.ContainingNamespace?.IsGlobalNamespace ?? true,
            IsUnityProject    = TypeMetaChecker.IsUnityProject(compilation),
        };

        foreach (var m in mapperSymbol.GetMembers().OfType<IMethodSymbol>())
        {
            if (!m.IsStatic || !m.IsPartialDefinition) continue;
            var mi = AnalyzeMethod(m);
            if (mi != null) info.Methods.Add(mi);
        }
        return info;
    }

    private static LuminMapMethodInfo? AnalyzeMethod(IMethodSymbol method)
    {
        bool sourceIsIn = method.Parameters.Length > 0
                          && method.Parameters[0].RefKind == RefKind.In;

        // 创建型：TDest Method([in] TSource source)
        if (method.ReturnType.SpecialType != SpecialType.System_Void
            && method.Parameters.Length == 1)
        {
            var srcType  = method.Parameters[0].Type as INamedTypeSymbol;
            var destType = method.ReturnType          as INamedTypeSymbol;
            if (srcType == null || destType == null) return null;

            // 集合映射
            var (dstColl, destElem) = LuminMapBindingAnalyzer.DetectCollection(destType);
            var (srcColl, srcElem)  = LuminMapBindingAnalyzer.DetectCollection(srcType);

            if (dstColl != CollectionMapKind.None && srcColl != CollectionMapKind.None
                && destElem != null && srcElem != null)
            {
                var elemMi = new LuminMapMethodInfo
                {
                    MethodName         = method.Name + "_elem",
                    SourceTypeFullName = srcElem.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    DestTypeFullName   = destElem.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    SourceIsIn         = false,
                    IsCreateNew        = true,
                    DestIsValueType    = destElem.IsValueType,
                    DestHasDefaultCtor = LuminMapBindingAnalyzer.HasDefaultCtor(destElem),
                };
                elemMi.Bindings.AddRange(LuminMapBindingAnalyzer.BuildBindings(srcElem, destElem));

                return new LuminMapMethodInfo
                {
                    MethodName         = method.Name,
                    SourceTypeFullName = srcType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    DestTypeFullName   = destType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    SourceIsIn         = sourceIsIn,   // 保留用户原签名的 in 修饰符
                    IsCreateNew        = true,
                    CollectionKind     = dstColl,
                    ElementMethod      = elemMi,
                };
            }

            // 普通映射
            var mi = new LuminMapMethodInfo
            {
                MethodName         = method.Name,
                SourceTypeFullName = srcType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                DestTypeFullName   = destType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                SourceIsIn         = sourceIsIn,
                IsCreateNew        = true,
                DestIsValueType    = destType.IsValueType,
                DestHasDefaultCtor = LuminMapBindingAnalyzer.HasDefaultCtor(destType),
            };
            mi.Bindings.AddRange(LuminMapBindingAnalyzer.BuildBindings(srcType, destType));
            return mi;
        }

        // 就地型：void Method([in] TSource source, ref TDest dest)
        if (method.ReturnType.SpecialType == SpecialType.System_Void
            && method.Parameters.Length == 2
            && method.Parameters[1].RefKind == RefKind.Ref)
        {
            var srcType  = method.Parameters[0].Type as INamedTypeSymbol;
            var destType = method.Parameters[1].Type as INamedTypeSymbol;
            if (srcType == null || destType == null) return null;

            var mi = new LuminMapMethodInfo
            {
                MethodName         = method.Name,
                SourceTypeFullName = srcType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                DestTypeFullName   = destType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                SourceIsIn         = sourceIsIn,
                IsCreateNew        = false,
                DestIsValueType    = destType.IsValueType,
                DestHasDefaultCtor = LuminMapBindingAnalyzer.HasDefaultCtor(destType),
            };
            mi.Bindings.AddRange(LuminMapBindingAnalyzer.BuildBindings(srcType, destType));
            return mi;
        }

        return null;
    }
}

// ═════════════════════════════════════════════════════════════════════════════
//  ② 手动路径代码生成器
// ═════════════════════════════════════════════════════════════════════════════

public static class LuminMapCodeGenerator
{
    public static string GenerateMapperImpl(LuminMapperClassInfo info, MetaInfo meta)
    {
        if (info.Methods.Count == 0) return string.Empty;

        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("// LuminMapper — generated from [LuminMapper] partial class");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("#pragma warning disable CS8600");
        sb.AppendLine("#pragma warning disable CS8601");
        sb.AppendLine("#pragma warning disable CS8618");
        sb.AppendLine("#pragma warning disable CS0162");
        sb.AppendLine();
        sb.AppendLine("using global::System;");
        sb.AppendLine("using global::System.Collections.Generic;");
        sb.AppendLine("using global::System.Runtime.CompilerServices;");
        sb.AppendLine("using global::LuminPack.Mapping;");
        sb.AppendLine();

        bool hasNs = !info.IsGlobalNamespace && !string.IsNullOrEmpty(info.ClassNamespace);
        if (hasNs) { sb.AppendLine($"namespace {info.ClassNamespace}"); sb.AppendLine("{"); }

        sb.AppendLine($"    public static partial class {info.ClassName}");
        sb.AppendLine("    {");

        foreach (var m in info.Methods)
        {
            if (m.CollectionKind != CollectionMapKind.None) EmitCollection(sb, m, meta);
            else if (m.IsCreateNew)                         EmitCreateNew(sb, m, meta);
            else                                            EmitMapInto(sb, m, meta);
            sb.AppendLine();
        }

        sb.AppendLine("    }");
        if (hasNs) sb.AppendLine("}");
        return sb.ToString();
    }

    // ── Bug fix: 使用 m.SourceIsIn 而非硬写 "in"/"scoped in"
    //   用户声明的 partial 方法签名决定是否有 in 修饰符，
    //   生成的实现签名必须与声明签名完全一致，否则编译器报
    //   "分部方法必须具有实现部分"。
    private static string SrcMod(LuminMapMethodInfo m, MetaInfo meta)
        => m.SourceIsIn ? (meta.IsNet8 ? "scoped in " : "in ") : "";

    private static void EmitCreateNew(StringBuilder sb, LuminMapMethodInfo m, MetaInfo meta)
    {
        sb.AppendLine("        [global::LuminPack.Attribute.Preserve]");
        sb.AppendLine("        [MethodImpl(MethodImplOptions.AggressiveInlining)]");
        sb.AppendLine($"        public static partial {m.DestTypeFullName} {m.MethodName}({SrcMod(m, meta)}{m.SourceTypeFullName} source)");
        sb.AppendLine("        {");
        EmitCtorLine(sb, m, "            ");
        EmitBindings(sb, m.Bindings, "            ");
        sb.AppendLine("            return dest;");
        sb.AppendLine("        }");
    }

    private static void EmitMapInto(StringBuilder sb, LuminMapMethodInfo m, MetaInfo meta)
    {
        sb.AppendLine("        [global::LuminPack.Attribute.Preserve]");
        sb.AppendLine("        [MethodImpl(MethodImplOptions.AggressiveInlining)]");
        sb.AppendLine($"        public static partial void {m.MethodName}({SrcMod(m, meta)}{m.SourceTypeFullName} source, ref {m.DestTypeFullName} dest)");
        sb.AppendLine("        {");
        EmitBindings(sb, m.Bindings, "            ");
        sb.AppendLine("        }");
    }

    private static void EmitCollection(StringBuilder sb, LuminMapMethodInfo m, MetaInfo meta)
    {
        var elem = m.ElementMethod!;
        // Bug fix: 集合方法同样使用 m.SourceIsIn，与用户声明签名保持一致
        sb.AppendLine("        [global::LuminPack.Attribute.Preserve]");
        sb.AppendLine("        [MethodImpl(MethodImplOptions.AggressiveInlining)]");
        sb.AppendLine($"        public static partial {m.DestTypeFullName} {m.MethodName}({SrcMod(m, meta)}{m.SourceTypeFullName} source)");
        sb.AppendLine("        {");

        if (m.CollectionKind == CollectionMapKind.Array)
        {
            sb.AppendLine("            if (source == null) return null!;");
            sb.AppendLine($"            var result = new {elem.DestTypeFullName}[source.Length];");
            sb.AppendLine("            for (int i = 0; i < source.Length; i++)");
            sb.AppendLine("            {");
            EmitCtorLine(sb, elem, "                ", "elem");
            foreach (var b in elem.Bindings)
                EmitOneBinding(sb, b, "source[i]", "elem", "                ");
            sb.AppendLine("                result[i] = elem;");
            sb.AppendLine("            }");
            sb.AppendLine("            return result;");
        }
        else // List
        {
            sb.AppendLine("            if (source == null) return null!;");
            sb.AppendLine($"            var result = new global::System.Collections.Generic.List<{elem.DestTypeFullName}>(source.Count);");
            sb.AppendLine("            foreach (var item in source)");
            sb.AppendLine("            {");
            EmitCtorLine(sb, elem, "                ", "elem");
            foreach (var b in elem.Bindings)
                EmitOneBinding(sb, b, "item", "elem", "                ");
            sb.AppendLine("                result.Add(elem);");
            sb.AppendLine("            }");
            sb.AppendLine("            return result;");
        }

        sb.AppendLine("        }");
    }

    private static void EmitCtorLine(
        StringBuilder sb, LuminMapMethodInfo m, string indent, string varName = "dest")
    {
        if (m.DestIsValueType)      sb.AppendLine($"{indent}{m.DestTypeFullName} {varName} = default;");
        else if (m.DestHasDefaultCtor) sb.AppendLine($"{indent}var {varName} = new {m.DestTypeFullName}();");
        else                        sb.AppendLine($"{indent}{m.DestTypeFullName} {varName} = default!;");
    }

    private static void EmitCtorLine(
        StringBuilder sb, AutoMapPairInfo pair, string dstFqn, string indent)
    {
        if (pair.DestIsValueType)      sb.AppendLine($"{indent}{dstFqn} dest = default;");
        else if (pair.DestHasDefaultCtor) sb.AppendLine($"{indent}var dest = new {dstFqn}();");
        else                           sb.AppendLine($"{indent}{dstFqn} dest = default!;");
    }

    private static void EmitBindings(
        StringBuilder sb, List<LuminMapFieldBinding> bindings, string indent)
    {
        foreach (var b in bindings)
            EmitOneBinding(sb, b, "source", "dest", indent);
    }

    private static void EmitOneBinding(
        StringBuilder sb, LuminMapFieldBinding b,
        string srcVar, string dstVar, string indent)
    {
        if (b.IsIgnored)
        { sb.AppendLine($"{indent}// [LuminMapIgnore] {dstVar}.{b.DestMemberName} — skipped"); return; }

        if (!b.SourceMemberFound)
        { sb.AppendLine($"{indent}// {dstVar}.{b.DestMemberName} — skipped, {ExtractComment(b.SourceExpression)}"); return; }

        string expr = b.SourceExpression.Replace("source.", srcVar + ".");
        if (b.ConditionExpression != null)
        {
            string cond = b.ConditionExpression.Replace("source", srcVar);
            sb.AppendLine($"{indent}if ({cond})");
            sb.AppendLine($"{indent}    {dstVar}.{b.DestMemberName} = {expr};");
        }
        else
        {
            sb.AppendLine($"{indent}{dstVar}.{b.DestMemberName} = {expr};");
        }
    }

    private static string ExtractComment(string expr)
    {
        int s = expr.IndexOf("/*", StringComparison.Ordinal);
        int e = expr.IndexOf("*/", StringComparison.Ordinal);
        if (s >= 0 && e > s) return expr.Substring(s + 2, e - s - 2).Trim();
        return expr;
    }

    public static string GenerateManualRegistrationLines(
        LuminMapperClassInfo info, string indent = "            ")
    {
        var sb     = new StringBuilder();
        string fqn = info.ClassFullName;

        var groups = new Dictionary<(string, string), (LuminMapMethodInfo? create, LuminMapMethodInfo? into)>();
        foreach (var m in info.Methods)
        {
            if (m.CollectionKind != CollectionMapKind.None) continue;
            var key = (m.SourceTypeFullName, m.DestTypeFullName);
            groups.TryGetValue(key, out var p);
            groups[key] = m.IsCreateNew ? (m, p.into) : (p.create, m);
        }

        foreach (var kvp in groups)
        {
            var (s, d)         = kvp.Key;
            var (create, into) = kvp.Value;
            // lambda 参数必须匹配委托签名（始终带 in），
            // 调用用户方法时按其声明签名决定是否传 in
            string callIn = (create ?? into)!.SourceIsIn ? "in " : "";
            if (create != null && into != null)
            {
                sb.AppendLine($"{indent}global::LuminPack.Mapping.LuminMapper.Register<{s}, {d}>(");
                sb.AppendLine($"{indent}    static (in {s} src) => {fqn}.{create.MethodName}({callIn}src),");
                sb.AppendLine($"{indent}    static (in {s} src, ref {d} dst) => {fqn}.{into.MethodName}({callIn}src, ref dst));");
            }
            else if (create != null)
            {
                sb.AppendLine($"{indent}global::LuminPack.Mapping.LuminMapper.RegisterMapOnly<{s}, {d}>(");
                sb.AppendLine($"{indent}    static (in {s} src) => {fqn}.{create.MethodName}({callIn}src));");
            }
        }
        return sb.ToString();
    }
}
