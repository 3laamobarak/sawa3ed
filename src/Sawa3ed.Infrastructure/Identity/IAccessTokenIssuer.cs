using Sawa3ed.Application.Auth;

namespace Sawa3ed.Infrastructure.Identity;

public interface IAccessTokenIssuer
{
    TokenResponse Issue(ApplicationUser user, AuthSession session, IList<string> roles, string refresh);
}
