using System.Linq;
using System.Reflection;
using System.Threading;
using LuminPack.SourceGenerator.Next.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LuminPack.SourceGenerator.Next.Analysis;

/// <summary>Discovers closed calls to the public <c>LuminPackSerializer</c> facade and their interception locations.</summary>
/// <remarks>This is a Roslyn boundary: the semantic model binds the call against the published facade and only compiler-free
/// facts leave. Calls whose payload or profile is open, unbound or outside the supported overloads are not returned and keep
/// the facade's registration-only fallback.</remarks>
internal static class SerializerCallAnalysis
{
    private const string SerializerName = "LuminPack.LuminPackSerializer";

    private static readonly MethodInfo? LocationMethod = FindLocationMethod();

    /// <summary>Whether this compiler host can publish versioned interception locations.</summary>
    internal static bool Supported => LocationMethod != null;

    /// <summary>Checks the syntax shape cheaply before binding.</summary>
    /// <param name="node">The candidate node.</param>
    /// <returns>Whether the node is an invocation of a facade method name.</returns>
    internal static bool IsCandidate(SyntaxNode node)
    {
        if (node is not InvocationExpressionSyntax invocation)
            return false;
        SimpleNameSyntax? name = invocation.Expression switch
        {
            MemberAccessExpressionSyntax access => access.Name,
            SimpleNameSyntax simple => simple,
            _ => null,
        };
        return name != null && name.Identifier.ValueText is "Serialize" or "Deserialize" or "SerializeJson" or "DeserializeJson" or
            "SerializeAsync" or "DeserializeAsync";
    }

    /// <summary>Binds one candidate call.</summary>
    /// <param name="context">The syntax and its semantic model.</param>
    /// <param name="cancellationToken">Cancels analysis.</param>
    /// <returns>The call model, or null when the call stays on the generic facade.</returns>
    internal static SerializerCallModel? Analyze(GeneratorSyntaxContext context, CancellationToken cancellationToken)
    {
        if (LocationMethod == null || context.Node is not InvocationExpressionSyntax invocation)
            return null;
        if (context.SemanticModel.GetSymbolInfo(invocation, cancellationToken).Symbol is not IMethodSymbol method ||
            !method.IsGenericMethod || method.ContainingType?.ToDisplayString() != SerializerName)
            return null;
        SerializerApi? api = Classify(method.OriginalDefinition);
        if (api == null)
            return null;
        ITypeSymbol payload = method.TypeArguments[0];
        if (!IsClosed(payload))
            return null;
        string? profile = null;
        bool openProfile = false;
        if (method.TypeArguments.Length == 2)
        {
            // An open profile keeps the operation's profile parameter; the payload must still be closed.
            if (method.TypeArguments[1] is ITypeParameterSymbol)
                openProfile = true;
            else if (!IsClosed(method.TypeArguments[1]) || !Accessible(method.TypeArguments[1]))
                return null;
            else
                profile = method.TypeArguments[1].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        }
        else if (method.TypeArguments.Length != 1)
        {
            return null;
        }
        cancellationToken.ThrowIfCancellationRequested();
        string? location = Location(context.SemanticModel, invocation, cancellationToken);
        if (location == null)
            return null;
        // A nullable value root keeps its Nullable spelling; the shorthand loses the distinction in owner names.
        // Nested nullable value types are spelled as Nullable<T> too: owner keys drop '?' annotations, which would merge int? and int.
        string spelling = payload.WithNullableAnnotation(NullableAnnotation.NotAnnotated).ToDisplayString(PayloadFormat);
        MemberModel? root = TypeModelFactory.TryCreateRoot(payload, context.SemanticModel.Compilation,
            Pipeline.GeneratorOptionsFactory.From(context.SemanticModel.Compilation));
        // A closed generic payload binds the generic roots of its definition, as the original concrete facade does.
        string? definition = null, typeArguments = null;
        if (payload is INamedTypeSymbol { IsGenericType: true } generic && generic.ContainingType is not { IsGenericType: true } &&
            generic.OriginalDefinition.SpecialType == SpecialType.None)
        {
            definition = generic.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            typeArguments = string.Join(", ", generic.TypeArguments.Select(argument => argument.ToDisplayString(PayloadFormat)));
        }
        EntryShape shape = TypeModelFactory.EntryShapeOf(payload, context.SemanticModel.Compilation,
            Pipeline.GeneratorOptionsFactory.From(context.SemanticModel.Compilation));
        return new SerializerCallModel(api.Value, spelling, profile, location, payload.IsValueType, root, openProfile, definition, typeArguments,
            shape);
    }

