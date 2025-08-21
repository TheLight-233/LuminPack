using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace LuminPack.Code
{
    public sealed class LuminDataInfo
    {
        public string className = "NewData";
        public string classFullName;
        public string classNameSpace = "Your Data NameSpace";
        public bool isGeneric;
        public StructLayout structLayout;
        public List<string> GenericParameters =  new ();
        public List<GenericParameterConstraint> GenericConstraints = new ();
        public bool isValueType;
        public readonly List<LuminDataField> fields = new ();
        public readonly List<LuminLocalFieldData> localFields = new ();
        public readonly List<(string, SerializeCallBackType, bool)> callBackMethods = new ();
        
        
        public bool enableBurst;

        public GeneratorType generatorType;
        
        //Union
        public bool isUnion;
        public Dictionary<INamedTypeSymbol, List<string>> UnionGenericTypes { get; } = 
            new Dictionary<INamedTypeSymbol, List<string>>(SymbolEqualityComparer.Default);
        public List<LuminUnionMemberInfo> UnionMembers { get; } = new List<LuminUnionMemberInfo>();
        
    }
    
    public class GenericParameterConstraint
    {
        public string ParameterName { get; set; }
        public List<string> Constraints { get; set; } = new();
        public bool IsUnmanaged { get; set; }
        public bool IsClass { get; set; }
        public bool IsStruct { get; set; }
        public bool IsNotNull { get; set; }
        public bool HasDefault { get; set; }
        public bool HasNewConstructor { get; set; } 
    }
}