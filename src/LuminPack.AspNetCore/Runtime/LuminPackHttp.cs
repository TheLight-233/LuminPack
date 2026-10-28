using System.Buffers;
using LuminPack.Code;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;
using Errors = LuminPack.Code.LuminPackExceptionHelper;

namespace LuminPack.AspNetCore;

/// <summary>Provides bounded binary HTTP request reading and Minimal API responses.</summary>
public static class LuminPackHttp
{
    /// <summary>Names the explicit LuminPack binary media type.</summary>
    public const string ContentType = "application/x-luminpack";
    /// <summary>Limits a request body to one MiB unless the application selects another limit.</summary>
    public const int DefaultMaximumBodyBytes = 1_048_576;

    /// <summary>Reads one bounded request body using an application-selected concrete codec.</summary>
    /// <typeparam name="T">The declared request DTO.</typeparam>
    /// <param name="request">The HTTP request.</param><param name="codec">The closed DTO codec.</param>
    /// <param name="maximumBodyBytes">The maximum encoded body length, including unknown/chunked lengths.</param>
    /// <param name="cancellationToken">Additional application cancellation, combined with RequestAborted.</param>
    /// <returns>The decoded DTO.</returns>
    public static async ValueTask<T> ReadLuminPackAsync<T>(this HttpRequest request, LuminPackHttpCodec<T> codec,
        int maximumBodyBytes = DefaultMaximumBodyBytes, CancellationToken cancellationToken = default)
    {
        if (request is null) Errors.ThrowArgumentNullException(nameof(request));
        if (codec is null) Errors.ThrowArgumentNullException(nameof(codec));
        if (!MediaTypeHeaderValue.TryParse(request.ContentType, out var mediaType) ||
            !string.Equals(mediaType.MediaType.Value, ContentType, StringComparison.OrdinalIgnoreCase))
            Errors.Rethrow(LuminPackHttpExceptionHelper.UnsupportedMediaType());
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(request.HttpContext.RequestAborted, cancellationToken);
        byte[] bytes = await ReadBodyAsync(request, maximumBodyBytes, linked.Token).ConfigureAwait(false);
        try { return codec.Deserialize(bytes); }
        catch (Exception error) when (codec.IsMalformedInput(error))
        {
            Errors.Rethrow(LuminPackHttpExceptionHelper.InvalidPayload(error));
            return default!;
        }
    }

    /// <summary>Creates a binary Minimal API response with a closed DTO codec.</summary>
    /// <typeparam name="T">The response DTO.</typeparam>
    /// <param name="value">The response.</param><param name="codec">The concrete serializer.</param><returns>The binary result.</returns>
    public static IResult Result<T>(T value, LuminPackHttpCodec<T> codec) => new LuminPackHttpResult<T>(value, codec);

    /// <summary>Copies at most the configured body limit plus one detection byte, without trusting Content-Length.</summary>
    /// <param name="request">The request stream owner.</param><param name="maximumBodyBytes">The application limit.</param>
    /// <param name="cancellationToken">The combined cancellation signal.</param><returns>The bounded complete body.</returns>
    internal static async Task<byte[]> ReadBodyAsync(HttpRequest request, int maximumBodyBytes, CancellationToken cancellationToken)
    {
        if (maximumBodyBytes <= 0) Errors.ThrowArgumentOutOfRangeException(nameof(maximumBodyBytes));
        if (request.ContentLength > maximumBodyBytes) Errors.Rethrow(LuminPackHttpExceptionHelper.TooLarge());
        int capacity = (int)Math.Min(request.ContentLength ?? 0, maximumBodyBytes);
        using var body = new MemoryStream(capacity);
        byte[] buffer = ArrayPool<byte>.Shared.Rent(Math.Min(maximumBodyBytes, 16_384));
        try
        {
            while (true)
            {
                int readLength = (int)Math.Min(buffer.Length, (long)maximumBodyBytes - body.Length + 1);
                int count = await request.Body.ReadAsync(buffer.AsMemory(0, readLength), cancellationToken).ConfigureAwait(false);
                if (count == 0) break;
                if (body.Length + count > maximumBodyBytes) Errors.Rethrow(LuminPackHttpExceptionHelper.TooLarge());
                body.Write(buffer, 0, count);
            }
            return body.ToArray();
        }
        finally { ArrayPool<byte>.Shared.Return(buffer, clearArray: true); }
    }
}

/// <summary>Writes a complete binary response only after serialization succeeds.</summary>
/// <typeparam name="T">The declared DTO.</typeparam>
internal sealed class LuminPackHttpResult<T> : IResult
{
    private readonly T _value;
    private readonly LuminPackHttpCodec<T> _codec;

    /// <summary>Creates a typed response.</summary>
    /// <param name="value">The DTO.</param><param name="codec">Its concrete codec.</param>
    internal LuminPackHttpResult(T value, LuminPackHttpCodec<T> codec)
    {
        _value = value;
        _codec = codec ?? Errors.ThrowArgumentNullException<LuminPackHttpCodec<T>>(nameof(codec));
    }

    /// <inheritdoc/>
    public async Task ExecuteAsync(HttpContext httpContext)
    {
        httpContext.RequestAborted.ThrowIfCancellationRequested();
        byte[] body = _codec.Serialize(_value);
        httpContext.Response.ContentType = LuminPackHttp.ContentType;
        httpContext.Response.ContentLength = body.Length;
        await httpContext.Response.Body.WriteAsync(body, httpContext.RequestAborted).ConfigureAwait(false);
    }
}
