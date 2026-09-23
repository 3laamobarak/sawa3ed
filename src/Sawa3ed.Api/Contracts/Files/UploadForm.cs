using System.ComponentModel.DataAnnotations;

namespace Sawa3ed.Api.Contracts.Files;

public sealed class UploadForm
{
    [Required] public List<IFormFile> Files { get; set; } = [];
    public List<string>? RelativePaths { get; set; }
}
