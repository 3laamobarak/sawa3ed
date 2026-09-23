namespace Sawa3ed.Infrastructure.Configuration;

public sealed class EmailOptions
{
    public string Host { get; set; } = "";
    public int Port { get; set; } = 587;
    public string UserName { get; set; } = "";
    public string Password { get; set; } = "";
    public string From { get; set; } = "noreply@example.test";
    public bool UseDevelopmentPickup { get; set; }
    public string PickupPath { get; set; } = "App_Data/mail";
}
