using System;

namespace LuminPack.SourceGenerator;

/// <summary>Centralizes exceptions raised by the generator implementation itself.</summary>
internal static class LuminPackExceptionHelper
{
    /// <summary>Raises the exception for argument exception.</summary>
    /// <param name="message">The message describing the failure.</param>
    /// <param name="paramName">The name of the parameter that failed validation.</param>
    public static void ThrowArgumentException(string message, string paramName = null)
        => throw new ArgumentException(message, paramName);

    /// <summary>Raises the exception for argument exception.</summary>
    /// <param name="message">The message describing the failure.</param>
    /// <param name="paramName">The name of the parameter that failed validation.</param>
    /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
    /// <returns>This method never returns a value.</returns>
    public static T ThrowArgumentException<T>(string message, string paramName = null)
        => throw new ArgumentException(message, paramName);

    /// <summary>Raises the exception for argument null exception.</summary>
    /// <param name="paramName">The name of the parameter that failed validation.</param>
    public static void ThrowArgumentNullException(string paramName)
        => throw new ArgumentNullException(paramName);

    /// <summary>Raises the exception for argument null exception.</summary>
    /// <param name="paramName">The name of the parameter that failed validation.</param>
    /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
    /// <returns>This method never returns a value.</returns>
    public static T ThrowArgumentNullException<T>(string paramName)
        => throw new ArgumentNullException(paramName);

    /// <summary>Raises the exception for invalid operation exception.</summary>
    /// <param name="message">The message describing the failure.</param>
    public static void ThrowInvalidOperationException(string message = null)
        => throw new InvalidOperationException(message);

    /// <summary>Raises the exception for invalid operation exception.</summary>
    /// <param name="message">The message describing the failure.</param>
    /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
    /// <returns>This method never returns a value.</returns>
    public static T ThrowInvalidOperationException<T>(string message = null)
        => throw new InvalidOperationException(message);

    /// <summary>Raises the exception for a lookup key that is not bound.</summary>
    /// <param name="key">The key that was not found.</param>
    /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
    /// <returns>This method never returns a value.</returns>
    public static T ThrowKeyNotFoundException<T>(string key)
        => throw new System.Collections.Generic.KeyNotFoundException(key);
}
