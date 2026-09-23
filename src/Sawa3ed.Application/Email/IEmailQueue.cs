namespace Sawa3ed.Application.Email;

public interface IEmailQueue
{
    // Stage delivery in the caller's transaction; do not contact a mail server here.
    void Enqueue(string email, string subject, string body);
}
