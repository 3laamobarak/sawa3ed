using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sawa3ed.Application.Chat;
using Sawa3ed.Application.Common;
using Sawa3ed.Infrastructure.Configuration;

namespace Sawa3ed.Infrastructure.Chat;

public sealed class CompatibleChatClient(HttpClient client, IOptions<ChatOptions> options, ILogger<CompatibleChatClient> logger) : IChatClient
{
    public async Task<string> CompleteAsync(IReadOnlyList<ChatTurn> messages, CancellationToken ct)
    {
        var settings = options.Value;
        if (!settings.Enabled) throw new AppException(503, "chat_disabled", "The learning assistant is not configured yet.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(settings.TimeoutSeconds));
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(settings.BaseUrl.TrimEnd('/') + "/"), "chat/completions"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);
        request.Content = JsonContent.Create(new
        {
            model = settings.Model,
            messages = messages.Select(x => new { role = x.Role, content = x.Content }),
            max_tokens = settings.MaxOutputTokens,
            stream = false
        });
        try
        {
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Chat provider failed with HTTP {StatusCode}", (int)response.StatusCode);
                throw new AppException(502, "chat_provider_error", "The learning assistant is temporarily unavailable.");
            }
            // Enforce a real bound even when Content-Length is missing or inaccurate.
            await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
            using var body = new MemoryStream();
            var buffer = new byte[8192];
            int read;
            while ((read = await stream.ReadAsync(buffer, timeout.Token)) > 0)
            {
                if (body.Length + read > 256 * 1024) throw new JsonException("Provider payload too large.");
                body.Write(buffer, 0, read);
            }
            using var json = JsonDocument.Parse(body.ToArray());
            if (!json.RootElement.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0 ||
                !choices[0].TryGetProperty("message", out var message) || !message.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.String)
                throw new JsonException("Provider response missing content.");
            var reply = content.GetString();
            if (string.IsNullOrWhiteSpace(reply) || reply.Length > 16000) throw new JsonException("Invalid provider content.");
            return reply;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new AppException(504, "chat_timeout", "The learning assistant timed out.");
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or InvalidOperationException)
        {
            logger.LogWarning("Chat provider returned an invalid response ({ErrorType})", ex.GetType().Name);
            throw new AppException(502, "chat_provider_error", "The learning assistant is temporarily unavailable.");
        }
    }
}
