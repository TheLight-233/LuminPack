using Microsoft.AspNetCore.Http;

namespace LuminPack.AspNetCore;

/// <summary>Owns framework-specific HTTP exceptions so Core remains independent of ASP.NET Core.</summary>
internal static class LuminPackHttpExceptionHelper
{
    /// <summary>Creates the standard HTTP exception for an oversized body.</summary>
    /// <returns>The framework failure.</returns>
    internal static Exception TooLarge() => new BadHttpRequestException("The LuminPack request body exceeds the configured limit.", StatusCodes.Status413PayloadTooLarge);

    /// <summary>Creates an unsupported binary media-type error.</summary>
    /// <returns>The framework failure.</returns>
    internal static Exception UnsupportedMediaType() => new BadHttpRequestException("Expected application/x-luminpack.", StatusCodes.Status415UnsupportedMediaType);

    /// <summary>Creates a malformed body error without exposing parser details in the public message.</summary>
    /// <param name="error">The original parser failure.</param><returns>The HTTP failure.</returns>
    internal static Exception InvalidPayload(Exception error) => new BadHttpRequestException("Invalid LuminPack request body.", StatusCodes.Status400BadRequest, error);
}
