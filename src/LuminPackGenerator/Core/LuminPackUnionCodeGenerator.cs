using System.Linq;
using System.Text;
using LuminPack.SourceGenerator;

namespace LuminPack.Code.Core;

/// <summary>
/// Emits the union root operations. Member dispatch itself is generated as partial virtual
/// slots by <see cref="LuminPackUnionDispatchCodeGenerator"/>; no parser or runtime registry
/// participates in the normal path.
/// </summary>
public static class LuminPackUnionCodeGenerator
{
    /// <summary>
    /// Emits the static MethodTable→slot map and resolver for a .NET 11 union struct.  The runtime
    /// case type is read from <c>object Value</c>; resolving it through the map (like the generic
    /// union's <c>UnionMap + switch</c>) is O(1) instead of a linear <c>is</c>-pattern chain.
    /// For a generic union a nested generic static cache class holds one map per closed
    /// instantiation (e.g. <c>CaseMap&lt;UnionCat&gt;</c>), so <c>typeof(Some&lt;T&gt;)</c> resolves
    /// to the concrete closed case type without enumerating project usage.
    /// </summary>
    public static void GenerateCaseMapStruct(LuminDataInfo data, StringBuilder sb)
    {
        string suffix = LuminPackUnionDispatchCodeGenerator.GetSlotSuffix(data.TypeSymbol.OriginalDefinition);

        if (!data.isGeneric)
        {
            sb.AppendLine("        [global::LuminPack.Attribute.Preserve]");
            sb.AppendLine("        [global::System.Runtime.CompilerServices.MethodImpl(MethodImplOptions.NoInlining)]");
            sb.AppendLine("        private static global::LuminPack.Utility.LuminUnionMap<int> BuildCaseMap_" + suffix + "()");
            sb.AppendLine("        {");
            sb.AppendLine("            var map = new global::LuminPack.Utility.LuminUnionMap<int>(" + data.UnionMembers.Count + ");");
            foreach (LuminUnionMemberInfo member in data.UnionMembers)
            {
                string memberType = GetMemberType(data, member);
                sb.AppendLine($"            map.TryRegister(global::LuminPack.Code.LuminPackMarshal.GetMethodTable(typeof({memberType})), {member.Id});");
            }
            sb.AppendLine("            return map;");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        private static readonly global::LuminPack.Utility.LuminUnionMap<int> s_caseMap_" + suffix + " = BuildCaseMap_" + suffix + "();");
            sb.AppendLine();
            sb.AppendLine("        [global::System.Runtime.CompilerServices.MethodImpl(MethodImplOptions.AggressiveInlining)]");
            sb.AppendLine("        private static int ResolveCase_" + suffix + "(object value)");
            sb.AppendLine("            => s_caseMap_" + suffix + ".TryGetValue(global::LuminPack.Code.LuminPackMarshal.GetMethodTable(value.GetType()), out var slot) ? slot : -1;");
            sb.AppendLine();
            return;
        }

        sb.AppendLine("        private static class CaseMap_" + suffix + "<T>");
        LuminPackExtensionGenerator.AppendGenericConstraints(sb, data);
        sb.AppendLine("        {");
        sb.AppendLine("            public static readonly global::LuminPack.Utility.LuminUnionMap<int> Map = Build();");
        sb.AppendLine();
        sb.AppendLine("            [global::LuminPack.Attribute.Preserve]");
        sb.AppendLine("            [global::System.Runtime.CompilerServices.MethodImpl(MethodImplOptions.NoInlining)]");
        sb.AppendLine("            private static global::LuminPack.Utility.LuminUnionMap<int> Build()");
        sb.AppendLine("            {");
        sb.AppendLine("                var map = new global::LuminPack.Utility.LuminUnionMap<int>(" + data.UnionMembers.Count + ");");
        foreach (LuminUnionMemberInfo member in data.UnionMembers)
        {
            string memberType = GetMemberType(data, member);
            sb.AppendLine($"                map.TryRegister(global::LuminPack.Code.LuminPackMarshal.GetMethodTable(typeof({memberType})), {member.Id});");
        }
        sb.AppendLine("                return map;");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        [global::System.Runtime.CompilerServices.MethodImpl(MethodImplOptions.AggressiveInlining)]");
        sb.AppendLine("        private static int ResolveCase_" + suffix + "<T>(object value)");
        LuminPackExtensionGenerator.AppendGenericConstraints(sb, data);
        sb.AppendLine("            => CaseMap_" + suffix + "<T>.Map.TryGetValue(global::LuminPack.Code.LuminPackMarshal.GetMethodTable(value.GetType()), out var slot) ? slot : -1;");
        sb.AppendLine();
    }

