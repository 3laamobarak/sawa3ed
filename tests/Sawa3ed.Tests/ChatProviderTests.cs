using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Sawa3ed.Application.Chat;
using Sawa3ed.Application.Common;
using Sawa3ed.Infrastructure.Chat;
using Sawa3ed.Infrastructure.Configuration;

namespace Sawa3ed.Tests;

public sealed class ChatProviderTests
{
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => send(request, ct);
    }
    private static CompatibleChatClient Create(HttpClient http, bool enabled = true) => new(http,
        Options.Create(new ChatOptions { Enabled = enabled, BaseUrl = "https://provider.example/v1/", ApiKey = "test-only-key", Model = "test-model", TimeoutSeconds = 1 }),
        NullLogger<CompatibleChatClient>.Instance);

    [Fact]
    public async Task Compatible_provider_payload_and_reply_are_processed()
    {
        string? payload = null;
        using var handler = new Handler(async (request, ct) =>
        {
            Assert.Equal("https://provider.example/v1/chat/completions", request.RequestUri!.ToString());
            payload = await request.Content!.ReadAsStringAsync(ct);
            return new(HttpStatusCode.OK) { Content = new StringContent("{\"choices\":[{\"message\":{\"content\":\"Useful answer\"}}]}") };
        });
        using var http = new HttpClient(handler);
        Assert.Equal("Useful answer", await Create(http).CompleteAsync([new ChatTurn("user", "Question")], default));
        Assert.Contains("\"role\":\"user\"", payload);
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests, "secret provider error")]
    [InlineData(HttpStatusCode.OK, "not json")]
    [InlineData(HttpStatusCode.OK, "{\"choices\":[]}")]
    public async Task Provider_failures_are_explicit_and_do_not_leak_the_provider_body(HttpStatusCode status, string body)
    {
        using var handler = new Handler((_, _) => Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) }));
        using var http = new HttpClient(handler);
        var exception = await Assert.ThrowsAsync<AppException>(() => Create(http).CompleteAsync([new("user", "Question")], default));
        Assert.Equal(502, exception.StatusCode);
        Assert.DoesNotContain(body, exception.Message);
    }

    [Fact]
    public async Task Timeout_is_reported_as_504()
    {
        using var handler = new Handler(async (_, ct) =>
        {
            await Task.Delay(Timeout.Infinite, ct);
            return new(HttpStatusCode.OK);
        });
        using var http = new HttpClient(handler);
        var exception = await Assert.ThrowsAsync<AppException>(() => Create(http).CompleteAsync([new("user", "Question")], default));
        Assert.Equal(504, exception.StatusCode);
    }
}
