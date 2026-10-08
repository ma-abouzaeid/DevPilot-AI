namespace DevPilotAI.Application.Common.Interfaces;

public interface ITurnstileService
{
    Task<bool> VerifyTokenAsync(string? token, string? remoteIp = null, CancellationToken cancellationToken = default);
}
