using MimeKit;

namespace Sawa3ed.Infrastructure.Email;

internal static class EmailMessageFactory
{
    public static MimeMessage Create(OutboundEmail email, string from)
    {
        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(from));
        message.To.Add(MailboxAddress.Parse(email.To));
        message.Subject = email.Subject;
        message.Body = new TextPart("plain") { Text = email.Body };
        return message;
    }
}
