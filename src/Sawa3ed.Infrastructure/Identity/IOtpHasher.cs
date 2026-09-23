namespace Sawa3ed.Infrastructure.Identity;

public interface IOtpHasher
{
    string Hash(Guid challengeId, string code);
    bool Verify(Guid challengeId, string code, string expectedHash);
}
