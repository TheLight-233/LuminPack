namespace LuminPack.Code
{
    /// <summary>Provides lumin pack code functionality.</summary>
    public static class LuminPackCode
    {
        //Non-negative values represent collection lengths; -1 represents a null collection.
        /// <summary>Stores the null collection.</summary>
        public const int NullCollection = -1;

        //Null Object
        /// <summary>Stores the null object.</summary>
        public const byte NullObject = 255;

        // Object/Union Header
        // 0~249 is member count or tag, 250~254 is unused, 255 is null
        /// <summary>Stores the wide tag.</summary>
        public const byte WideTag = 250; // for Union, 250 is wide tag
        /// <summary>Stores the reference id.</summary>
        public const byte ReferenceId = 250; // for CircularReference, 250 is referenceId marker, next VarInt id reference.

        /// <summary>Gets the null collection data.</summary>
        internal static ReadOnlySpan<byte> NullCollectionData => new byte[] { 255, 255, 255, 255 }; // -1
        /// <summary>Gets the zero collection data.</summary>
        internal static ReadOnlySpan<byte> ZeroCollectionData => new byte[] { 0, 0, 0, 0 }; // 0
    }
}
