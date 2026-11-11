using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace LuminPack.Code
{
    /// <summary>Describes the type, storage, ordering, and formatting policy of one serialized member.</summary>
    public sealed class LuminDataField
    {
        /// <summary>Stores the member's immutable declaration-time serialization profile.</summary>
        internal LuminPack.SourceGenerator.CompiledOptions Options;
        /// <summary>Stores the type.</summary>
        public LuminFiledType Type;
        /// <summary>Stores the name.</summary>
        public string Name = "NewField";

        /// <summary>Stores former version-tolerant names in chronological order.</summary>
        public string[] OldNames = System.Array.Empty<string>();

        /// <summary>Gets the stable name written by the version-tolerant binary contract.</summary>
        public string SerializedName => OldNames.Length == 0 ? Name : OldNames[0];

        /// <summary>Gets the identifier.</summary>
        public string Identifier =>
            SyntaxFacts.GetKeywordKind(Name) != SyntaxKind.None ||
            SyntaxFacts.GetContextualKeywordKind(Name) != SyntaxKind.None
                ? "@" + Name
                : Name;

        /// <summary>Stores the is property.</summary>
        public bool isProperty = false;

        /// <summary>Gets or sets the full type name.</summary>
        public string FullTypeName { get; set; }

        /// <summary>The Roslyn symbol for this exact constructed field type.</summary>
        public ITypeSymbol TypeSymbol { get; set; }

        /// <summary>Gets or sets the type name.</summary>
        public string TypeName { get; set; }

        /// <summary>Stores the name space.</summary>
        public string NameSpace = "Your Data NameSpace";

        /// <summary>Stores the show nested fields.</summary>
        [System.NonSerialized]
        //ForUnity
        public bool ShowNestedFields = false;


        /// <summary>Stores the field type.</summary>
        public LuminDataType FieldType;

        //Enum
        /// <summary>Stores the enum type.</summary>
        public LuminEnumFieldType EnumType;

        //Generics
        /// <summary>Stores the generic type.</summary>
        public List<LuminGenericsType> GenericType = [];

        //Class Or Struct
        /// <summary>Stores the class fields.</summary>
        public List<LuminDataField> ClassFields = [];
        /// <summary>Stores the local fields.</summary>
        public List<LuminLocalFieldData> localFields = [];
        /// <summary>Stores the class name.</summary>
        public string ClassName = "Your Class Name";
        /// <summary>Stores the class generic type.</summary>
        public string ClassGenericType;
        /// <summary>Stores the belong class name.</summary>
        public string belongClassName;
        /// <summary>Stores the belong class field name.</summary>
        public string belongClassFieldName;
        /// <summary>Gets or sets the selected constructor.</summary>
        public LuminConstructorData SelectedConstructor { get; set; }
        /// <summary>Stores the construct parameter count.</summary>
        public int ConstructParameterCount = 0;

        //Attribute
        /// <summary>Stores the order.</summary>
        public uint Order = int.MaxValue;

        /// <summary>Stores the is private.</summary>
        public bool IsPrivate;
        /// <summary>Whether the original field requires volatile access semantics.</summary>
        public bool IsVolatile;
        /// <summary>Stores the is compress.</summary>
        public bool IsCompress;

        //PoolRent
        /// <summary>Gets or sets the rent pool method.</summary>
        public IMethodSymbol RentPoolMethod { get; set; }

    }

}