    /// <summary>
    /// Binary serialize for a .NET 11 union struct.  Unlike class/interface unions (which dispatch
    /// through partial virtual slots), a union struct carries its case payload in <c>object Value</c>.
    /// For non-generic unions the runtime case type is resolved through the MethodTable map (O(1),
    /// like the generic union's UnionMap+switch), with an <c>is</c>-pattern fallback that also matches
    /// case-type subtypes.  Generic unions fall back to the linear <c>is</c> chain because a static
    /// map cannot name the open type parameter.
    /// </summary>
    public static void GenerateSerializeCodeStruct(LuminDataInfo data, StringBuilder sb)
    {
        string suffix = LuminPackUnionDispatchCodeGenerator.GetSlotSuffix(data.TypeSymbol.OriginalDefinition);
        ushort maxTag = data.UnionMembers.Count == 0 ? (ushort)0 : data.UnionMembers.Max(static member => member.Id);
        bool wide = maxTag >= 250 || data.IsWideTag;

        sb.AppendLine("            if (value.Value is null)");
        sb.AppendLine("            {");
        sb.AppendLine("                ref int offset = ref writer.GetCurrentSpanOffset();");
        sb.AppendLine("                writer.WriteNullUnionHeader(ref offset);");
        sb.AppendLine("                offset += 1;");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();

        if (!data.isGeneric)
        {
            sb.AppendLine("            switch (ResolveCase_" + suffix + "(value.Value))");
        }
        else
        {
            sb.AppendLine("            switch (ResolveCase_" + suffix + "<" + string.Join(", ", data.GenericParameters) + ">(value.Value))");
        }
        sb.AppendLine("            {");
        foreach (LuminUnionMemberInfo member in data.UnionMembers)
        {
            string memberType = GetMemberType(data, member);
            sb.AppendLine($"                case {member.Id}:");
            sb.AppendLine("                {");
            sb.AppendLine(wide
                ? $"                    writer.WriteWideUnionHeader({member.Id});"
                : $"                    writer.WriteUnionHeader({member.Id});");
            sb.AppendLine($"                    var caseValue{member.Id} = ({memberType})value.Value;");
            sb.AppendLine($"                    writer.WriteValue(in caseValue{member.Id});");
            sb.AppendLine("                    return;");
            sb.AppendLine("                }");
        }
        sb.AppendLine("                default:");
        sb.AppendLine("                {");
        AppendCaseFallbackSerialize(data, sb, wide);
        sb.AppendLine("                    global::LuminPack.Code.LuminPackExceptionHelper.ThrowNotFoundInUnionType(value.Value.GetType(), typeof(" + data.classFullName + "));");
        sb.AppendLine("                    return;");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
    }

    private static void AppendCaseFallbackSerialize(LuminDataInfo data, StringBuilder sb, bool wide)
    {
        foreach (LuminUnionMemberInfo member in data.UnionMembers)
        {
            string memberType = GetMemberType(data, member);
            sb.AppendLine($"                if (value.Value is {memberType} member{member.Id})");
            sb.AppendLine("                {");
            sb.AppendLine(wide
                ? $"                    writer.WriteWideUnionHeader({member.Id});"
                : $"                    writer.WriteUnionHeader({member.Id});");
            sb.AppendLine($"                    writer.WriteValue(in member{member.Id});");
            sb.AppendLine("                    return;");
            sb.AppendLine("                }");
        }
    }

