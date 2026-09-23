using Microsoft.AspNetCore.Identity;
using Sawa3ed.Application.Common;

namespace Sawa3ed.Infrastructure.Identity;

internal static class IdentityResultGuard
{
    internal static void Ensure(IdentityResult result)
    {
        if (!result.Succeeded) throw AppException.Invalid(string.Join(" ", result.Errors.Select(e => e.Description)));
    }
}
