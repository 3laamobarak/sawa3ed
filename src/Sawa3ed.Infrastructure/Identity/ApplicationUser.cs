using Microsoft.AspNetCore.Identity;

namespace Sawa3ed.Infrastructure.Identity;

public sealed class ApplicationUser : IdentityUser
{
    public string DisplayName { get; set; } = "";
    public bool IsDeleted { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
