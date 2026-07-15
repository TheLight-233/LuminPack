using System.Collections.Generic;

namespace LuminPack.Enum
{
    public class LuminDataInfo
    {
        public string className = "NewData";
        public string classNameSpace = "Your Data NameSpace";
        public bool isValueType;
        public bool enableBurst = true;
        public readonly List<LuminDataFiled> fields = new List<LuminDataFiled>();
    }
}