    /// <summary>Binary deserialize for a .NET 11 union struct.</summary>
    public static void GenerateDeserializeCodeStruct(LuminDataInfo data, StringBuilder sb)
    {
        ushort maxTag = data.UnionMembers.Count == 0 ? (ushort)0 : data.UnionMembers.Max(static member => member.Id);
        bool wide = maxTag >= 250 || data.IsWideTag;

        sb.AppendLine(wide
            ? "            if (!reader.TryPeekWideUnionHeader(out var tag))"
            : "            if (!reader.TryPeekUnionHeader(out var tag))");
        sb.AppendLine("            {");
        sb.AppendLine("                value = default;");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            switch (tag)");
        sb.AppendLine("            {");
        foreach (LuminUnionMemberInfo member in data.UnionMembers)
        {
            string memberType = GetMemberType(data, member);
            sb.AppendLine($"                case {member.Id}:");
            sb.AppendLine("                {");
            sb.AppendLine($"                    {memberType} member = default!;");
            // The case value is always written with the concrete WriteValue (not the polymorphism
            // header), so read it back with the matching concrete ReadValue.  This works for both
            // packable class cases and built-in value-type cases.
            sb.AppendLine("                    reader.ReadValue(ref member);");
            sb.AppendLine($"                    value = new {data.classFullName}(member);");
            sb.AppendLine("                    break;");
            sb.AppendLine("                }");
        }
        sb.AppendLine("                default:");
        sb.AppendLine($"                    global::LuminPack.Code.LuminPackExceptionHelper.ThrowNotFoundInUnionType(tag, typeof({data.classFullName}));");
        sb.AppendLine("                    break;");
        sb.AppendLine("            }");
    }

    /// <summary>Size calculation for a .NET 11 union struct.</summary>
    public static void GenerateCalculateOffsetCodeStruct(
        LuminDataInfo data,
        StringBuilder sb,
        string classGlobalName)
    {
        sb.AppendLine("            if (value.Value is null)");
        sb.AppendLine("            {");
        sb.AppendLine("                evaluator += 1;");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();

        string suffix = LuminPackUnionDispatchCodeGenerator.GetSlotSuffix(data.TypeSymbol.OriginalDefinition);
        if (!data.isGeneric)
        {
            sb.AppendLine("            switch (ResolveCase_" + suffix + "(value.Value))");
        }
        else
        {
            sb.AppendLine("            switch (ResolveCase_" + suffix + "<" + string.Join(", ", data.GenericParameters) + ">(value.Value))");
        }
        sb.AppendLine("            {");
        foreach (LuminUnionMemberInfo member in data.UnionMembers)
        {
            string memberType = GetMemberType(data, member);
            sb.AppendLine($"                case {member.Id}:");
            sb.AppendLine("                {");
            sb.AppendLine($"                    evaluator.CalculateUnionHeader({member.Id});");
            sb.AppendLine($"                    var typed{member.Id} = ({memberType})value.Value;");
            sb.AppendLine($"                    evaluator.CalculateOffset(ref typed{member.Id});");
            sb.AppendLine("                    return;");
            sb.AppendLine("                }");
        }
        sb.AppendLine("                default:");
        sb.AppendLine("                {");
        foreach (LuminUnionMemberInfo member in data.UnionMembers)
        {
            string memberType = GetMemberType(data, member);
            sb.AppendLine($"                    if (value.Value is {memberType} member{member.Id})");
            sb.AppendLine("                    {");
            sb.AppendLine($"                        evaluator.CalculateUnionHeader({member.Id});");
            sb.AppendLine($"                        evaluator.CalculateOffset(ref member{member.Id});");
            sb.AppendLine("                        return;");
            sb.AppendLine("                    }");
        }
        sb.AppendLine("                    global::LuminPack.Code.LuminPackExceptionHelper.ThrowNotFoundInUnionType(value.Value.GetType(), typeof(" + classGlobalName + "));");
        sb.AppendLine("                    return;");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
    }

