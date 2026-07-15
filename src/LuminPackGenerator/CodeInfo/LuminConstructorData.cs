using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace LuminPack.Code;

public sealed class LuminConstructorData
{
    public List<ConstructorParameter> Parameters { get; set; } = new();
    public bool IsMarkedWithAttribute { get; set; }
    public Accessibility Accessibility { get; set; }
    public int ParameterCount => Parameters.Count;
}

public sealed class ConstructorParameter
{
    public string Name { get; set; }
    public string Type { get; set; }
    public string TypeNamespace { get; set; }
    public bool MatchesField { get; set; }
    public string MatchingFieldName { get; set; }
}
