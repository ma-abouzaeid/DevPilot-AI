namespace DevPilotAI.Application.Users;

public interface IUserService
{
    Task DeleteAccountAsync(int userId, CancellationToken cancellationToken = default);
}