    public static void GenerateSerializeJsonCodeStruct(
        LuminDataInfo data,
        StringBuilder sb,
        string classGlobalName)
    {
        sb.AppendLine("            if (value.Value is null)");
        sb.AppendLine("            {");
        sb.AppendLine("                writer.WriteNull();");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();

        string suffix = LuminPackUnionDispatchCodeGenerator.GetSlotSuffix(data.TypeSymbol.OriginalDefinition);
        if (!data.isGeneric)
        {
            sb.AppendLine("            switch (ResolveCase_" + suffix + "(value.Value))");
        }
        else
        {
            sb.AppendLine("            switch (ResolveCase_" + suffix + "<" + string.Join(", ", data.GenericParameters) + ">(value.Value))");
        }
        sb.AppendLine("            {");
        foreach (LuminUnionMemberInfo member in data.UnionMembers)
        {
            string memberType = GetMemberType(data, member);
            sb.AppendLine($"                case {member.Id}:");
            sb.AppendLine("                {");
            sb.AppendLine("                    writer.WriteObjectStart();");
            sb.AppendLine("                    if (writer.Option.StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8)");
            sb.AppendLine("                        writer.WritePropertyName(global::LuminPack.LuminPackConstUtf8.TypeU8);");
            sb.AppendLine("                    else");
            sb.AppendLine("                        writer.WritePropertyName(global::LuminPack.LuminPackConstUtf8.TypeU16);");
            sb.AppendLine($"                    writer.WriteInt({member.Id});");
            sb.AppendLine("                    if (writer.Option.StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8)");
            sb.AppendLine("                        writer.WritePropertyName(global::LuminPack.LuminPackConstUtf8.ValueU8);");
            sb.AppendLine("                    else");
            sb.AppendLine("                        writer.WritePropertyName(global::LuminPack.LuminPackConstUtf8.ValueU16);");
            sb.AppendLine($"                    var caseValue{member.Id} = ({memberType})value.Value;");
            sb.AppendLine($"                    writer.WriteValue(in caseValue{member.Id});");
            sb.AppendLine("                    writer.WriteObjectEnd();");
            sb.AppendLine("                    return;");
            sb.AppendLine("                }");
        }
        sb.AppendLine("                default:");
        sb.AppendLine("                {");
        foreach (LuminUnionMemberInfo member in data.UnionMembers)
        {
            string memberType = GetMemberType(data, member);
            sb.AppendLine($"                    if (value.Value is {memberType} member{member.Id})");
            sb.AppendLine("                    {");
            sb.AppendLine("                        writer.WriteObjectStart();");
            sb.AppendLine("                        if (writer.Option.StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8)");
            sb.AppendLine("                            writer.WritePropertyName(global::LuminPack.LuminPackConstUtf8.TypeU8);");
            sb.AppendLine("                        else");
            sb.AppendLine("                            writer.WritePropertyName(global::LuminPack.LuminPackConstUtf8.TypeU16);");
            sb.AppendLine($"                        writer.WriteInt({member.Id});");
            sb.AppendLine("                        if (writer.Option.StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8)");
            sb.AppendLine("                            writer.WritePropertyName(global::LuminPack.LuminPackConstUtf8.ValueU8);");
            sb.AppendLine("                        else");
            sb.AppendLine("                            writer.WritePropertyName(global::LuminPack.LuminPackConstUtf8.ValueU16);");
            sb.AppendLine($"                        writer.WriteValue(in member{member.Id});");
            sb.AppendLine("                        writer.WriteObjectEnd();");
            sb.AppendLine("                        return;");
            sb.AppendLine("                    }");
        }
        sb.AppendLine("                    global::LuminPack.Code.LuminPackExceptionHelper.ThrowNotFoundInUnionType(value.Value.GetType(), typeof(" + classGlobalName + "));");
        sb.AppendLine("                    return;");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
    }

    public static void GenerateSerializeCode(LuminDataInfo data, StringBuilder sb)
    {
        string classGlobalName = data.classFullName;

        foreach (var item in data.callBackMethods.Where(
                     static callback => callback.Item2 is SerializeCallBackType.OnSerializing))
        {
            sb.AppendLine(item.Item3
                ? $"            {classGlobalName}.{item.Item1}();"
                : $"            value?.{item.Item1}();");
        }

        if (!data.isValueType)
        {
            sb.AppendLine("            if (value is null)");
            sb.AppendLine("            {");
            sb.AppendLine("                ref int offset = ref writer.GetCurrentSpanOffset();");
            sb.AppendLine("                writer.WriteNullUnionHeader(ref offset);");
            sb.AppendLine("                offset += 1;");
            sb.AppendLine("                return;");
            sb.AppendLine("            }");
        }

        sb.AppendLine();
        string suffix = LuminPackUnionDispatchCodeGenerator.GetSlotSuffix(data.TypeSymbol.OriginalDefinition);
        sb.AppendLine($"            value.__LuminPackUnionSerialize_{suffix}(ref writer);");
        sb.AppendLine();

        foreach (var item in data.callBackMethods.Where(
                     static callback => callback.Item2 is SerializeCallBackType.OnSerialized))
        {
            sb.AppendLine(item.Item3
                ? $"            {classGlobalName}.{item.Item1}();"
                : $"            value?.{item.Item1}();");
        }
    }

