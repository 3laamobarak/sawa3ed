using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sawa3ed.Application.Auth;
using Sawa3ed.Application.Chat;
using Sawa3ed.Application.Common;
using Sawa3ed.Application.Files;
using Sawa3ed.Infrastructure.Persistence;
using Sawa3ed.Infrastructure.Storage;

namespace Sawa3ed.Tests;

public sealed class FilesAndChatTests
{
    [Theory]
    [InlineData("../secrets.pdf")]
    [InlineData("folder/../../secrets.pdf")]
    [InlineData("/etc/passwd")]
    [InlineData("C:\\secrets.pdf")]
    [InlineData("folder//test.pdf")]
    [InlineData("folder/./test.pdf")]
    public void Traversal_and_absolute_paths_are_rejected(string path) =>
        Assert.Throws<AppException>(() => FilePathPolicy.SafePath(path, true));

    [Fact]
    public void Forged_extension_is_rejected() =>
        Assert.Throws<AppException>(() => FileSignaturePolicy.DetectContentType(".png", Encoding.UTF8.GetBytes("<script>alert(1)</script>")));

    [Fact]
    public async Task Uploads_preserve_virtual_folders_and_enforce_ownership_and_soft_delete()
    {
        using var app = new ApiFactory();
        using var client = await app.StartAsync();
        var owner = await app.AccountAsync(client, Roles.Teacher);
        ApiFactory.Authenticate(client, owner.Tokens);
        using var multipart = Pdf("lesson.pdf", "skills/lesson.pdf");
        var upload = await client.PostAsync("/api/v1/files", multipart);
        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
        var record = Assert.Single((await upload.Content.ReadFromJsonAsync<FileResponse[]>())!);
        Assert.Equal("skills", record.Folder);
        Assert.Equal("application/pdf", record.ContentType);
        Assert.DoesNotContain("storageKey", await upload.Content.ReadAsStringAsync());
        var other = await app.AccountAsync(client, Roles.Admin);
        ApiFactory.Authenticate(client, other.Tokens);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/files/{record.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"/api/v1/files/{record.Id}")).StatusCode);
        ApiFactory.Authenticate(client, owner.Tokens);
        var download = await client.GetAsync($"/api/v1/files/{record.Id}");
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal("attachment", download.Content.Headers.ContentDisposition?.DispositionType);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/v1/files/{record.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/files/{record.Id}")).StatusCode);
        await using var scope = app.Services.CreateAsyncScope();
        var deleted = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Files.IgnoreQueryFilters().SingleAsync(x => x.Id == record.Id);
        Assert.True(deleted.IsDeleted);
        Assert.Equal(owner.Id, deleted.DeletedBy);
    }

    [Fact]
    public async Task Student_cannot_upload_and_invalid_file_is_not_saved()
    {
        using var app = new ApiFactory();
        using var client = await app.StartAsync();
        ApiFactory.Authenticate(client, (await app.AccountAsync(client)).Tokens);
        using var studentUpload = Pdf("test.pdf");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync("/api/v1/files", studentUpload)).StatusCode);
        ApiFactory.Authenticate(client, (await app.AccountAsync(client, Roles.Teacher)).Tokens);
        using var fakeImage = Pdf("fake.png");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/v1/files", fakeImage)).StatusCode);
        await using var scope = app.Services.CreateAsyncScope();
        Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<AppDbContext>().Files.CountAsync());
    }

    [Fact]
    public async Task Chat_preserves_roles_and_isolates_conversations_and_does_not_store_failures()
    {
        using var app = new ApiFactory();
        using var client = await app.StartAsync();
        var owner = await app.AccountAsync(client);
        ApiFactory.Authenticate(client, owner.Tokens);
        var first = await client.PostAsJsonAsync("/api/v1/chat/messages", new ChatRequest("Explain fractions"));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var conversation = (await first.Content.ReadFromJsonAsync<ChatResponse>())!;
        var second = await client.PostAsJsonAsync("/api/v1/chat/messages", new ChatRequest("Give an example", conversation.ConversationId));
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(new[] { "system", "user", "assistant", "user" }, app.Chat.LastMessages.Select(x => x.Role));
        ApiFactory.Authenticate(client, (await app.AccountAsync(client)).Tokens);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/chat/{conversation.ConversationId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync("/api/v1/chat/messages", new ChatRequest("Hi", conversation.ConversationId))).StatusCode);
        ApiFactory.Authenticate(client, owner.Tokens);
        app.Chat.Fail = true;
        Assert.Equal(HttpStatusCode.BadGateway, (await client.PostAsJsonAsync("/api/v1/chat/messages", new ChatRequest("Another question", conversation.ConversationId))).StatusCode);
        await using var scope = app.Services.CreateAsyncScope();
        Assert.Equal(4, await scope.ServiceProvider.GetRequiredService<AppDbContext>().ChatMessages.CountAsync());
    }

    [Fact]
    public async Task Deleting_a_conversation_during_a_provider_call_does_not_resurrect_it()
    {
        using var app = new ApiFactory();
        using var client = await app.StartAsync();
        ApiFactory.Authenticate(client, (await app.AccountAsync(client)).Tokens);
        var first = await client.PostAsJsonAsync("/api/v1/chat/messages", new ChatRequest("First question"));
        var conversation = (await first.Content.ReadFromJsonAsync<ChatResponse>())!;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        app.Chat.BeforeReply = async ct => { entered.SetResult(); await release.Task.WaitAsync(ct); };
        var pending = client.PostAsJsonAsync("/api/v1/chat/messages", new ChatRequest("Second question", conversation.ConversationId));
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/v1/chat/{conversation.ConversationId}")).StatusCode);
        release.SetResult();
        Assert.Equal(HttpStatusCode.Conflict, (await pending).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/chat/{conversation.ConversationId}")).StatusCode);
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(0, await db.ChatMessages.CountAsync());
        Assert.Equal(2, await db.ChatMessages.IgnoreQueryFilters().CountAsync());
    }

    private static MultipartFormDataContent Pdf(string name, string? relative = null)
    {
        var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent("%PDF-1.7\nSample test payload\n%%EOF"u8.ToArray()), "Files", name);
        if (relative is not null) form.Add(new StringContent(relative), "RelativePaths");
        return form;
    }
}
