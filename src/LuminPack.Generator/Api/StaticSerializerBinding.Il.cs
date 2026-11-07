using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace LuminPack.Code.Core;

internal sealed partial class StaticSerializerBinding
{
    /// <summary>Emits build-only metadata without adding a runtime dependency.</summary>
    /// <param name="text">The generated source builder.</param>
    private static void AppendIlAttribute(StringBuilder text)
    {
        text.AppendLine("namespace LuminPack.Generated {");
        text.AppendLine("[global::System.AttributeUsage(global::System.AttributeTargets.Method, AllowMultiple = true)]");
        text.AppendLine("internal sealed class StaticCallAttribute : global::System.Attribute {");
        text.AppendLine("public StaticCallAttribute(string key, int profileArgument) { }\n}\n}");
    }

    /// <summary>Associates a static body with the exact original method construction.</summary>
    /// <param name="text">The generated source builder.</param>
    /// <param name="method">The original bound method.</param>
    /// <param name="profileArgument">The forwarded options argument, or minus one.</param>
    private static void AppendIlMapping(StringBuilder text, IMethodSymbol method, int profileArgument)
    {
        var definition = method.OriginalDefinition;
        string key = IlType(method.ContainingType) + "::" + method.MetadataName + "`" + method.Arity + "(" +
            string.Join(",", definition.Parameters.Select(parameter => IlType(parameter.Type) + (parameter.RefKind == RefKind.None ? "" : "&"))) + ")->" +
            IlType(definition.ReturnType) + "|" + string.Join(",", method.TypeArguments.Select((type, index) => index == profileArgument ? "*" : IlType(type)));
        text.Append("[global::LuminPack.Generated.StaticCall(").Append(SymbolDisplay.FormatLiteral(key, true))
            .Append(", ").Append(profileArgument).AppendLine(")]");
    }

    /// <summary>Formats the ECMA type shape shared with the build-time rewriter.</summary>
    /// <param name="type">A definition or constructed signature type.</param>
    /// <returns>A deterministic metadata shape without C# aliases or nullable annotations.</returns>
    private static string IlType(ITypeSymbol type)
    {
        if (type is IArrayTypeSymbol array)
            return IlType(array.ElementType) + "[" + new string(',', array.Rank - 1) + "]";
        if (type is IPointerTypeSymbol pointer)
            return IlType(pointer.PointedAtType) + "*";
        if (type is ITypeParameterSymbol parameter)
        {
            int offset = 0;
            for (var owner = parameter.ContainingType?.ContainingType; parameter.TypeParameterKind == TypeParameterKind.Type && owner != null; owner = owner.ContainingType)
                offset += owner.Arity;
            return (parameter.TypeParameterKind == TypeParameterKind.Method ? "!!" : "!") + (parameter.Ordinal + offset);
        }
        if (type.TypeKind == TypeKind.Dynamic)
            return "System.Object";
        var named = (INamedTypeSymbol)type;
        if (named.IsTupleType)
            named = named.TupleUnderlyingType;
        string name = named.MetadataName;
        var arguments = named.TypeArguments.ToList();
        for (var owner = named.ContainingType; owner != null; owner = owner.ContainingType)
        {
            name = owner.MetadataName + "/" + name;
            arguments.InsertRange(0, owner.TypeArguments);
        }
        if (!named.ContainingNamespace.IsGlobalNamespace)
            name = named.ContainingNamespace.ToDisplayString() + "." + name;
        return name + (arguments.Count == 0 ? "" : "<" + string.Join(",", arguments.Select(IlType)) + ">");
    }
}
