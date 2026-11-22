using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace LuminPack.SystemTextJson;

/// <summary>Centralizes errors for the System.Text.Json adapter.</summary>
internal static class LuminPackExceptionHelper
{
    /// <summary>Ensures that a required constructor argument was supplied.</summary>
    /// <typeparam name="T">The argument type.</typeparam>
    /// <param name="value">The argument value.</param>
    /// <param name="parameter">The parameter name.</param>
    /// <returns>The supplied value.</returns>
    internal static T RequireArgument<T>(T? value, string parameter) where T : class =>
        value ?? throw new ArgumentNullException(parameter);

    /// <summary>Rejects a contract type that overlaps its model type.</summary>
    /// <param name="parameter">The parameter that supplies the contract.</param>
    [DoesNotReturn]
    internal static void ThrowOverlappingContract(string parameter) =>
        throw new ArgumentException("The JSON contract must be separate from the model hierarchy.", parameter);

    /// <summary>Checks the result of a JSON contract read or mapping.</summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="value">The value checked for null.</param>
    /// <param name="message">The error to report for a null value.</param>
    /// <returns>The nonnull value.</returns>
    internal static T RequireJsonValue<T>(T? value, string message) where T : notnull =>
        value ?? throw new JsonException(message);

    /// <summary>Rejects a value that cannot be represented by the contract.</summary>
    /// <param name="message">The reason for the rejection.</param>
    [DoesNotReturn]
    internal static void ThrowJsonException(string message) => throw new JsonException(message);
}
