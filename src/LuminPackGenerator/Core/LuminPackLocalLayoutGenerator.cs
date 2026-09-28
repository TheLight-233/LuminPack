using System;
using System.Collections.Generic;
using System.Text;
using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;

namespace LuminPack.Code.Core;

/// <summary>
/// Emits the "Local" layout mirror classes that let the generated extension class reach
/// private/protected storage.
///
/// <para>
/// A Local is a type whose field sequence mirrors a layout type exactly: same field types in the same
/// declaration order, plus the same <c>[StructLayout]</c> kind. The generated code then reinterprets a
/// reference with <c>LuminPackMarshal.As&lt;Real, Local&gt;</c> and reads/writes the mirrored slots by
/// name. Because the mirror is a plain type, chaining works to any depth - each nesting level simply
/// reinterprets the previous level's slot - which is what makes deeply nested private members
/// serializable.
/// </para>
///
/// <para>
/// All Local classes for an assembly are emitted into one dedicated file so a Local declared for one
/// root type is also usable from every other generated file of the same assembly (they all share the
/// same <c>internal static partial</c> extension class).
/// </para>
/// </summary>
public static class LuminPackLocalLayoutGenerator
{
    /// <summary>
    /// Tracks which Local classes have already been emitted for the current assembly, so a type is
    /// mirrored at most once no matter how many roots reach it.
    /// </summary>
    public sealed class LocalLayoutRegistry
    {
        private readonly HashSet<string> _emitted = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>Registers a layout key; returns false when it was already emitted.</summary>
        public bool TryRegister(string key) => _emitted.Add(key);

        public int Count => _emitted.Count;
    }

