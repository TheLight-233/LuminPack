using System;

namespace LuminPack.SourceGenerator.Next.Diagnostics;

/// <summary>Centralizes generator argument failures and verification failures.</summary>
internal static class LuminPackExceptionHelper
{
    /// <summary>Rejects an argument while preserving its diagnostic and parameter name.</summary>
    /// <typeparam name="T">The result type required by the caller.</typeparam>
    /// <param name="message">The reason the argument is invalid.</param>
    /// <param name="parameter">The argument name when the failure identifies a parameter.</param>
    /// <returns>Never returns a value.</returns>
    internal static T ThrowArgument<T>(string message, string? parameter = null) => throw new ArgumentException(message, parameter);

    /// <summary>Rejects an invalid internal buffer range.</summary>
    /// <param name="parameter">The invalid parameter name.</param>
    internal static void ThrowArgumentOutOfRange(string parameter) => throw new ArgumentOutOfRangeException(parameter);

    /// <summary>Rejects an unsupported argument in a value-producing expression.</summary>
    /// <typeparam name="T">The required result type.</typeparam>
    /// <param name="parameter">The invalid parameter name.</param>
    /// <returns>Never returns a value.</returns>
    internal static T ThrowArgumentOutOfRange<T>(string parameter) => throw new ArgumentOutOfRangeException(parameter);

    /// <summary>Stops verification with the concrete failed contract.</summary>
    /// <param name="message">The failed contract and its context.</param>
    /// <returns>Never returns a value.</returns>
    internal static int ThrowInvalidOperation(string message) => throw new InvalidOperationException(message);

    /// <summary>Stops rendering when a required operation or dependency is unavailable.</summary>
    /// <typeparam name="T">The result type required by the caller.</typeparam>
    /// <param name="message">The unavailable operation and its context.</param>
    /// <returns>Never returns a value.</returns>
    internal static T ThrowInvalidOperation<T>(string message) => throw new InvalidOperationException(message);
}
