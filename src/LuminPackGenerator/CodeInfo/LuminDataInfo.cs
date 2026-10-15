using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace LuminPack.Code
{
    /// <summary>Describes a packable type's ordered members, construction, callbacks, and generation mode.</summary>
    public sealed class LuminDataInfo
    {
        /// <summary>Stores the class name.</summary>
        public string className = "NewData";
        /// <summary>Stores the class file name.</summary>
        public string classFileName;
        /// <summary>Stores the class full name.</summary>
        public string classFullName;
        /// <summary>Stores the class name space.</summary>
        public string classNameSpace = "Your Data NameSpace";
        /// <summary>Stores the is generic.</summary>
        public bool isGeneric;
        /// <summary>Gets or sets the struct layout.</summary>
        public StructLayout structLayout { get; set; }
        /// <summary>Stores the generic parameters.</summary>
        public List<string> GenericParameters = new();
        /// <summary>Stores the generic constraints.</summary>
        public List<GenericParameterConstraint> GenericConstraints = new();
        /// <summary>Stores the is value type.</summary>
        public bool isValueType;
        /// <summary>Gets or sets the type symbol.</summary>
        public INamedTypeSymbol TypeSymbol { get; set; }
        /// <summary>Gets or sets the can generate union dispatch.</summary>
        public bool CanGenerateUnionDispatch { get; set; }
        /// <summary>Stores the fields.</summary>
        public readonly List<LuminDataField> fields = new();
        /// <summary>Stores the parent.</summary>
        public LuminDataInfo Parent;
        /// <summary>Stores the local fields.</summary>
        public readonly List<LuminLocalFieldData> localFields = new();
        /// <summary>Stores the call back methods.</summary>
        public readonly List<(string, SerializeCallBackType, bool)> callBackMethods = new();
        /// <summary>Stores the interfaces.</summary>
        public readonly List<string> interfaces = new();
        /// <summary>Gets or sets the selected constructor.</summary>
        public LuminConstructorData SelectedConstructor { get; set; }
        /// <summary>Gets or sets the all constructors.</summary>
        public List<LuminConstructorData> AllConstructors { get; set; } = new();
        /// <summary>Stores the enable burst.</summary>
        public bool enableBurst;

        // Diagnostics belong to this exact incremental-generator input. Keeping
        // them with the generated model prevents parallel compilations/types from
        // observing or clearing each other's reports.
        internal Diagnostic[] Diagnostics = System.Array.Empty<Diagnostic>();

        /// <summary>Stores the generator type.</summary>
        public GeneratorType generatorType;

        //Union
        /// <summary>Stores the is union.</summary>
        public bool isUnion;
        /// <summary>Gets or sets the is wide tag.</summary>
        public bool IsWideTag { get; set; }
        /// <summary>Gets the union generic types.</summary>
        public Dictionary<INamedTypeSymbol, List<string>> UnionGenericTypes { get; } =
            new Dictionary<INamedTypeSymbol, List<string>>(SymbolEqualityComparer.Default);
        /// <summary>Gets the union members.</summary>
        public List<LuminUnionMemberInfo> UnionMembers { get; } = new List<LuminUnionMemberInfo>();

        //PoolRent
        /// <summary>Gets or sets the rent pool method.</summary>
        public IMethodSymbol RentPoolMethod { get; set; }
    }

    /// <summary>Provides generic parameter constraint functionality.</summary>
    public class GenericParameterConstraint
    {
        /// <summary>Gets or sets the parameter name.</summary>
        public string ParameterName { get; set; }
        /// <summary>Gets or sets the constraints.</summary>
        public List<string> Constraints { get; set; } = new();
        /// <summary>Gets or sets the is unmanaged.</summary>
        public bool IsUnmanaged { get; set; }
        /// <summary>Gets or sets the is class.</summary>
        public bool IsClass { get; set; }
        /// <summary>Gets or sets the is struct.</summary>
        public bool IsStruct { get; set; }
        /// <summary>Gets or sets the is not null.</summary>
        public bool IsNotNull { get; set; }
        /// <summary>Gets or sets the has default.</summary>
        public bool HasDefault { get; set; }
        /// <summary>Gets or sets the has new constructor.</summary>
        public bool HasNewConstructor { get; set; }
    }
}
