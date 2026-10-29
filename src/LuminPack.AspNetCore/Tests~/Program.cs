using System.Net;
using System.Net.Http.Headers;
using LuminPack;
using LuminPack.AspNetCore;
using LuminPack.Attribute;
using LuminPack.Platforms;
using LuminPack.Platforms.Tests;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;

LuminAspNetCore.Register();
LuminAspNetCore.Register();
RegistrationContract.Verify();
CodecContract.Check(LuminAspNetCore.StringValuesCodec, default, "Default header");
CodecContract.Check(LuminAspNetCore.StringValuesCodec, new StringValues("one"), "Single header");
CodecContract.Check(LuminAspNetCore.StringValuesCodec, new StringValues(new string?[] { "one", null, "", "two" }), "Header values");
CodecContract.Check(LuminAspNetCore.StringValuesCodec, new StringValues(new string?[] { "\u4E2D\u6587", "\uD83D\uDE80", null, "\u00E9" }), "Unicode header values");
CodecContract.Check(LuminAspNetCore.StringSegmentCodec, default, "Null segment");
CodecContract.Check(LuminAspNetCore.StringSegmentCodec, new StringSegment("prefix_value_tail", 7, 5), "Segment slice");
CodecContract.Check(LuminAspNetCore.HostStringCodec, new HostString("example.com:8080"), "Host");
CodecContract.Check(LuminAspNetCore.PathStringCodec, new PathString("/api/example"), "Path");
CodecContract.Check(LuminAspNetCore.QueryStringCodec, new QueryString("?a=1&a=2"), "Query");
CodecContract.Check(LuminAspNetCore.FragmentStringCodec, new FragmentString("#section"), "Fragment");
await HttpContracts.RunAsync();
Console.WriteLine($"Passed ASP.NET Core contracts; {CodecContract.Samples} framework value samples and real MVC/Minimal HTTP requests.");

/// <summary>Checks media negotiation, bounded streams, malformed bodies, cancellation and callback propagation.</summary>
internal static class HttpContracts
{
    internal static readonly LuminPackHttpCodec<WebPayload> Codec = new(
        static value => LuminPackSerializer.Serialize(value),
        static bytes => { WebPayload? value = null; LuminPackSerializer.Deserialize(bytes.Span, ref value); return value!; });

