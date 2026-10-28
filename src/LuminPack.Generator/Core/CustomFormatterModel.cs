using Microsoft.CodeAnalysis;

namespace LuminPack.Code.Core;

/// <summary>Analysis result for one [LuminPackable(GeneratorType.Custom)] type.</summary>
internal sealed class CustomFormatterModel
{
    /// <summary>Initializes a new instance of <c>CustomFormatterModel</c>.</summary>
    /// <param name="type">The type or symbol to analyze.</param>
    /// <param name="typeName">The C# type name to use in generated source.</param>
    internal CustomFormatterModel(INamedTypeSymbol type, string typeName)
    {
        Type = type;
        TypeName = typeName;
    }

    /// <summary>Gets the type.</summary>
    internal INamedTypeSymbol Type { get; }

    /// <summary>global::-qualified type name usable in generated code.</summary>
    internal string TypeName { get; }

    /// <summary>Gets or sets the serialize.</summary>
    internal IMethodSymbol? Serialize { get; set; }
    /// <summary>Gets or sets the deserialize.</summary>
    internal IMethodSymbol? Deserialize { get; set; }
    /// <summary>Gets or sets the serialize json.</summary>
    internal IMethodSymbol? SerializeJson { get; set; }
    /// <summary>Gets or sets the deserialize json.</summary>
    internal IMethodSymbol? DeserializeJson { get; set; }
    /// <summary>Gets or sets the calculate offset.</summary>
    internal IMethodSymbol? CalculateOffset { get; set; }

    /// <summary>Gets or sets the has errors.</summary>
    internal bool HasErrors { get; set; }

    /// <summary>Register overload tier: 1 = binary, 2 = +JSON, 3 = +JSON+calculate. 0 = invalid.</summary>
    internal int RegisterTier
    {
        get
        {
            if (Serialize is null || Deserialize is null) return 0;
            if (CalculateOffset is not null) return 3;
            if (SerializeJson is not null && DeserializeJson is not null) return 2;
            return 1;
        }
    }
}
