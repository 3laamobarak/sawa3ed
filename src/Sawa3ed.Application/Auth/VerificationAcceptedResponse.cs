namespace Sawa3ed.Application.Auth;

public sealed record VerificationAcceptedResponse
{
    public string Message => "If the account is eligible, a verification email will arrive shortly.";
}