    /// <summary>Runs only this extension's transport and value contracts.</summary>
    /// <returns>A task completing after all HTTP contracts pass.</returns>
    internal static async Task RunAsync()
    {
        var source = new WebPayload { Number = 137, Header = new StringValues(new string?[] { "a", null, "b" }) };
        byte[] bytes = Codec.Serialize(source);
        var options = new WebHostBuilder().ConfigureServices(services =>
            services.AddControllers().AddApplicationPart(typeof(EchoController).Assembly)
                .AddLuminPack(settings => { settings.MaximumBodyBytes = 512; settings.Add(Codec); }))
            .Configure(app =>
            {
                // TestServer exposes application exceptions; this boundary mirrors HTTP status handling in the host.
                app.Use(async (context, next) =>
                {
                    try { await next(); }
                    catch (BadHttpRequestException error) { context.Response.StatusCode = error.StatusCode; }
                });
                app.UseRouting();
                app.UseEndpoints(endpoints =>
                {
                    endpoints.MapControllers();
                    endpoints.MapPost("/minimal", async context =>
                    {
                        var value = await context.Request.ReadLuminPackAsync(Codec, 512);
                        await LuminPackHttp.Result(value, Codec).ExecuteAsync(context);
                    });
                });
            });
        using var server = new TestServer(options);
        using var client = server.CreateClient();
        foreach (string path in new[] { "/echo", "/minimal" })
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = BinaryContent(bytes) };
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(LuminPackHttp.ContentType));
            using var response = await client.SendAsync(request);
            CodecContract.Assert(response.StatusCode == HttpStatusCode.OK && response.Content.Headers.ContentType?.MediaType == LuminPackHttp.ContentType, "Binary HTTP negotiation failed.");
            var restored = Codec.Deserialize(await response.Content.ReadAsByteArrayAsync());
            CodecContract.Assert(restored.Number == source.Number && restored.Header.Value.Count == 3 && restored.Header.Value[1] is null, "HTTP DTO/Custom header round trip failed.");
            using var malformed = await client.PostAsync(path, BinaryContent(new byte[] { 0 }));
            CodecContract.Assert(malformed.StatusCode == HttpStatusCode.BadRequest, "Malformed HTTP body was not 400.");
            using var tooLarge = await client.PostAsync(path, BinaryContent(new byte[513]));
            CodecContract.Assert(tooLarge.StatusCode == HttpStatusCode.RequestEntityTooLarge, "Oversized HTTP body was not 413.");
        }
        using (var unsupported = await client.PostAsync("/minimal", new StringContent("plain text")))
            CodecContract.Assert(unsupported.StatusCode == HttpStatusCode.UnsupportedMediaType, "Unsupported media type was accepted.");
        using (var json = await client.PostAsync("/echo", new StringContent("{\"number\":19}", System.Text.Encoding.UTF8, "application/json")))
            CodecContract.Assert(json.StatusCode == HttpStatusCode.OK && json.Content.Headers.ContentType?.MediaType == "application/json", "Existing JSON formatter changed.");
        var context = NewContext(new byte[513]);
        context.Request.ContentLength = null;
        bool rejected = false;
        try { await context.Request.ReadLuminPackAsync(Codec, 512); }
        catch (BadHttpRequestException error) when (error.StatusCode == 413) { rejected = true; }
        CodecContract.Assert(rejected, "Unknown-length body bypassed the limit.");
        context = NewContext(bytes);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        context.RequestAborted = cancelled.Token;
        rejected = false;
        try { await context.Request.ReadLuminPackAsync(Codec); } catch (OperationCanceledException) { rejected = true; }
        CodecContract.Assert(rejected, "Request cancellation was ignored.");
        var expected = new InvalidOperationException("Application callback sentinel.");
        var callbackCodec = new LuminPackHttpCodec<WebPayload>(_ => bytes, _ => { System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw(expected); return default!; });
        rejected = false;
        try { await NewContext(bytes).Request.ReadLuminPackAsync(callbackCodec); }
        catch (InvalidOperationException actual) { rejected = ReferenceEquals(actual, expected); }
        CodecContract.Assert(rejected, "Application exception was hidden or replaced.");
        var writeFailure = new LuminPackHttpCodec<WebPayload>(_ => { System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw(expected); return Array.Empty<byte>(); }, _ => source);
        var failedResponse = NewContext(bytes);
        failedResponse.Response.Body = new MemoryStream();
        rejected = false;
        try { await LuminPackHttp.Result(source, writeFailure).ExecuteAsync(failedResponse); }
        catch (InvalidOperationException actual) { rejected = ReferenceEquals(actual, expected); }
        CodecContract.Assert(rejected && failedResponse.Response.ContentLength is null && failedResponse.Response.Body.Length == 0,
            "A failing response serializer published output or hid the exception.");
        var retainedStream = NewContext(bytes);
        await retainedStream.Request.ReadLuminPackAsync(Codec);
        CodecContract.Assert(retainedStream.Request.Body.CanRead, "Request stream ownership changed.");
        foreach (int count in new[] { -1, int.MaxValue })
        {
            rejected = false;
            try { LuminAspNetCore.StringValuesCodec.Deserialize(BitConverter.GetBytes(count)); } catch { rejected = true; }
            CodecContract.Assert(rejected, "Forged header count was accepted.");
        }
    }

    /// <summary>Creates a binary request content without a text encoding.</summary>
    /// <param name="bytes">The complete record.</param><returns>The request content.</returns>
    private static ByteArrayContent BinaryContent(byte[] bytes)
    {
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue(LuminPackHttp.ContentType);
        return content;
    }

    /// <summary>Creates an isolated HTTP context over a caller-owned memory stream.</summary>
    /// <param name="bytes">The body.</param><returns>The test context.</returns>
    private static DefaultHttpContext NewContext(byte[] bytes)
    {
        var context = new DefaultHttpContext();
        context.Request.ContentType = LuminPackHttp.ContentType;
        context.Request.Body = new MemoryStream(bytes);
        return context;
    }
}

/// <summary>Contains an ordinary scalar and an explicitly selected framework value contract.</summary>
[LuminPackable]
public partial class WebPayload
{
    public int Number;
    public LuminPackValue<StringValues> Header;
}

/// <summary>Exercises the real MVC formatter pipeline.</summary>
[ApiController]
[Route("echo")]
public sealed class EchoController : ControllerBase
{
    /// <summary>Returns the declared binary or JSON DTO through normal content negotiation.</summary>
    /// <param name="value">The decoded request.</param><returns>The same DTO.</returns>
    [HttpPost]
    public WebPayload Echo([FromBody] WebPayload value) => value;
}
