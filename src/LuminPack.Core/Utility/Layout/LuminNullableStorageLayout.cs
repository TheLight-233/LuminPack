#if NET8_0_OR_GREATER
using System.ComponentModel;

namespace LuminPack.Utility;

/// <summary>Describes the modeled storage of a rectangular nullable array element.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface ILuminNullableStorageLayout : ILuminStorageLayout
{
    /// <summary>Gets the storage offset of a data byte in a validated tagged model.</summary>
    /// <param name="tag">The existing storage model identifier.</param>
    /// <param name="dataByte">The byte's ordinal in declaration order.</param>
    /// <returns>The model-relative address of the byte.</returns>
    int ModelByteOffset(int tag, int dataByte);
}
#endif
