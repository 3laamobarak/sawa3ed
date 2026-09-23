namespace Sawa3ed.Application.Abstractions;

public interface IApplicationReadiness
{
    Task<bool> IsReadyAsync(CancellationToken ct);
}