    /// <summary>
    /// A root type needs a mirror when any generated path reads it through one. That is not only the
    /// private/protected storage case: the VersionTolerant and CircleReference layouts bind a mirror
    /// unconditionally at the root (they address every member through it), and the pooled JSON read
    /// path assigns every field through it as well.
    /// </summary>
    private static bool NeedsRootLocal(LuminDataInfo data)
    {
        if (data is null)
        {
            return false;
        }

        if (data.generatorType is GeneratorType.CircleReference or GeneratorType.VersionTolerant)
        {
            return true;
        }

        if (data.RentPoolMethod is not null)
        {
            return true;
        }

        IReadOnlyList<LuminDataField> fields = data.fields;
        if (fields is null)
        {
            return false;
        }

        for (int i = 0; i < fields.Count; i++)
        {
            if (fields[i].IsPrivate || fields[i].isProperty)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Nested members reach only private/protected slots through the mirror; a public member (including
    /// a public auto-property) is named directly, so it never justifies a Local.
    /// </summary>
    private static bool NeedsNestedLocal(IReadOnlyList<LuminDataField> fields)
    {
        if (fields is null)
        {
            return false;
        }

        for (int i = 0; i < fields.Count; i++)
        {
            if (fields[i].IsPrivate)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Local class name used when <em>referencing</em> the mirror (generic arguments preserved), matching
    /// <c>TypeMetaChecker.BuildLocalClassName</c> for root types.
    /// </summary>
    public static string GetLocalUsageName(ITypeSymbol type)
    {
        string fullName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        if (fullName.StartsWith("global::", StringComparison.Ordinal))
        {
            fullName = fullName.Substring(8);
        }

        return "Local" + fullName.Replace(".", "_").Replace('+', '_');
    }

    /// <summary>
    /// Local class name used when <em>declaring</em> the mirror. Nested mirrors are emitted as plain
    /// non-generic classes with a mangled name so a closed generic member type (<c>Foo&lt;int&gt;</c>)
    /// mirrors its substituted field sequence without needing the original constraints.
    /// </summary>
    public static string GetLocalDeclarationName(ITypeSymbol type)
    {
        return "Local" + MangleTypeName(type);
    }

    private static string MangleTypeName(ITypeSymbol type)
    {
        string fullName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        if (fullName.StartsWith("global::", StringComparison.Ordinal))
        {
            fullName = fullName.Substring(8);
        }

        var sb = new StringBuilder(fullName.Length + 8);
        foreach (char c in fullName)
        {
            sb.Append(c switch
            {
                '.' or '+' or '<' or '>' or ',' or '[' or ']' or ' ' or '?' or '*' => '_',
                _ => c,
            });
        }

        return sb.ToString();
    }

    /// <summary>One level of a mirrored class hierarchy: a named class plus the fields it declares itself.</summary>
    private sealed class LocalLevel
    {
        public string Name;
        public List<LuminLocalFieldData> OwnFields;

        /// <summary>
        /// Layout declared by this level's own type. The mirror has to carry the same kind as the real
        /// level: an <c>Explicit</c> or <c>Sequential</c> type laid out by the mirror as <c>Auto</c>
        /// lands its slots somewhere else entirely.
        /// </summary>
        public StructLayout Layout;

        public bool IsValueType;
    }

    /// <summary>
    /// Builds the mirrored class hierarchy for <paramref name="type"/>, base level first.
    ///
    /// <para>
    /// The mirror must reproduce the real type's inheritance, not merely its field list. A flat class
    /// listing every field is not layout-equivalent: the runtime lays out a base class's fields and
    /// then a derived class's fields as separate levels, and that arrangement does not match a single
    /// flat declaration - even with an explicit <c>LayoutKind.Sequential</c>. Emitting the same
    /// hierarchy keeps the runtime's own per-level algorithm in play, which is what makes the mirrored
    /// offsets line up.
    /// </para>
    ///
    /// <para>
    /// <paramref name="flattenedFields"/> arrives base-first (the member walk prepends base members),
    /// so each level's own fields are selected from it by the names that level declares.
    /// </para>
    /// </summary>
    private static List<LocalLevel> BuildLevels(
        INamedTypeSymbol type,
        IReadOnlyList<LuminLocalFieldData> flattenedFields,
        Func<ITypeSymbol, string> nameSelector)
    {
        var chain = new List<INamedTypeSymbol>();
        for (INamedTypeSymbol current = type;
             current is not null &&
                 current.SpecialType != SpecialType.System_Object &&
                 current.SpecialType != SpecialType.System_ValueType;
             current = current.BaseType)
        {
            chain.Add(current);
        }

        chain.Reverse();

        var levels = new List<LocalLevel>(chain.Count);

        // Tracked per slot rather than per name. Two levels can declare a same-named member (the member
        // walk reports that separately), and claiming by name would silently drop the second slot, which
        // shifts every later slot in the mirror.
        var claimed = new bool[flattenedFields.Count];

        for (int i = 0; i < chain.Count; i++)
        {
            var own = new List<LuminLocalFieldData>();
            HashSet<string> ownNames = i == chain.Count - 1 ? null : GetOwnStorageNames(chain[i]);

            for (int f = 0; f < flattenedFields.Count; f++)
            {
                if (claimed[f])
                {
                    continue;
                }

                // The most derived level owns whatever no earlier level identified, so a slot whose
                // owner cannot be determined is still mirrored instead of being dropped.
                if (ownNames is null || ownNames.Contains(flattenedFields[f].Name))
                {
                    claimed[f] = true;
                    own.Add(flattenedFields[f]);
                }
            }

            levels.Add(new LocalLevel
            {
                Name = nameSelector(chain[i]),
                OwnFields = own,
                Layout = TypeMetaChecker.GetStructLayout(chain[i]),
                IsValueType = chain[i].IsValueType,
            });
        }

        return levels;
    }

    /// <summary>Names of the storage slots (instance fields and auto-property backing fields) a level declares.</summary>
    private static HashSet<string> GetOwnStorageNames(INamedTypeSymbol type)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (ISymbol member in type.GetMembers())
        {
            if (member.IsStatic || member.IsImplicitlyDeclared)
            {
                continue;
            }

            if (member is IFieldSymbol)
            {
                names.Add(member.Name);
            }
            else if (member is IPropertySymbol property && LuminPackSourceGenerator.IsAutoProperty(property))
            {
                names.Add(member.Name);
            }
        }

        return names;
    }

    /// <summary>Emits every level of a mirrored hierarchy, each inheriting the level before it.</summary>
    private static void EmitLevels(
        StringBuilder sb,
        LocalLayoutRegistry registry,
        List<LocalLevel> levels,
        IReadOnlyList<string> genericParameters,
        IReadOnlyList<GenericParameterConstraint> genericConstraints)
    {
        string genericParameterList = BuildGenericParameterList(genericParameters);

        for (int i = 0; i < levels.Count; i++)
        {
            LocalLevel level = levels[i];
            // The type parameter list is part of the identity: the same type name is mirrored both open
            // (LocalFoo<T>) and closed (LocalFoo for Foo&lt;int&gt;), and those are different classes.
            if (!registry.TryRegister("local:" + level.Name + genericParameterList))
            {
                // Already emitted for another root; the inherited base still resolves by name.
                continue;
            }

            string baseList = i > 0 ? " : " + levels[i - 1].Name + genericParameterList : string.Empty;

            EmitLocalClass(
                sb,
                level.Name,
                genericParameterList,
                baseList,
                level.IsValueType,
                level.Layout,
                genericConstraints,
                level.OwnFields);
        }
    }

    /// <summary>
    /// Emits the Local mirror for a root <c>[LuminPackable]</c> type, including its inherited fields
    /// (already flattened into <see cref="LuminDataInfo.localFields"/> base-first, which is the CLR's
    /// own field order).
    /// </summary>
    public static bool AppendRootLocalClass(
        StringBuilder sb,
        LocalLayoutRegistry registry,
        LuminDataInfo data)
    {
        if (data?.TypeSymbol is null || !NeedsRootLocal(data))
        {
            return false;
        }

        // The most derived level is declared without a type parameter list here - EmitLevels appends the
        // shared generic list, which makes the declaration name equal what the root formatters
        // reference through TypeMetaChecker.BuildLocalClassName.
        string rootLocalName = TypeMetaChecker.BuildLocalMyClassName(data);

        List<LocalLevel> levels = BuildLevels(
            data.TypeSymbol,
            data.localFields,
            level => level.Equals(data.TypeSymbol, SymbolEqualityComparer.Default)
                ? rootLocalName
                : "Local" + MangleDeclarationName(level));

        if (levels.Count == 0 || levels[levels.Count - 1].Name != rootLocalName)
        {
            return false;
        }

        int before = registry.Count;
        EmitLevels(
            sb,
            registry,
            levels,
            data.GenericParameters,
            data.GenericConstraints);

        return registry.Count != before;
    }

    /// <summary>
    /// Name of a mirrored hierarchy level: the type name without generic arguments, which are appended
    /// separately as the declaration's own type parameter list.
    /// </summary>
    private static string MangleDeclarationName(ITypeSymbol type)
    {
        string mangled = MangleTypeName(type);
        string fullName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        if (fullName.StartsWith("global::", StringComparison.Ordinal))
        {
            fullName = fullName.Substring(8);
        }

        int open = fullName.IndexOf('<');
        if (open < 0)
        {
            return mangled;
        }

        // Re-mangle only the part before the generic argument list.
        string head = fullName.Substring(0, open);
        var sb2 = new StringBuilder(head.Length + 2);
        foreach (char c in head)
        {
            sb2.Append(c switch
            {
                '.' or '+' or '[' or ']' or ' ' or '?' or '*' => '_',
                _ => c,
            });
        }

        return sb2.ToString();
    }

    /// <summary>
    /// Emits Local mirrors for every nested member type reachable from <paramref name="fields"/> that
    /// actually owns private storage. Recurses through the member graph so any nesting depth is
    /// covered.
    /// </summary>
    public static void AppendNestedLocalClasses(
        StringBuilder sb,
        LocalLayoutRegistry registry,
        IReadOnlyList<LuminDataField> fields)
    {
        if (fields is null)
        {
            return;
        }

        for (int i = 0; i < fields.Count; i++)
        {
            LuminDataField field = fields[i];
            if (field.ClassFields is null || field.ClassFields.Count == 0)
            {
                continue;
            }

            if (field.TypeSymbol is INamedTypeSymbol nestedType && NeedsNestedLocal(field.ClassFields))
            {
                EmitLevels(
                    sb,
                    registry,
                    BuildLevels(nestedType, field.localFields, GetLocalDeclarationName),
                    null,
                    null);
            }

            AppendNestedLocalClasses(sb, registry, field.ClassFields);
        }
    }

    /// <summary>
    /// Emits the member accessors that hand out a <c>ref</c> to a mirrored private/protected slot.
    ///
    /// <para>
    /// These replace the previous <c>[UnsafeAccessor]</c> externs. An <c>UnsafeAccessor</c> call can
    /// only reach the member it names, so reaching a member of a member required nesting a second
    /// accessor inside the first - a form the runtime does not support and which faulted at runtime.
    /// Going through the Local mirror instead makes every level an independent reinterpretation, so the
    /// accessor never has to compose with another one and nesting depth stops mattering.
    /// </para>
    ///
    /// <para>
    /// The signature keeps the <c>(in T value)</c> shape the call sites already emit, so the accessor
    /// stays a drop-in replacement at every use site.
    /// </para>
    /// </summary>
    public static void AppendLocalMemberAccessors(
        StringBuilder sb,
        LocalLayoutRegistry registry,
        IReadOnlyList<LuminDataField> fields)
    {
        if (fields is null)
        {
            return;
        }

        for (int i = 0; i < fields.Count; i++)
        {
            LuminDataField field = fields[i];
            if (field.ClassFields is null || field.ClassFields.Count == 0)
            {
                continue;
            }

            // Every member of ClassFields is declared by the type of this field, so the mirror to reach
            // through is the one for that type. The parameter type is taken from the member itself, which
            // is what the pre-existing call sites already pass.
            string localName = field.TypeSymbol is not null
                ? GetLocalDeclarationName(field.TypeSymbol)
                : null;
            string fallbackDeclaringType = field.TypeSymbol?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

            if (localName is not null)
            {
                for (int m = 0; m < field.ClassFields.Count; m++)
                {
                    LuminDataField member = field.ClassFields[m];
                    if (!member.IsPrivate)
                    {
                        continue;
                    }

                    string declaringType = !string.IsNullOrEmpty(member.belongClassName)
                        ? member.belongClassName
                        : fallbackDeclaringType;

                    if (declaringType is null)
                    {
                        continue;
                    }

                    // Keyed on the emitted signature so one accessor per (declaring type, member) is
                    // produced no matter how many roots reach it.
                    if (!registry.TryRegister("acc:" + declaringType + "|" + member.Name))
                    {
                        continue;
                    }

                    sb.AppendLine("        [global::LuminPack.Attribute.Preserve]");
                    sb.AppendLine("        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
                    sb.AppendLine("        private static ref " + member.TypeName + " Get" + field.Identifier + member.Identifier + "(in " + declaringType + " value)");
                    sb.AppendLine("            => ref global::LuminPack.Code.LuminPackMarshal.As<" + declaringType + ", " + localName
                        + ">(ref global::System.Runtime.CompilerServices.Unsafe.AsRef(in value))." + member.Identifier + ";");
                    sb.AppendLine();
                }
            }

            AppendLocalMemberAccessors(sb, registry, field.ClassFields);
        }
    }

    private static string BuildGenericParameterList(IReadOnlyList<string> genericParameters)
    {
        return genericParameters is null || genericParameters.Count == 0
            ? string.Empty
            : "<" + string.Join(", ", genericParameters) + ">";
    }

    private static void EmitLocalClass(
        StringBuilder sb,
        string className,
        string genericParameterList,
        string baseList,
        bool isValueType,
        StructLayout structLayout,
        IReadOnlyList<GenericParameterConstraint> genericConstraints,
        IReadOnlyList<LuminLocalFieldData> fields)
    {
        switch (structLayout)
        {
            case StructLayout.Explicit:
                sb.AppendLine("        [global::System.Runtime.InteropServices.StructLayout(LayoutKind.Explicit)]");
                break;
            case StructLayout.Auto:
                sb.AppendLine("        [global::System.Runtime.InteropServices.StructLayout(LayoutKind.Auto)]");
                break;
            case StructLayout.Sequential:
                sb.AppendLine("        [global::System.Runtime.InteropServices.StructLayout(LayoutKind.Sequential)]");
                break;
        }

        sb.AppendLine("        [global::LuminPack.Attribute.Preserve]");
        sb.AppendLine(isValueType
            ? "        private struct " + className + genericParameterList + baseList
            : "        private class " + className + genericParameterList + baseList);

        AppendGenericConstraints(sb, genericConstraints);

        sb.AppendLine("        {");
        for (int i = 0; i < fields.Count; i++)
        {
            LuminLocalFieldData field = fields[i];
            if (structLayout == StructLayout.Explicit && field.filedOffset >= 0)
            {
                sb.AppendLine("            [global::System.Runtime.InteropServices.FieldOffset(" + field.filedOffset + ")]");
            }

            string nullable = LuminPackCodeGenerator.IsUnmanagedFiledType(field.TypeName) || field.IsValue
                ? string.Empty
                : "?";
            sb.AppendLine("            internal " + field.TypeName + nullable + " " + field.Identifier + ";");
        }

        sb.AppendLine("        }");
        sb.AppendLine();
    }

    private static void AppendGenericConstraints(
        StringBuilder sb,
        IReadOnlyList<GenericParameterConstraint> genericConstraints)
    {
        if (genericConstraints is null || genericConstraints.Count == 0)
        {
            return;
        }

        foreach (GenericParameterConstraint constraint in genericConstraints)
        {
            if (!constraint.IsUnmanaged && !constraint.IsClass && !constraint.IsStruct &&
                !constraint.IsNotNull && !constraint.HasDefault && !constraint.HasNewConstructor &&
                constraint.Constraints.Count == 0)
            {
                continue;
            }

            var parts = new List<string>();
            if (constraint.IsUnmanaged) parts.Add("unmanaged");
            if (constraint.IsClass) parts.Add("class");
            if (constraint.IsStruct) parts.Add("struct");
            if (constraint.IsNotNull) parts.Add("notnull");
            if (constraint.HasNewConstructor) parts.Add("new()");
            if (constraint.HasDefault) parts.Add("default");
            parts.AddRange(constraint.Constraints);

            sb.Append("            where ");
            sb.Append(constraint.ParameterName);
            sb.Append(" : ");
            string joined = string.Join(", ", parts);
            sb.Append(joined);
            sb.AppendLine();
        }
    }

    /// <summary>
    /// Wraps emitted Local classes in the shared assembly-wide extension class. Every generated file of
    /// an assembly declares the same <c>internal static partial</c> class, so a Local emitted here is
    /// visible to all of them.
    /// </summary>
    public static string Wrap(string body, string extensionClassName)
    {
        if (string.IsNullOrEmpty(body))
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("// LuminPack local layout mirrors - one dedicated file for the whole assembly.");
        sb.AppendLine("// These types mirror the exact field sequence of the types they are named after so the");
        sb.AppendLine("// generated formatters can reach private/protected storage through LuminPackMarshal.As.");
        sb.AppendLine("// Emitted only for types that actually own private storage; a Local is never produced for");
        sb.AppendLine("// a type whose serialized members are all publicly nameable.");
        sb.AppendLine("using global::System;");
        sb.AppendLine("using global::System.Collections.Generic;");
        sb.AppendLine("using global::System.Runtime.CompilerServices;");
        sb.AppendLine("using global::System.Runtime.InteropServices;");
        sb.AppendLine();
        sb.AppendLine("#nullable enable");
        sb.AppendLine("namespace LuminPack.Generated");
        sb.AppendLine("{");
        sb.AppendLine("    internal static partial class " + extensionClassName);
        sb.AppendLine("    {");
        sb.Append(body);
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }
}