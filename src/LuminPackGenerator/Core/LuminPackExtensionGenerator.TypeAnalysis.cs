using System.Collections.Generic;
using System.Linq;
using System.Text;
using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;

namespace LuminPack.Code.Core;

public static partial class LuminPackExtensionGenerator
{

    /// <summary>Builds registration and each generic dispatch operation as independent source contributions.</summary>
    /// <param name="metaInfo">Current language, framework, and registration settings.</param>
    /// <param name="extensionClassName">The common partial extension container.</param>
    /// <param name="dispatch">Stable dense ids and operation capabilities.</param>
    /// <param name="fallback">The consuming assembly's constants for unknown registered types.</param>
    /// <param name="publish">Receives a fixed operation identity and its unchanged declaration text.</param>
    private static void BuildGenericDispatchParts(MetaInfo metaInfo, string extensionClassName, DispatchEntry[] dispatch, CompiledOptions fallback, System.Action<string, string> publish)
    {
        var sb = new StringBuilder();
        string scopedIn = metaInfo.IsNet8 ? "scoped in " : "in ";
        string scopedRef = metaInfo.IsNet8 ? "scoped ref " : "ref ";
        string unsafeAs = "global::System.Runtime.CompilerServices.Unsafe.As";
        string throwNoFormatter = "global::LuminPack.Code.LuminPackExceptionHelper.ThrowNoSourceGeneratedFormatter(typeof(T));";

        // The static ctor writes each per-type compact id before any generic dispatch extension can
        // run (the JIT runs a type's static ctor before its static methods). uint.MaxValue stays the
        // "not generated" sentinel, so ids can be dense 0,1,2,3... for a JIT jump table.
        sb.AppendLine("        static " + extensionClassName + "()");
        sb.AppendLine("        {");
        foreach (DispatchEntry d in dispatch)
        {
            sb.AppendLine("            LuminPackTypeCache<" + d.Name + ">.TypeId = " + d.Id + "u;");
        }
        sb.AppendLine("        }");
        sb.AppendLine();
        // Compact ids describe this assembly's switch, not a process-wide formatter ordering.
        sb.AppendLine("        private static class LuminPackTypeCache<T>");
        sb.AppendLine("        {");
        sb.AppendLine("            internal static uint TypeId = uint.MaxValue;");
        sb.AppendLine("        }");
        sb.AppendLine("        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
        sb.AppendLine("        internal static bool __LuminPackShouldResetMember<T>(ref global::LuminPack.Core.LuminPackReader reader)");
        sb.AppendLine("        {");
        sb.AppendLine("            if (typeof(T).IsValueType) return false;");
        sb.AppendLine("            switch (LuminPackTypeCache<T>.TypeId)");
        sb.AppendLine("            {");
        foreach (var entry in dispatch.Where(entry => entry.Binary && entry.OrdinaryObjectHeader))
            sb.AppendLine("                case " + entry.Id + "u: return reader.PeekIsNullObject(ref reader.GetCurrentSpanOffset());");
        foreach (var entry in dispatch.Where(entry => !entry.Binary || !entry.OrdinaryObjectHeader))
            sb.AppendLine("                case " + entry.Id + "u: return false;");
        // An opaque registered formatter keeps its original fresh member boundary.
        sb.AppendLine("                default: return true;");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine("        internal static global::LuminPack.Option.LuminPackStringEncoding __LuminPackJsonEncoding<T>()");
        sb.AppendLine("        {");
        sb.AppendLine("            switch (LuminPackTypeCache<T>.TypeId)");
        sb.AppendLine("            {");
        foreach (DispatchEntry d in dispatch.Where(entry => entry.Options.Encoding != fallback.Encoding))
            sb.AppendLine("                case " + d.Id + "u: return (global::LuminPack.Option.LuminPackStringEncoding)" + d.Options.Encoding + ";");
        sb.AppendLine("                default: return (global::LuminPack.Option.LuminPackStringEncoding)" + fallback.Encoding + ";");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine("        internal static bool __LuminPackJsonReadBoundary<T>(ref global::LuminPack.Core.LuminPackJsonReader reader, bool complete)");
        sb.AppendLine("        {");
        sb.AppendLine("            switch (LuminPackTypeCache<T>.TypeId)");
        sb.AppendLine("            {");
        foreach (var group in dispatch.Where(entry => entry.Options.Identity != fallback.Identity).GroupBy(entry => entry.Options.Identity))
        {
            foreach (DispatchEntry d in group) sb.AppendLine("                case " + d.Id + "u:");
            string profile = group.First().Options.TypeName;
            sb.AppendLine("                    if (complete) { reader.EnsureEndOfDocument<" + profile + ">(); return true; }");
            sb.AppendLine("                    return reader.Read<" + profile + ">();");
        }
        sb.AppendLine("                default:");
        sb.AppendLine("                    if (complete) { reader.EnsureEndOfDocument<" + fallback.TypeName + ">(); return true; }");
        sb.AppendLine("                    return reader.Read<" + fallback.TypeName + ">();");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine("        internal static global::LuminPack.Option.LuminPackStringEncoding __LuminPackJsonEncoding<T, TOptions>() where TOptions : struct, global::LuminPack.Option.ILuminPackOptions");
        sb.AppendLine("            => typeof(TOptions) == typeof(global::LuminPack.Option.LuminPackDeclaredOptions) ? __LuminPackJsonEncoding<T>() : default(TOptions).StringEncoding;");
        sb.AppendLine("        internal static bool __LuminPackJsonReadBoundary<T, TOptions>(ref global::LuminPack.Core.LuminPackJsonReader reader, bool complete) where TOptions : struct, global::LuminPack.Option.ILuminPackOptions");
        sb.AppendLine("        {");
        sb.AppendLine("            if (typeof(TOptions) == typeof(global::LuminPack.Option.LuminPackDeclaredOptions)) return __LuminPackJsonReadBoundary<T>(ref reader, complete);");
        sb.AppendLine("            if (complete) { reader.EnsureEndOfDocument<TOptions>(); return true; }");
        sb.AppendLine("            return reader.Read<TOptions>();");
        sb.AppendLine("        }");
        publish("Registration", sb.ToString());
        sb.Clear();
        AppendGenericSwitch(sb, dispatch, "WriteValue", "ref this LuminPackWriter writer", scopedIn, "T value", metaInfo, op: "write", isJson: false, unsafeAs, throwNoFormatter);
        publish("BinaryWrite", sb.ToString());
        sb.Clear();
        AppendGenericSwitch(sb, dispatch, "ReadValue", "ref this LuminPackReader reader", scopedRef, "T value", metaInfo, op: "read", isJson: false, unsafeAs, throwNoFormatter);
        publish("BinaryRead", sb.ToString());
        sb.Clear();
        AppendGenericSwitch(sb, dispatch, "CalculateOffset", "ref this LuminPackEvaluator evaluator", scopedRef, "T value", metaInfo, op: "calculate", isJson: false, unsafeAs, throwNoFormatter);
        publish("Calculate", sb.ToString());
        sb.Clear();
        AppendGenericSwitch(sb, dispatch, "WriteValue", "ref this global::LuminPack.Core.LuminPackJsonWriter writer", scopedIn, "T value", metaInfo, op: "write", isJson: true, unsafeAs, throwNoFormatter);
        publish("JsonWrite", sb.ToString());
        sb.Clear();
        AppendGenericSwitch(sb, dispatch, "ReadValue", "ref this global::LuminPack.Core.LuminPackJsonReader reader", scopedRef, "T value", metaInfo, op: "read", isJson: true, unsafeAs, throwNoFormatter);
        publish("JsonRead", sb.ToString());
    }

    /// <summary>Builds the generic switch source from the supplied metadata.</summary>
    /// <param name="sb">The builder receiving generated source code.</param>
    /// <param name="dispatch">The dispatch used by this operation.</param>
    /// <param name="methodName">The method name used by this operation.</param>
    /// <param name="receiver">The receiver used by this operation.</param>
    /// <param name="paramModifier">The param modifier used by this operation.</param>
    /// <param name="param">The param used by this operation.</param>
    /// <param name="metaInfo">The language, framework, and generation settings.</param>
    /// <param name="op">The op used by this operation.</param>
    /// <param name="isJson">The is json used by this operation.</param>
    /// <param name="unsafeAs">The unsafe as used by this operation.</param>
    /// <param name="throwNoFormatter">The throw no formatter used by this operation.</param>
    private static void AppendGenericSwitch(
        StringBuilder sb,
        DispatchEntry[] dispatch,
        string methodName,
        string receiver,
        string paramModifier,
        string param,
        MetaInfo metaInfo,
        string op,
        bool isJson,
        string unsafeAs,
        string throwNoFormatter)
    {
        string cacheType = "LuminPackTypeCache";
        string registryTryGet = "global::LuminPack.Code.LuminPackFormatterRegistry.TryGetValue(global::LuminPack.Code.LuminPackMarshal.GetMethodTable(typeof(T)), out var __entry)";
        sb.AppendLine("        [global::LuminPack.Attribute.Preserve]");
        sb.AppendLine("        [global::System.Runtime.CompilerServices.MethodImpl(MethodImplOptions.AggressiveInlining)]");
        sb.AppendLine("        public static void " + methodName + "<T>(" + receiver + ", " + paramModifier + param + ")");
        sb.AppendLine("        {");
        sb.AppendLine("            switch (" + cacheType + "<T>.TypeId)");
        sb.AppendLine("            {");
        foreach (DispatchEntry d in dispatch)
        {
            bool supported = op switch
            {
                "write" => isJson ? d.Json : d.Binary,
                "read" => isJson ? d.Json : d.Binary,
                "calculate" => d.Calculate,
                _ => false
            };
            sb.AppendLine("                case " + d.Id + "u:");
            sb.AppendLine("                {");
            if (supported)
            {
                switch (op)
                {
                    case "write":
                        sb.AppendLine("                    var v = " + unsafeAs + "<T, " + d.Name + ">(ref global::System.Runtime.CompilerServices.Unsafe.AsRef(in value)); writer.WriteValue(in v); return;");
                        break;
                    case "read":
                        sb.AppendLine("                    ref var v = ref " + unsafeAs + "<T, " + d.Name + ">(ref value); reader.ReadValue(ref v); value = " + unsafeAs + "<" + d.Name + ", T>(ref v); return;");
                        break;
                    case "calculate":
                        sb.AppendLine("                    var v = " + unsafeAs + "<T, " + d.Name + ">(ref value); evaluator.CalculateOffset(ref v); return;");
                        break;
                }
            }
            else
            {
                sb.AppendLine("                    " + throwNoFormatter);
                sb.AppendLine("                    return;");
            }
            sb.AppendLine("                }");
        }
        sb.AppendLine("                default:");
        if (!metaInfo.AllowUnsafe || metaInfo.RegisterMode == LuminPackRegisterMode.Disabled)
        {
            // Without AllowUnsafeBlocks the registered method pointers cannot be invoked, so the
            // fallback registry is not emitted at all.
            sb.AppendLine("                {");
            sb.AppendLine("                    " + throwNoFormatter);
            sb.AppendLine("                    return;");
            sb.AppendLine("                }");
        }
        else
        {
            sb.AppendLine("                {");
            sb.AppendLine("                    if (!" + registryTryGet + ")");
            sb.AppendLine("                    {");
            sb.AppendLine("                        " + throwNoFormatter);
            sb.AppendLine("                        return;");
            sb.AppendLine("                    }");
            switch (op)
            {
                case "write":
                    if (isJson)
                    {
                        sb.AppendLine("                    if (__entry.WriteValueJson == 0) { " + throwNoFormatter + " }");
                        sb.AppendLine("                    unsafe { ((delegate*<ref global::LuminPack.Core.LuminPackJsonWriter, in T, void>)__entry.WriteValueJson)(ref writer, in value); } return;");
                    }
                    else
                    {
                        sb.AppendLine("                    unsafe { ((delegate*<ref global::LuminPack.Core.LuminPackWriter, in T, void>)__entry.WriteValue)(ref writer, in value); } return;");
                    }
                    break;
                case "read":
                    if (isJson)
                    {
                        sb.AppendLine("                    if (__entry.ReadValueJson == 0) { " + throwNoFormatter + " }");
                        sb.AppendLine("                    unsafe { ((delegate*<ref global::LuminPack.Core.LuminPackJsonReader, ref T, void>)__entry.ReadValueJson)(ref reader, ref value); } return;");
                    }
                    else
                    {
                        sb.AppendLine("                    unsafe { ((delegate*<ref global::LuminPack.Core.LuminPackReader, ref T, void>)__entry.ReadValue)(ref reader, ref value); } return;");
                    }
                    break;
                case "calculate":
                    sb.AppendLine("                    if (__entry.CalculateOffset == 0) { " + throwNoFormatter + " }");
                    sb.AppendLine("                    unsafe { ((delegate*<ref global::LuminPack.Core.LuminPackEvaluator, in T, void>)__entry.CalculateOffset)(ref evaluator, in value); } return;");
                    break;
            }
            sb.AppendLine("                }");
        }
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine();
    }
    /// <summary>Performs the enumerate formatter fields step for the current operation.</summary>
    /// <param name="data">The metadata describing the serializable type.</param>
    /// <returns>The result of the operation.</returns>
    private static IEnumerable<LuminLocalFieldData> EnumerateFormatterFields(LuminDataInfo data)
    {
        HashSet<ITypeSymbol> visited = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
        foreach (LuminDataField field in data.fields)
        {
            foreach (LuminLocalFieldData formatterField in EnumerateFormatterFields(field.TypeSymbol, visited))
            {
                yield return formatterField;
            }
        }
    }

    /// <summary>Performs the enumerate formatter fields step for the current operation.</summary>
    /// <param name="type">The type or symbol to analyze.</param>
    /// <param name="visited">The visited used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    private static IEnumerable<LuminLocalFieldData> EnumerateFormatterFields(ITypeSymbol? type, HashSet<ITypeSymbol> visited)
    {
        if (type is null || type is ITypeParameterSymbol || !visited.Add(type))
        {
            yield break;
        }

        if (type is IArrayTypeSymbol array)
        {
            foreach (LuminLocalFieldData element in EnumerateFormatterFields(array.ElementType, visited))
            {
                yield return element;
            }
        }
        else if (type is INamedTypeSymbol named)
        {
            foreach (ITypeSymbol argument in named.TypeArguments)
            {
                foreach (LuminLocalFieldData argumentField in EnumerateFormatterFields(argument, visited))
                {
                    yield return argumentField;
                }
            }

            // These LINQ formatters serialize through concrete interface views which may
            // never appear explicitly in user syntax. Keep their exact closed helper types
            // in the formatter dependency graph so nested calls can bind statically.
            string definitionName = named.OriginalDefinition.ToDisplayString();
            if (definitionName == "System.Linq.ILookup<TKey, TElement>" ||
                definitionName == "System.Linq.IGrouping<TKey, TElement>")
            {
                foreach (INamedTypeSymbol enumerable in named.AllInterfaces.Where(static candidate =>
                    candidate.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T))
                {
                    foreach (LuminLocalFieldData dependency in EnumerateFormatterFields(enumerable, visited))
                    {
                        yield return dependency;
                    }
                }
            }
        }

        yield return new LuminLocalFieldData
        {
            TypeName = FormatterTypeName.Get(type),
            TypeSymbol = type,
            Name = "value",
            IsValue = type.IsValueType
        };
    }

    /// <summary>Determines whether the metadata contains type parameter.</summary>
    /// <param name="type">The type or symbol to analyze.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    internal static bool ContainsTypeParameter(ITypeSymbol? type)
    {
        if (type is null || type is ITypeParameterSymbol)
        {
            return type is ITypeParameterSymbol;
        }

        return type switch
        {
            IArrayTypeSymbol array => ContainsTypeParameter(array.ElementType),
            INamedTypeSymbol named => named.IsUnboundGenericType || ContainsTypeParameter(named.ContainingType) || named.TypeArguments.Any(ContainsTypeParameter),
            _ => false
        };
    }

    /// <summary>Determines whether the supplied value has the aot visible classification.</summary>
    /// <param name="type">The type or symbol to analyze.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    internal static bool IsAotVisible(ITypeSymbol type)
    {
        if (type is IArrayTypeSymbol array)
        {
            return IsAotVisible(array.ElementType);
        }

        if (type is not INamedTypeSymbol named || named.IsAnonymousType)
        {
            return !type.IsAnonymousType;
        }

        if (named.TypeArguments.Any(argument => !IsAotVisible(argument)))
        {
            return false;
        }

        for (INamedTypeSymbol? current = named; current is not null; current = current.ContainingType)
        {
            if (current.DeclaredAccessibility is Accessibility.Private or Accessibility.Protected or Accessibility.ProtectedAndInternal)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Obtains the global type name for the supplied input.</summary>
    /// <param name="data">The metadata describing the serializable type.</param>
    /// <returns>The result of the operation.</returns>
    private static string GetGlobalTypeName(LuminDataInfo data)
    {
        string typeName = data.classFullName;
        return typeName.IndexOf('.') < 0 && data.classNameSpace != "<global namespace>"
            ? "global::" + data.classNameSpace + "." + typeName
            : typeName;
    }

    /// <summary>Builds the generic constraints source from the supplied metadata.</summary>
    /// <param name="sb">The builder receiving generated source code.</param>
    /// <param name="data">The metadata describing the serializable type.</param>
    internal static void AppendGenericConstraints(StringBuilder sb, LuminDataInfo data)
    {
        foreach (GenericParameterConstraint constraint in data.GenericConstraints)
        {
            List<string> parts = new List<string>();
            if (constraint.IsUnmanaged) parts.Add("unmanaged");
            if (constraint.IsClass) parts.Add("class");
            if (constraint.IsStruct) parts.Add("struct");
            if (constraint.IsNotNull) parts.Add("notnull");
            parts.AddRange(constraint.Constraints);
            if (constraint.HasNewConstructor) parts.Add("new()");
            if (constraint.HasDefault) parts.Add("default");
            if (parts.Count > 0) sb.AppendLine("            where " + constraint.ParameterName + " : " + string.Join(", ", parts));
        }
    }

}