    private static readonly SymbolDisplayFormat PayloadFormat = SymbolDisplayFormat.FullyQualifiedFormat
        .AddMiscellaneousOptions(SymbolDisplayMiscellaneousOptions.ExpandNullable);

    /// <summary>Whether generated code outside the declaring type can name the type: a private or protected nested type keeps
    /// its call on the facade.</summary>
    private static bool Accessible(ITypeSymbol type)
    {
        for (ITypeSymbol? current = type; current != null; current = current.ContainingType)
            if (current.DeclaredAccessibility is Microsoft.CodeAnalysis.Accessibility.Private or Microsoft.CodeAnalysis.Accessibility.Protected or
                Microsoft.CodeAnalysis.Accessibility.ProtectedAndInternal)
                return false;
        return type is not INamedTypeSymbol named || named.TypeArguments.All(Accessible);
    }

    private static bool IsClosed(ITypeSymbol type) => type.TypeKind is not (TypeKind.Error or TypeKind.TypeParameter or TypeKind.Dynamic) &&
        !ContainsTypeParameter(type);

    private static bool ContainsTypeParameter(ITypeSymbol type) => type switch
    {
        ITypeParameterSymbol => true,
        IArrayTypeSymbol array => ContainsTypeParameter(array.ElementType),
        INamedTypeSymbol named => named.TypeArguments.Any(ContainsTypeParameter) ||
            named.ContainingType != null && ContainsTypeParameter(named.ContainingType),
        IPointerTypeSymbol pointer => ContainsTypeParameter(pointer.PointedAtType),
        _ => false,
    };

    /// <summary>Classifies the original facade overload by its parameter shapes.</summary>
    private static SerializerApi? Classify(IMethodSymbol definition)
    {
        var parameters = definition.Parameters;
        string First() => Shape(parameters[0].Type);
        bool returnsValue = definition.ReturnType is ITypeParameterSymbol;
        switch (definition.Name)
        {
            case "Serialize" when parameters.Length == 1 && parameters[0].Type is ITypeParameterSymbol && !definition.ReturnsVoid:
                return SerializerApi.SerializeArray;
            case "Serialize" when parameters.Length == 2 && parameters[0].RefKind == RefKind.Ref && First() == "LuminPackWriter":
                return SerializerApi.SerializeWriter;
            case "Deserialize" when parameters.Length == 2 && parameters[0].RefKind == RefKind.Ref && First() == "LuminPackReader":
                return SerializerApi.DeserializeReader;
            case "SerializeJson" when parameters.Length == 2 && parameters[0].RefKind == RefKind.Ref && First() == "LuminPackJsonWriter":
                return SerializerApi.SerializeJsonWriter;
            case "DeserializeJson" when parameters.Length == 2 && parameters[0].RefKind == RefKind.Ref && First() == "LuminPackJsonReader":
                return SerializerApi.DeserializeJsonReader;
            case "Serialize" when parameters.Length == 2 && parameters[0].Type is ITypeParameterSymbol:
                return Shape(parameters[1].Type) switch
                {
                    "IBufferWriter" => SerializerApi.SerializeBufferWriter,
                    "LuminBufferWriter" => SerializerApi.SerializeBuffer,
                    _ => null,
                };
            case "Deserialize" when parameters.Length == 1 && returnsValue:
                return First() switch
                {
                    "ReadOnlySpan<byte>" => SerializerApi.DeserializeSpan,
                    "LuminBufferWriter" => SerializerApi.DeserializeBuffer,
                    _ => null,
                };
            case "Deserialize" when parameters.Length == 2 && parameters[1].RefKind == RefKind.Ref && parameters[1].Type is ITypeParameterSymbol:
                return First() switch
                {
                    "ReadOnlySpan<byte>" => SerializerApi.DeserializeSpanRef,
                    "LuminBufferWriter" => SerializerApi.DeserializeBufferRef,
                    "ReadOnlySequence" when parameters[0].Type is INamedTypeSymbol { TypeArguments.Length: 1 } sequence &&
                        sequence.TypeArguments[0].SpecialType == SpecialType.System_Byte => SerializerApi.DeserializeSequenceRef,
                    _ => null,
                };
            case "SerializeJson" when parameters.Length == 1 && parameters[0].Type is ITypeParameterSymbol && !definition.ReturnsVoid:
                return SerializerApi.SerializeJsonString;
            case "SerializeJson" when parameters.Length == 2 && parameters[0].Type is ITypeParameterSymbol:
                return Shape(parameters[1].Type) switch
                {
                    "IBufferWriter" => SerializerApi.SerializeJsonBufferWriter,
                    "LuminBufferWriter" => SerializerApi.SerializeJsonBuffer,
                    _ => null,
                };
            case "DeserializeJson" when parameters.Length == 1 && returnsValue:
                return First() switch
                {
                    "string" => SerializerApi.DeserializeJsonString,
                    "ReadOnlySpan<byte>" => SerializerApi.DeserializeJsonBytes,
                    "ReadOnlySpan<char>" => SerializerApi.DeserializeJsonChars,
                    "LuminBufferWriter" => SerializerApi.DeserializeJsonBuffer,
                    _ => null,
                };
            case "DeserializeJson" when parameters.Length == 2 && parameters[1].RefKind == RefKind.Ref && parameters[1].Type is ITypeParameterSymbol:
                return First() switch
                {
                    "string" => SerializerApi.DeserializeJsonStringRef,
                    "ReadOnlySpan<byte>" => SerializerApi.DeserializeJsonBytesRef,
                    "ReadOnlySpan<char>" => SerializerApi.DeserializeJsonCharsRef,
                    "LuminBufferWriter" => SerializerApi.DeserializeJsonBufferRef,
                    _ => null,
                };
            case "SerializeAsync" when parameters.Length == 3 && First() == "Stream" && parameters[1].Type is ITypeParameterSymbol:
                return SerializerApi.SerializeStream;
            case "DeserializeAsync" when parameters.Length == 2 && First() == "Stream":
                return SerializerApi.DeserializeStream;
            case "DeserializeAsync" when parameters.Length == 3 && First() == "Stream" && parameters[1].Type is ITypeParameterSymbol:
                return SerializerApi.DeserializeStreamValue;
            default:
                return null;
        }
    }

