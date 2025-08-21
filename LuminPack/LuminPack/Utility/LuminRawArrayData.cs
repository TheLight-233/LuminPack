using LuminPack.Attribute;

namespace LuminPack.Code
{
    [Preserve]
    public sealed class LuminRawArrayData
    {
        public uint Length;
        public uint Rank;
        public byte Data;
    }
}