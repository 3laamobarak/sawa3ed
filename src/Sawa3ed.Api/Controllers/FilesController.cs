using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Sawa3ed.Api.Contracts.Files;
using Sawa3ed.Application.Abstractions;
using Sawa3ed.Application.Auth;
using Sawa3ed.Application.Common;
using Sawa3ed.Application.Files;

namespace Sawa3ed.Api.Controllers;

[ApiController, Route("api/v1/files"), Authorize]
[EnableRateLimiting("api")]
public sealed class FilesController(IFileService files) : ControllerBase
{
    private string UserId => User.FindFirst("sub")!.Value;
    [HttpPost, Authorize(Policy = Permissions.UploadFiles), EnableRateLimiting("upload")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(105_000_000), RequestFormLimits(MultipartBodyLengthLimit = 105_000_000)]
    public async Task<IActionResult> Upload([FromForm] UploadForm form, CancellationToken ct)
    {
        if (form.RelativePaths is { Count: > 0 } && form.RelativePaths.Count != form.Files.Count)
            throw AppException.Invalid("Supply one relative path per file, in the same order.");
        var uploads = form.Files.Select((file, index) => new UploadFile(file.FileName,
            form.RelativePaths is { Count: > 0 } ? form.RelativePaths[index] : null, file.Length, file.OpenReadStream())).ToArray();
        try { return Ok(await files.UploadAsync(UserId, uploads, ct)); }
        finally { foreach (var upload in uploads) await upload.Content.DisposeAsync(); }
    }
    [HttpGet]
    public Task<Page<FileResponse>> List([FromQuery, Range(1, 100000)] int page = 1, [FromQuery, Range(1, 100)] int pageSize = 20, CancellationToken ct = default)
        => files.ListAsync(UserId, page, pageSize, ct);
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Download(Guid id, CancellationToken ct)
    {
        var file = await files.DownloadAsync(UserId, id, ct);
        Response.Headers.CacheControl = "private, no-store";
        Response.Headers.XContentTypeOptions = "nosniff";
        return File(file.Content, file.ContentType, file.Name, enableRangeProcessing: true);
    }
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await files.DeleteAsync(UserId, id, ct);
        return NoContent();
    }
}
