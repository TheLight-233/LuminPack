using Microsoft.CodeAnalysis;

/// <summary>Distinguishes a shared concrete JSON implementation from generic payload dispatch.</summary>
internal static class SerializerExecutionAssertions
{
    /// <summary>Recognizes a direct state call whose payload type remains statically selected.</summary>
    /// <param name="method">The method bound by the generated invocation.</param>
    /// <returns>Whether the call targets one existing concrete JSON state implementation.</returns>
    internal static bool IsConcreteJsonStateCall(IMethodSymbol? method)
        => method is { ContainingType.Name: "LuminPackSerializer", Parameters.Length: 2 } &&
            method.Name is "SerializeJson" or "DeserializeJson" &&
            method.Parameters[0].RefKind == RefKind.Ref &&
            method.Parameters[0].Type.ToDisplayString() is "LuminPack.Core.LuminPackJsonWriter" or "LuminPack.Core.LuminPackJsonReader" &&
            method.Parameters[1].Type is not ITypeParameterSymbol &&
            method.TypeParameters.Length <= 1 &&
            (method.TypeParameters.Length == 0 || method.TypeParameters[0].ConstraintTypes.Any(type =>
                type.ToDisplayString() == "LuminPack.Option.ILuminPackOptions")) &&
            method.DeclaringSyntaxReferences.Length == 1;
}
