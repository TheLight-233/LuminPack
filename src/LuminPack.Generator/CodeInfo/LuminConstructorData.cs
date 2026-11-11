using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace LuminPack.Code;

/// <summary>Provides lumin constructor data functionality.</summary>
public sealed class LuminConstructorData
{
    /// <summary>Gets or sets the parameters.</summary>
    public List<ConstructorParameter> Parameters { get; set; } = new();
    /// <summary>Gets or sets the is marked with attribute.</summary>
    public bool IsMarkedWithAttribute { get; set; }
    /// <summary>Gets or sets the accessibility.</summary>
    public Accessibility Accessibility { get; set; }
    /// <summary>Gets the parameter count.</summary>
    public int ParameterCount => Parameters.Count;
}

/// <summary>Provides constructor parameter functionality.</summary>
public sealed class ConstructorParameter
{
    /// <summary>Gets or sets the name.</summary>
    public string Name { get; set; }
    /// <summary>Gets or sets the type.</summary>
    public string Type { get; set; }
    /// <summary>Gets or sets the type namespace.</summary>
    public string TypeNamespace { get; set; }
    /// <summary>Gets or sets the matches field.</summary>
    public bool MatchesField { get; set; }
    /// <summary>Gets or sets the matching field name.</summary>
    public string MatchingFieldName { get; set; }
}
