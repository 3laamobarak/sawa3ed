using Sawa3ed.Api.Configuration;
using Sawa3ed.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddInfrastructure(builder.Environment);
builder.AddApi();

var app = builder.Build();
if (await app.RunStartupCommandsAsync(args)) return;
app.UseApi();
await app.RunAsync();

public partial class Program;
