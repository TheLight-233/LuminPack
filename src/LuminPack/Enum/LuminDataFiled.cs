using System.Collections.Generic;

namespace LuminPack.Enum
{

    public sealed class LuminDataFiled
    {
        public LuminFiledType Type;
        public string Name = "NewField";
        
        public string NameSpace = "Your Data NameSpace";
        
        [System.NonSerialized]
        public bool ShowNestedFields = false;
        
        
        public LuminDataType FieldType;
        
        //Enum
        public LuminEnumFieldType EnumType;
        
        //Generics
        public List<LuminGenericsType> GenericType = [];
        
        //Class Or Struct
        public List<LuminDataFiled> ClassFields = [];
        public string ClassName = "Your Class Name";
        public string ClassGenericType;
    }
    
    
}