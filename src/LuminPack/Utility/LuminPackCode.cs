namespace LuminPack.Code
{
    public static class LuminPackCode
    {
        //0-*为数组长度，-1表示空集合
        public const int NullCollection = -1;

        //Null Object
        public const byte NullObject = 255;
        
        // Object/Union Header
        // 0~249 is member count or tag, 250~254 is unused, 255 is null
        public const byte WideTag = 250; // for Union, 250 is wide tag
        public const byte ReferenceId = 250; // for CircularReference, 250 is referenceId marker, next VarInt id reference.
        
        internal static ReadOnlySpan<byte> NullCollectionData => new byte[] { 255, 255, 255, 255 }; // -1
        internal static ReadOnlySpan<byte> ZeroCollectionData => new byte[] { 0, 0, 0, 0 }; // 0
    }
}