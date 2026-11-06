using System.Buffers;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using LuminPack.Code;
using LuminPack.Option;
using LuminPack.Utility;

namespace LuminPack.Core;

/// <summary>Provides allocation-free assembly-qualified name writing through the existing text kernels.</summary>
#if NET8_0_OR_GREATER
public ref partial struct LuminPackJsonWriter
#else
public unsafe ref partial struct LuminPackJsonWriter
#endif
{
    /// <summary>Writes a non-null type name after removing its established assembly qualifiers.</summary>
    /// <param name="fullName">The validated assembly-qualified name.</param>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public void WriteTypeName(string fullName) => WriteTypeName<LuminPackDefaultOptions>(fullName);

    /// <summary>Writes a compact type name without constructing a replacement string.</summary>
    /// <typeparam name="TOptions">The compiled encoding and recording profile.</typeparam>
    /// <param name="fullName">The validated assembly-qualified name.</param>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void WriteTypeName<TOptions>(string fullName) where TOptions : struct, ILuminPackOptions
    {
        if (!LuminPackTypeNameText.FindRemoval(fullName.AsSpan(), out int start, out int end))
        {
            Write<TOptions>(fullName);
            return; }
        char[]? rented = null;
        Span<char> scratch = fullName.Length <= 512 ? stackalloc char[fullName.Length] : (rented = ArrayPool<char>.Shared.Rent(fullName.Length));
        try
        {
            int length = LuminPackTypeNameText.Compact(fullName.AsSpan(), scratch, start, end);
            WriteStringSnapshot<TOptions>(scratch.Slice(0, length));
        }
        finally
        {
            if (rented is not null)
                ArrayPool<char>.Shared.Return(rented); }
    }
}
