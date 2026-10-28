using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Extensions.DependencyInjection;
using Errors = LuminPack.Code.LuminPackExceptionHelper;

namespace LuminPack.AspNetCore;

/// <summary>Declares the closed request/response DTOs accepted by MVC's LuminPack formatters.</summary>
public sealed class LuminPackMvcOptions
{
    internal readonly Dictionary<Type, IBodyCodec> Codecs = new();

    /// <summary>Gets or sets the encoded request limit, including unknown-length bodies.</summary>
    public int MaximumBodyBytes { get; set; } = LuminPackHttp.DefaultMaximumBodyBytes;

    /// <summary>Adds a closed, application-generated DTO codec.</summary>
    /// <typeparam name="T">The declared body type.</typeparam>
    /// <param name="codec">The complete record operations.</param><returns>This configuration.</returns>
    public LuminPackMvcOptions Add<T>(LuminPackHttpCodec<T> codec)
    {
        if (codec is null) Errors.ThrowArgumentNullException(nameof(codec));
        if (!Codecs.TryAdd(typeof(T), new BodyCodec<T>(codec))) Errors.ThrowArgumentException("The MVC body type already has a LuminPack codec.", nameof(codec));
        return this;
    }

    /// <summary>Invokes closed application operations at MVC's required object boundary.</summary>
    internal interface IBodyCodec
    {
        /// <summary>Decodes the complete bounded body.</summary>
        /// <param name="body">The body bytes.</param><returns>The MVC model.</returns>
        object? Read(ReadOnlyMemory<byte> body);
        /// <summary>Encodes a model of the previously selected declared type.</summary>
        /// <param name="value">The MVC model.</param><returns>The complete body.</returns>
        byte[] Write(object? value);
        /// <summary>Classifies a parser failure using the application's explicit policy.</summary>
        /// <param name="error">The failure.</param><returns>Whether it is malformed input.</returns>
        bool IsMalformed(Exception error);
    }

    /// <summary>Adapts a closed generic DTO codec without reflective invocation or type activation.</summary>
    /// <typeparam name="T">The declared DTO.</typeparam>
    private sealed class BodyCodec<T> : IBodyCodec
    {
        private readonly LuminPackHttpCodec<T> _codec;
        /// <summary>Captures the declared operations.</summary><param name="codec">The typed codec.</param>
        internal BodyCodec(LuminPackHttpCodec<T> codec) => _codec = codec;
        /// <inheritdoc/>
        public object? Read(ReadOnlyMemory<byte> body) => _codec.Deserialize(body);
        /// <inheritdoc/>
        public byte[] Write(object? value) => _codec.Serialize((T)value!);
        /// <inheritdoc/>
        public bool IsMalformed(Exception error) => _codec.IsMalformedInput(error);
    }
}

/// <summary>Installs explicit binary MVC input/output formatters without changing JSON formatters.</summary>
public static class LuminPackMvcExtensions
{
    /// <summary>Adds LuminPack negotiation for explicitly registered DTOs and application/x-luminpack.</summary>
    /// <param name="builder">The MVC registration builder.</param><param name="configure">The DTO and size-limit configuration.</param>
    /// <returns>The same MVC builder.</returns>
    public static IMvcBuilder AddLuminPack(this IMvcBuilder builder, Action<LuminPackMvcOptions> configure)
    {
        if (builder is null) Errors.ThrowArgumentNullException(nameof(builder));
        if (configure is null) Errors.ThrowArgumentNullException(nameof(configure));
        var settings = new LuminPackMvcOptions();
        configure(settings);
        if (settings.MaximumBodyBytes <= 0) Errors.ThrowArgumentOutOfRangeException(nameof(settings.MaximumBodyBytes));
        var codecs = new Dictionary<Type, LuminPackMvcOptions.IBodyCodec>(settings.Codecs);
        int maximumBodyBytes = settings.MaximumBodyBytes;
        builder.Services.Configure<MvcOptions>(options =>
        {
            options.InputFormatters.Insert(0, new LuminPackInputFormatter(codecs, maximumBodyBytes));
            options.OutputFormatters.Add(new LuminPackOutputFormatter(codecs));
        });
        return builder;
    }
}

/// <summary>Reads only explicitly selected DTOs from bounded binary request bodies.</summary>
internal sealed class LuminPackInputFormatter : InputFormatter
{
    private readonly IReadOnlyDictionary<Type, LuminPackMvcOptions.IBodyCodec> _codecs;
    private readonly int _maximumBodyBytes;

    /// <summary>Captures an immutable configuration snapshot.</summary>
    /// <param name="codecs">The closed DTO operations.</param><param name="maximumBodyBytes">The encoded request limit.</param>
    internal LuminPackInputFormatter(IReadOnlyDictionary<Type, LuminPackMvcOptions.IBodyCodec> codecs, int maximumBodyBytes)
    {
        _codecs = codecs;
        _maximumBodyBytes = maximumBodyBytes;
        SupportedMediaTypes.Add(LuminPackHttp.ContentType);
    }

    /// <inheritdoc/>
    protected override bool CanReadType(Type type) => _codecs.ContainsKey(type);

    /// <inheritdoc/>
    public override async Task<InputFormatterResult> ReadRequestBodyAsync(InputFormatterContext context)
    {
        byte[] body = await LuminPackHttp.ReadBodyAsync(context.HttpContext.Request, _maximumBodyBytes, context.HttpContext.RequestAborted).ConfigureAwait(false);
        var codec = _codecs[context.ModelType];
        try { return await InputFormatterResult.SuccessAsync(codec.Read(body)).ConfigureAwait(false); }
        catch (Exception error) when (codec.IsMalformed(error))
        {
            context.ModelState.TryAddModelError(context.ModelName, "Invalid LuminPack request body.");
            return await InputFormatterResult.FailureAsync().ConfigureAwait(false);
        }
    }
}

/// <summary>Writes only explicitly selected DTOs after serialization completes successfully.</summary>
internal sealed class LuminPackOutputFormatter : OutputFormatter
{
    private readonly IReadOnlyDictionary<Type, LuminPackMvcOptions.IBodyCodec> _codecs;

    /// <summary>Captures the closed DTO operations.</summary><param name="codecs">The configuration snapshot.</param>
    internal LuminPackOutputFormatter(IReadOnlyDictionary<Type, LuminPackMvcOptions.IBodyCodec> codecs)
    {
        _codecs = codecs;
        SupportedMediaTypes.Add(LuminPackHttp.ContentType);
    }

    /// <inheritdoc/>
    protected override bool CanWriteType(Type? type) => type is not null && _codecs.ContainsKey(type);

    /// <inheritdoc/>
    public override async Task WriteResponseBodyAsync(OutputFormatterWriteContext context)
    {
        Type type = context.ObjectType ?? context.Object?.GetType() ?? Errors.ThrowInvalidOperationException<Type>("A declared MVC body type is required.");
        if (!_codecs.TryGetValue(type, out var codec)) Errors.ThrowInvalidOperationException("The MVC body type is not registered.");
        context.HttpContext.RequestAborted.ThrowIfCancellationRequested();
        byte[] body = codec!.Write(context.Object);
        context.HttpContext.Response.ContentLength = body.Length;
        await context.HttpContext.Response.Body.WriteAsync(body, context.HttpContext.RequestAborted).ConfigureAwait(false);
    }
}
