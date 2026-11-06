using System.Buffers;
using System.Buffers.Text;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using LuminPack.Code;
using LuminPack.Option;
using LuminPack.Utility;

namespace LuminPack.Core
{
#if NET8_0_OR_GREATER
    public ref partial struct LuminPackJsonReader
#else
    public unsafe ref partial struct LuminPackJsonReader
#endif
    {
        /// <summary>Counts validated array elements without advancing this reader or reading application values.</summary>
        /// <returns>The number of elements in the current array.</returns>
        public int CountArrayElements() => CountArrayElements<LuminPackDefaultOptions>();

        /// <summary>Counts array elements with the selected document profile without advancing this reader.</summary>
        /// <typeparam name="TOptions">The stateless JSON profile.</typeparam>
        /// <returns>The number of complete elements in the current array.</returns>
        public int CountArrayElements<TOptions>() where TOptions : struct, ILuminPackOptions
        {
            var scan = this;
            // Deep grammar storage is mutable; a lookahead must own its copy.
            scan._pooledLookahead = true;
            scan.DeepContainerKinds = null;
            try
            {
                if (default(TOptions).ValidateJsonSyntax && DeepContainerKinds is not null)
                {
                    scan.DeepContainerKinds = ArrayPool<byte>.Shared.Rent(DeepContainerKinds.Length);
                    DeepContainerKinds.AsSpan().CopyTo(scan.DeepContainerKinds);
                }
                scan.TryConsumeArrayStart<TOptions>();
                int count = 0;
                while (scan.Read<TOptions>())
                {
                    if (scan.CurrentTokenType == JsonTokenType.ArrayEnd)
                        return count;
                    scan.Skip<TOptions>();
                    count = checked(count + 1);
                }
                LuminPackExceptionHelper.ThrowFormatException("Unterminated JSON array");
                return 0;
            }
            finally
            {
                if (scan.DeepContainerKinds is not null)
                    ArrayPool<byte>.Shared.Return(scan.DeepContainerKinds);
            }
        }

        /// <summary>Attempts to consume array start and reports whether it succeeded.</summary>
        /// <returns>Whether the requested condition or operation succeeded.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryConsumeArrayStart()
        {
            return TryConsumeArrayStart<LuminPackDefaultOptions>();
        }

        /// <summary>Performs the consume array end step for the current operation.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ConsumeArrayEnd()
        {
            ConsumeArrayEnd<LuminPackDefaultOptions>();
        }

        /// <summary>Attempts to consume array start and reports whether it succeeded.</summary>
        /// <returns>Whether the requested condition or operation succeeded.</returns>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public bool TryConsumeArrayStart<TOptions>()
            where TOptions : struct, ILuminPackOptions
        {
            if (CurrentTokenType == JsonTokenType.ArrayStart)
                return true;
            if (Read<TOptions>() && CurrentTokenType == JsonTokenType.ArrayStart)
                return true;
            LuminPackExceptionHelper.ThrowInvalidOperationException("Expected JSON array");
            return false;
        }

        /// <summary>Performs the consume array end step for the current operation.</summary>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void ConsumeArrayEnd<TOptions>()
            where TOptions : struct, ILuminPackOptions
        {
            if (!Read<TOptions>() || CurrentTokenType != JsonTokenType.ArrayEnd)
                LuminPackExceptionHelper.ThrowFormatException("Expected the end of a JSON array");
        }
    }
}
