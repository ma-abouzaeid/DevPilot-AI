namespace DevPilotAI.Application.Common.Interfaces;

public interface IJwtTokenGenerator
{
    string GenerateAccessToken(int userId, string email, string name, IEnumerable<string> roles);
}
