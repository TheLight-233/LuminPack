using System.Collections.Generic;

namespace LuminPack.Code
{
    public sealed class LuminDataField
    {
        public LuminFiledType Type;
        public string Name = "NewField";
        public bool isProperty = false;

        public string TypeName;
        
        public string NameSpace = "Your Data NameSpace";
        
        [System.NonSerialized]
        //ForUnity
        public bool ShowNestedFields = false;
        
        
        public LuminDataType FieldType;
        
        //Enum
        public LuminEnumFieldType EnumType;
        
        //Generics
        public List<LuminGenericsType> GenericType = [];
        
        //Class Or Struct
        public List<LuminDataField> ClassFields = [];
        public List<LuminLocalFieldData> localFields = [];
        public string ClassName = "Your Class Name";
        public string ClassGenericType;
        public int ConstructParameterCount = 0;
        
        //Attribute
        public uint Order = int.MaxValue;
        public uint FixLength = int.MaxValue;
        public bool IsPrivate;
        

    }
    
}