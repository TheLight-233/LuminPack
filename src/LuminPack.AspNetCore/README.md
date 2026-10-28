# LuminPack.AspNetCore

Optional ASP.NET Core integration using application-selected concrete LuminPack operations. It supplies MVC binary input/output formatters, Minimal API request reading/results, and explicit codecs for StringValues, StringSegment, HostString, PathString, QueryString and FragmentString. Core has no ASP.NET dependency.

StringValues stores a count and complete Core strings, retaining ordering, null elements and empty strings. Its private object/string/array storage is not serialized. A record accepts at most 65,536 values. Text wrappers store their public logical Value, including null/empty; StringSegment encodes the selected slice instead of its entire backing string. Binary strings use the declaration's default Core profile. Initialize these value codecs with `LuminAspNetCore.Register()`; direct codec calls need no registration. Generated DTO fields can select `LuminPackValue<T>` as described in [the shared adapter](../LuminPack.Platforms/README.md).

Register only explicitly selected DTOs with MVC:

```csharp
using LuminPack;
using LuminPack.AspNetCore;

var codec = new LuminPackHttpCodec<Message>(
    value => LuminPackSerializer.Serialize(value),
    bytes =>
    {
        Message? value = null;
        LuminPackSerializer.Deserialize(bytes.Span, ref value);
        return value!;
    });

builder.Services.AddControllers().AddLuminPack(options =>
{
    options.MaximumBodyBytes = 1024 * 1024;
    options.Add(codec);
});
```

The consuming application runs the existing generator against its DTO and concrete serializer calls. No reflective serializer or input-selected CLR type activation is introduced. Existing JSON formatters keep their default preference. Clients select binary output with `Accept: application/x-luminpack`; binary input uses that Content-Type. Keep all serializer options consistent between both endpoints.

Minimal API request and response entries use the same codec:

```csharp
app.MapPost("/messages", async (HttpRequest request) =>
{
    Message value = await request.ReadLuminPackAsync(codec);
    return LuminPackHttp.Result(value, codec);
});
```

The request reader rejects unsupported media types, limits known and unknown/chunked encoded bodies, honors RequestAborted and application cancellation, and leaves the request stream open. Oversized input raises a standard BadHttpRequestException with status 413; unsupported media type uses 415; classified invalid payload uses 400. Use the application's normal HTTP exception boundary for Minimal endpoints. MVC converts classified parser failures into model-state errors and retains application exceptions. Error messages do not include parser details.

The default classifier recognizes Core's bounds/record exception and FormatException/InvalidDataException/EndOfStreamException. Other application exceptions propagate. A codec constructor can accept an explicit classifier for additional application parser errors. The application reader must validate a complete record; this adapter cannot infer bytes consumed by an arbitrary generated delegate. Explicit platform root codecs already reject trailing bytes.

The default encoded body limit is one MiB. It is not a universal allocation, recursion or CPU budget for the decoded DTO. Select a suitable schema and application limits; request size alone does not make arbitrary input safe. Responses serialize successfully before headers/body are published. The transport does not create authentication, encryption, compression or HTTP type identifiers.

Build against the ASP.NET Core shared framework and an already built net8.0 Core DLL. The project never rebuilds Core/generator. It is an optional source project, not a published NuGet package. The focused tests exercise actual MVC/Minimal requests, negotiation, retained JSON behavior, malformed/oversized/unknown-length input, cancellation, callback identity and generated Custom headers.
