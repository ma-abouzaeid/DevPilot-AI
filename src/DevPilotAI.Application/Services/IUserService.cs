namespace DevPilotAI.Application.Services;

public interface IUserService
{
    Task DeleteAccountAsync(int userId, CancellationToken cancellationToken = default);
}
