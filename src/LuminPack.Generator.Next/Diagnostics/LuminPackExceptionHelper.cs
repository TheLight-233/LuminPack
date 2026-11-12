using System;

namespace LuminPack.SourceGenerator.Next.Diagnostics;

/// <summary>Centralizes generator argument failures and verification failures.</summary>
internal static class LuminPackExceptionHelper
{
    /// <summary>Rejects an invalid internal buffer range.</summary>
    /// <param name="parameter">The invalid parameter name.</param>
    internal static void ThrowArgumentOutOfRange(string parameter) => throw new ArgumentOutOfRangeException(parameter);

    /// <summary>Stops verification with the concrete failed contract.</summary>
    /// <param name="message">The failed contract and its context.</param>
    /// <returns>Never returns a value.</returns>
    internal static int ThrowInvalidOperation(string message) => throw new InvalidOperationException(message);
}
