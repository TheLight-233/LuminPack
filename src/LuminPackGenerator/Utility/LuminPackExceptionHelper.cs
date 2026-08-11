using System;

namespace LuminPack.SourceGenerator;

/// <summary>Centralizes exceptions raised by the generator implementation itself.</summary>
internal static class LuminPackExceptionHelper
{
    public static void ThrowArgumentException(string message, string paramName = null)
        => throw new ArgumentException(message, paramName);

    public static T ThrowArgumentException<T>(string message, string paramName = null)
        => throw new ArgumentException(message, paramName);

    public static void ThrowArgumentNullException(string paramName)
        => throw new ArgumentNullException(paramName);

    public static T ThrowArgumentNullException<T>(string paramName)
        => throw new ArgumentNullException(paramName);

    public static void ThrowInvalidOperationException(string message = null)
        => throw new InvalidOperationException(message);

    public static T ThrowInvalidOperationException<T>(string message = null)
        => throw new InvalidOperationException(message);
}
