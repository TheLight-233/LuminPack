using System;

namespace LuminPack.Build
{
    /// <summary>Centralizes postprocessor failures without referencing the serialization runtime.</summary>
    internal static class LuminPackExceptionHelper
    {
        /// <summary>Stops rewriting when a mapping or signing contract is invalid.</summary>
        /// <param name="message">The failed build contract.</param>
        internal static void ThrowInvalidOperationException(string message)
            => throw new InvalidOperationException("LuminPack IL rewrite: " + message);
    }
}
