using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Sawa3ed.Application.Auth;
using Sawa3ed.Application.Abstractions;
using Sawa3ed.Application.Chat;

namespace Sawa3ed.Api.Controllers;

[ApiController, Route("api/v1/chat"), Authorize(Policy = Permissions.Chat)]
[EnableRateLimiting("chat")]
public sealed class ChatController(IChatService chat) : ControllerBase
{
    private string UserId => User.FindFirst("sub")!.Value;
    [HttpPost("messages")]
    public Task<ChatResponse> Send(ChatRequest request, CancellationToken ct) => chat.SendAsync(UserId, request, ct);
    [HttpGet("{conversationId:guid}"), EnableRateLimiting("api")]
    public Task<Page<ChatMessageResponse>> History(Guid conversationId, [FromQuery, Range(1, 100000)] int page = 1,
        [FromQuery, Range(1, 100)] int pageSize = 20, CancellationToken ct = default) => chat.HistoryAsync(UserId, conversationId, page, pageSize, ct);
    [HttpDelete("{conversationId:guid}")]
    public async Task<IActionResult> Delete(Guid conversationId, CancellationToken ct)
    {
        await chat.DeleteAsync(UserId, conversationId, ct);
        return NoContent();
    }
}
