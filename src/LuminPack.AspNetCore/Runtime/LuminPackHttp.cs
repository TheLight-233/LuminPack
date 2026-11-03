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
        using var body = await ReadBodyAsync(request, maximumBodyBytes, linked.Token).ConfigureAwait(false);
        try { return codec.Deserialize(body.Memory); }
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
    /// <param name="cancellationToken">The combined cancellation signal.</param><returns>The bounded complete body in pooled storage, which the caller disposes.</returns>
    /// <remarks>The body is read straight into rented storage that grows by doubling, so no staging stream or final copy is allocated.</remarks>
    internal static async Task<PooledBody> ReadBodyAsync(HttpRequest request, int maximumBodyBytes, CancellationToken cancellationToken)
    {
        if (maximumBodyBytes <= 0) Errors.ThrowArgumentOutOfRangeException(nameof(maximumBodyBytes));
        if (request.ContentLength > maximumBodyBytes) Errors.Rethrow(LuminPackHttpExceptionHelper.TooLarge());
        // One byte beyond the limit detects an oversized body whose length was not declared.
        long ceiling = (long)maximumBodyBytes + 1;
        byte[] buffer = ArrayPool<byte>.Shared.Rent((int)Math.Min(Math.Max(request.ContentLength ?? 0, 16_384), ceiling));
        int length = 0;
        try
        {
            while (true)
            {
                if (length == buffer.Length)
                {
                    byte[] grown = ArrayPool<byte>.Shared.Rent((int)Math.Min((long)length * 2, ceiling));
                    Buffer.BlockCopy(buffer, 0, grown, 0, length);
                    ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
                    buffer = grown;
                }
                int readLength = (int)Math.Min(buffer.Length - length, ceiling - length);
                int count = await request.Body.ReadAsync(buffer.AsMemory(length, readLength), cancellationToken).ConfigureAwait(false);
                if (count == 0) break;
                length += count;
                if (length > maximumBodyBytes) Errors.Rethrow(LuminPackHttpExceptionHelper.TooLarge());
            }
        }
        catch (Exception error)
        {
            ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
            Errors.Rethrow(error);
        }
        return new PooledBody(buffer, length);
    }

    /// <summary>Owns a request body in rented storage until the request has been decoded.</summary>
    internal struct PooledBody : IDisposable
    {
        private byte[]? _buffer;
        private readonly int _length;

        /// <summary>Wraps rented storage.</summary>
        /// <param name="buffer">The rented array.</param><param name="length">The body length.</param>
        internal PooledBody(byte[] buffer, int length)
        {
            _buffer = buffer;
            _length = length;
        }

        /// <summary>Gets the body.</summary>
        internal readonly ReadOnlyMemory<byte> Memory => _buffer is null ? default : new ReadOnlyMemory<byte>(_buffer, 0, _length);

        /// <summary>Clears and returns the storage, which may hold request data.</summary>
        public void Dispose()
        {
            var buffer = _buffer;
            _buffer = null;
            if (buffer is not null) ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
        }
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
