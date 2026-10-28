using LuminPack.Attribute;

namespace LuminPack.Code
{
    /// <summary>Provides lumin raw array data functionality.</summary>
    [Preserve]
    public sealed class LuminRawArrayData
    {
        /// <summary>Stores the length.</summary>
        public uint Length;
        /// <summary>Stores the rank.</summary>
        public uint Rank;
        /// <summary>Stores the data.</summary>
        public byte Data;
    }
}
