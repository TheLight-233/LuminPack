using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

/// <summary>Distinguishes a shared concrete JSON implementation from generic payload dispatch.</summary>
internal static class SerializerExecutionAssertions
{
    /// <summary>Recognizes a direct state call whose payload type remains statically selected.</summary>
    /// <param name="method">The method bound by the generated invocation.</param>
    /// <returns>Whether the call targets one existing concrete JSON state implementation.</returns>
    internal static bool IsConcreteJsonStateCall(IMethodSymbol? method)
    {
        method = method?.ReducedFrom ?? method;
        return method is { Parameters.Length: 2 } &&
            (method.ContainingType.Name == "LuminPackSerializer" && method.Name is "SerializeJson" or "DeserializeJson" ||
             IsSerializerRootContainer(method.ContainingType) && method.Name is "RootWriteJson" or "RootReadJson" ||
             method.ContainingType.Name.StartsWith("Binary_", StringComparison.Ordinal) && method.Name is "Write" or "Read") &&
            method.Parameters[0].RefKind == RefKind.Ref &&
            method.Parameters[0].Type.ToDisplayString() is "LuminPack.Core.LuminPackJsonWriter" or "LuminPack.Core.LuminPackJsonReader" &&
            method.Parameters[1].Type is not ITypeParameterSymbol &&
            method.TypeParameters.Length <= 1 &&
            (method.TypeParameters.Length == 0 || method.TypeParameters[0].ConstraintTypes.Any(type =>
                type.ToDisplayString() == "LuminPack.Option.ILuminPackOptions")) &&
            method.DeclaringSyntaxReferences.Length == 1;
    }

    /// <summary>Recognizes the Serializer or one of its payload root containers.</summary>
    /// <param name="type">The type declaring a root.</param>
    /// <returns>Whether the type is the Serializer or a container nested in it.</returns>
    internal static bool IsSerializerRootContainer(INamedTypeSymbol type)
        => type.Name == "LuminPackSerializer" ||
            type.Name.StartsWith("__LuminRoots_", StringComparison.Ordinal) && type.ContainingType?.Name == "LuminPackSerializer";

    /// <summary>Recognizes a generated Serializer declaration or one of its payload root containers.</summary>
    /// <param name="declaration">The parent of a generated member.</param>
    /// <returns>Whether members of the declaration belong to the Serializer.</returns>
    internal static bool IsSerializerRootContainer(SyntaxNode? declaration)
        => declaration is ClassDeclarationSyntax { Identifier.ValueText: "LuminPackSerializer" } ||
            declaration is ClassDeclarationSyntax type && type.Identifier.ValueText.StartsWith("__LuminRoots_", StringComparison.Ordinal) &&
            type.Parent is ClassDeclarationSyntax { Identifier.ValueText: "LuminPackSerializer" };
}
