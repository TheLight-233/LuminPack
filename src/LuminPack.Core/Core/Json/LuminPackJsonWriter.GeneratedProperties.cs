using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LuminPack.Code;
using LuminPack.Option;

namespace LuminPack.Core
{
    public ref partial struct LuminPackJsonWriter
    {
        /// <summary>Attempts a generated property-name write with the default JSON profile.</summary>
        /// <param name="utf8">The complete UTF-8 property head, starting with a comma.</param>
        /// <param name="utf16">The complete UTF-16 property head, starting with a comma.</param>
        /// <returns>Whether the property head fit and was written.</returns>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryWriteGeneratedPropertyName(ReadOnlySpan<byte> utf8, ReadOnlySpan<char> utf16) =>
            TryWriteGeneratedPropertyName<LuminPackDefaultOptions>(utf8, utf16);

        /// <summary>Copies a generated comma, quoted name and colon into the current window.</summary>
        /// <typeparam name="TOptions">The closed document profile.</typeparam>
        /// <param name="utf8">The complete UTF-8 property head, starting with a comma.</param>
        /// <param name="utf16">The complete UTF-16 property head, starting with a comma.</param>
        /// <returns>Whether the property head fit and was written.</returns>
        /// <remarks>Generated callers supply a correctly encoded static literal. A false result leaves the writer unchanged.</remarks>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryWriteGeneratedPropertyName<TOptions>(ReadOnlySpan<byte> utf8, ReadOnlySpan<char> utf16)
            where TOptions : struct, ILuminPackOptions
        {
            if (Unsafe.SizeOf<IntPtr>() != 8 || !HasConstantGeneratedPropertyEncoding<TOptions>())
                return false;

            bool useUtf8 = default(TOptions).StringEncoding == LuminPackStringEncoding.UTF8;
            ReadOnlySpan<byte> head = useUtf8 ? utf8 : MemoryMarshal.AsBytes(utf16);
            int skip = IsFirstElement ? (useUtf8 ? 1 : 2) : 0;
            int count = head.Length - skip;
            if (count <= 0 || !HasEnumerationCapacity(count))
                return false;

            ref byte destination = ref Current;
            if ((long)Unsafe.ByteOffset(ref MemoryMarshal.GetReference(Buffer), ref destination) < 0)
                return false;
            ref byte source = ref Unsafe.Add(ref MemoryMarshal.GetReference(head), (nint)(uint)skip);
            LuminPackMarshal.CopyBlockUnaligned(ref destination, ref source, (uint)count);
            Advance(count);
            IsFirstElement = true;
            return true;
        }

        /// <summary>Recognizes profiles whose encoding getters have no user callbacks.</summary>
        /// <typeparam name="TOptions">The closed document profile.</typeparam>
        /// <returns>Whether the encoding is fixed through the property head.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool HasConstantGeneratedPropertyEncoding<TOptions>() where TOptions : struct, ILuminPackOptions =>
            LuminPackMarshal.ConstantStringProfile<TOptions>() ||
            typeof(TOptions) == typeof(LuminPackOptions<LuminPackUtf8LengthOptions, LuminPackGeneralFormat, LuminPackDefaultDepth, LuminPackStrictJson>) ||
            typeof(TOptions) == typeof(LuminPackOptions<LuminPackUtf16LengthOptions, LuminPackGeneralFormat, LuminPackDefaultDepth, LuminPackStrictJson>);
    }
}
