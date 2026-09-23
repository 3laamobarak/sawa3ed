using System.ComponentModel.DataAnnotations;

namespace Sawa3ed.Application.Auth;

public sealed record EmailRequest([System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.EmailAddress,
    System.ComponentModel.DataAnnotations.MaxLength(254)] string Email);