    public static void GenerateDeserializeCode(LuminDataInfo data, StringBuilder sb)
    {
        string classGlobalName = data.classFullName;
        ushort maxTag = data.UnionMembers.Count == 0 ? (ushort)0 : data.UnionMembers.Max(static member => member.Id);

        foreach (var item in data.callBackMethods.Where(
                     static callback => callback.Item2 is SerializeCallBackType.OnDeserializing))
        {
            sb.AppendLine(item.Item3
                ? $"            {classGlobalName}.{item.Item1}();"
                : $"            value?.{item.Item1}();");
        }

        sb.AppendLine(maxTag < 250 && !data.IsWideTag
            ? "            if (!reader.TryPeekUnionHeader(out var tag))"
            : "            if (!reader.TryPeekWideUnionHeader(out var tag))");
        sb.AppendLine("            {");
        sb.AppendLine("                value = default;");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            switch (tag)");
        sb.AppendLine("            {");
        foreach (LuminUnionMemberInfo member in data.UnionMembers)
        {
            string memberType = GetMemberType(data, member);
            sb.AppendLine($"                case {member.Id}:");
            sb.AppendLine("                {");
            sb.AppendLine($"                    {memberType} member = default!;");
            sb.AppendLine("                    reader.ReadPolymorphismValue(ref member);");
            sb.AppendLine(member.Type.IsValueType
                ? $"                    value = ({classGlobalName})(object)member;"
                : $"                    value = LuminPackMarshal.As<{memberType}, {classGlobalName}>(ref member!);");
            sb.AppendLine("                    break;");
            sb.AppendLine("                }");
        }
        sb.AppendLine("                default:");
        sb.AppendLine($"                    LuminPackExceptionHelper.ThrowNotFoundInUnionType(tag, typeof({classGlobalName}));");
        sb.AppendLine("                    break;");
        sb.AppendLine("            }");

        foreach (var item in data.callBackMethods.Where(
                     static callback => callback.Item2 is SerializeCallBackType.OnDeserialized))
        {
            sb.AppendLine(item.Item3
                ? $"            {classGlobalName}.{item.Item1}();"
                : $"            value?.{item.Item1}();");
        }
    }

    public static void GenerateCalculateOffsetCode(
        LuminDataInfo data,
        StringBuilder sb,
        string classGlobalName)
    {
        if (!data.isValueType)
        {
            sb.AppendLine("            if (value is null)");
            sb.AppendLine("            {");
            sb.AppendLine("                evaluator += 1;");
            sb.AppendLine("                return;");
            sb.AppendLine("            }");
        }

        sb.AppendLine();
        string suffix = LuminPackUnionDispatchCodeGenerator.GetSlotSuffix(data.TypeSymbol.OriginalDefinition);
        sb.AppendLine($"            value.__LuminPackUnionCalculateOffset_{suffix}(ref evaluator);");
        sb.AppendLine();
    }

    public static void GenerateSerializeJsonCode(
        LuminDataInfo data,
        StringBuilder sb,
        string classGlobalName)
    {
        foreach (var item in data.callBackMethods.Where(
                     static callback => callback.Item2 is SerializeCallBackType.OnSerializing))
        {
            sb.AppendLine(item.Item3
                ? $"            {classGlobalName}.{item.Item1}();"
                : $"            value?.{item.Item1}();");
        }

        if (!data.isValueType)
        {
            sb.AppendLine("            if (value is null)");
            sb.AppendLine("            {");
            sb.AppendLine("                writer.WriteNull();");
            sb.AppendLine("                return;");
            sb.AppendLine("            }");
        }

        sb.AppendLine();
        string suffix = LuminPackUnionDispatchCodeGenerator.GetSlotSuffix(data.TypeSymbol.OriginalDefinition);
        sb.AppendLine($"            value.__LuminPackUnionSerializeJson_{suffix}(ref writer);");
        sb.AppendLine();

        foreach (var item in data.callBackMethods.Where(
                     static callback => callback.Item2 is SerializeCallBackType.OnSerialized))
        {
            sb.AppendLine(item.Item3
                ? $"            {classGlobalName}.{item.Item1}();"
                : $"            value?.{item.Item1}();");
        }
    }

    private static string GetMemberType(LuminDataInfo data, LuminUnionMemberInfo member)
    {
        if (!member.Type.IsUnboundGenericType)
        {
            return FormatterTypeName.Get(member.Type);
        }

        string prefix = member.Type.ContainingNamespace.IsGlobalNamespace
            ? "global::"
            : "global::" + member.Type.ContainingNamespace.ToDisplayString() + ".";
        return prefix + member.Type.Name + "<" + string.Join(", ", data.GenericParameters) + ">";
    }
}
