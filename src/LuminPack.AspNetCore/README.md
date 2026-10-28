<div align="center">

# 🌐 LuminPack.AspNetCore

**Binary request and response bodies for ASP.NET Core MVC and Minimal APIs**

![Optional](https://img.shields.io/badge/module-optional-8B5CF6?style=flat-square) ![net8.0](https://img.shields.io/badge/target-net8.0-512BD4?style=flat-square&logo=dotnet&logoColor=white) ![ASP.NET Core](https://img.shields.io/badge/ASP.NET%20Core-512BD4?style=flat-square) ![Source only](https://img.shields.io/badge/NuGet-source%20only-lightgrey?style=flat-square)

**English** · [简体中文](README.cn.md)

Part of [LuminPack](../../README.md) · [all platform modules](../LuminPack.Platforms/README.md)

[Features](#-features) · [MVC](#-mvc) · [Minimal APIs](#-minimal-apis) · [Errors & limits](#-errors--limits) · [HTTP value types](#-http-value-types) · [Building](#-building)

</div>

---

Optional ASP.NET Core integration built on concrete LuminPack operations that the application selects itself. Core has no ASP.NET dependency.

## ✨ Features

- 📥 **MVC** binary input and output formatters
- ⚡ **Minimal API** request reading and results
- 🧾 **HTTP value codecs** for `StringValues`, `StringSegment`, `HostString`, `PathString`, `QueryString` and `FragmentString`
- 🔒 **No reflection** — no reflective serializer and no CLR type activation chosen by the input

## 🧱 MVC

Register only the DTOs you explicitly select:

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

- The application runs the existing generator against its DTO and the concrete serializer calls.
- Existing JSON formatters keep their default preference.
- Clients ask for binary output with `Accept: application/x-luminpack`; binary input uses the same `Content-Type`.
- Keep all serializer options identical on both endpoints.

## ⚡ Minimal APIs

Request and response entries use the same codec:

```csharp
app.MapPost("/messages", async (HttpRequest request) =>
{
    Message value = await request.ReadLuminPackAsync(codec);
    return LuminPackHttp.Result(value, codec);
});
```

## 🚦 Errors & limits

The request reader rejects unsupported media types, limits both known-length and unknown-length/chunked bodies, honors `RequestAborted` and application cancellation, and leaves the request stream open.

| Situation | Result |
| --- | --- |
| Body larger than the limit | `BadHttpRequestException` with status **413** |
| Unsupported media type | **415** |
| Classified invalid payload | **400** |

- **Minimal APIs** — use the application's normal HTTP exception boundary.
- **MVC** — classified parser failures become model-state errors; application exceptions are kept. Error messages never include parser details.
- **Classifier** — by default it recognizes Core's bounds/record exception plus `FormatException`, `InvalidDataException` and `EndOfStreamException`; other application exceptions propagate. A codec constructor can take an explicit classifier for additional application parser errors.
- **Complete records** — the application reader must validate a complete record; the adapter cannot infer how many bytes an arbitrary generated delegate consumed. Explicit platform root codecs already reject trailing bytes.
- **Responses** — they serialize successfully before headers and body are published.

> [!WARNING]
> The default limit of **one MiB** applies to the encoded body. It is not an allocation, recursion or CPU budget for the decoded DTO — pick a suitable schema and application limits, because request size alone does not make arbitrary input safe. The transport adds no authentication, encryption, compression or HTTP type identifiers.

## 🧾 HTTP value types

| Type | Wire record |
| --- | --- |
| `StringValues` | A count plus complete Core strings, keeping order, null elements and empty strings. Its private object/string/array storage is not serialized. At most 65,536 values per record. |
| `StringSegment` | Only the selected slice, not the whole backing string |
| `HostString` · `PathString` · `QueryString` · `FragmentString` | The public logical `Value`, including null and empty |

Binary strings use the declaration's default Core profile. Initialize these value codecs with `LuminAspNetCore.Register()`; direct codec calls need no registration. Generated DTO fields can use `LuminPackValue<T>` as described in the [shared adapter](../LuminPack.Platforms/README.md).

## 🔨 Building

Build against the ASP.NET Core shared framework and an already built **net8.0** Core DLL. The project never rebuilds Core or the generator, and it is an optional source project rather than a published NuGet package.

The focused tests drive real MVC and Minimal API requests: negotiation, unchanged JSON behavior, malformed/oversized/unknown-length input, cancellation, callback identity and generated Custom headers.