    private static string Shape(ITypeSymbol type) => type switch
    {
        { SpecialType: SpecialType.System_String } => "string",
        INamedTypeSymbol { Name: "ReadOnlySpan", TypeArguments.Length: 1 } span => "ReadOnlySpan<" +
            (span.TypeArguments[0].SpecialType == SpecialType.System_Byte ? "byte" :
                span.TypeArguments[0].SpecialType == SpecialType.System_Char ? "char" : "?") + ">",
        INamedTypeSymbol { Name: "IBufferWriter" } => "IBufferWriter",
        INamedTypeSymbol { Name: "LuminBufferWriter" } => "LuminBufferWriter",
        _ => type.Name,
    };

    /// <summary>Obtains the compiler-owned interception attribute without raising the Roslyn 4.3 loading requirement.</summary>
    private static string? Location(SemanticModel model, InvocationExpressionSyntax call, CancellationToken cancellationToken)
    {
        object? location = LocationMethod!.Invoke(null, new object[] { model, call, cancellationToken });
        if (location == null)
            return null;
        var type = location.GetType();
        object version = type.GetProperty("Version")!.GetValue(location);
        string data = (string)type.GetProperty("Data")!.GetValue(location);
        return "[global::System.Runtime.CompilerServices.InterceptsLocation(" + version + ", " + SymbolDisplay.FormatLiteral(data, true) + ")]";
    }

    private static MethodInfo? FindLocationMethod()
    {
        var assembly = typeof(CSharpCompilation).Assembly;
        foreach (string name in new[] { "Microsoft.CodeAnalysis.CSharp.CSharpExtensions", "Microsoft.CodeAnalysis.CSharpExtensions" })
        {
            var method = assembly.GetType(name)?.GetMethod("GetInterceptableLocation", BindingFlags.Public | BindingFlags.Static, null,
                new[] { typeof(SemanticModel), typeof(InvocationExpressionSyntax), typeof(CancellationToken) }, null);
            if (method != null)
                return method;
        }
        return null;
    }
}
