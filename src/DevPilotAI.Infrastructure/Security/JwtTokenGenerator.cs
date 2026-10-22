using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using DevPilotAI.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;

namespace DevPilotAI.Infrastructure.Security;

public class JwtTokenGenerator : IJwtTokenGenerator
{
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment? _environment;

    public JwtTokenGenerator(IConfiguration configuration, IHostEnvironment? environment = null)
    {
        _configuration = configuration;
        _environment = environment;
    }

    public string GenerateAccessToken(int userId, string email, string name, IEnumerable<string> roles)
    {
        var envName = _environment?.EnvironmentName 
            ?? _configuration["ASPNETCORE_ENVIRONMENT"] 
            ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        var isProduction = string.Equals(envName, "Production", StringComparison.OrdinalIgnoreCase);

        var secret = JwtSecurityValidator.GetValidatedSecret(_configuration, isProduction);
        var issuer = JwtSecurityValidator.GetIssuer(_configuration);
        var audience = JwtSecurityValidator.GetAudience(_configuration);
        var expiryMinutes = _configuration.GetValue<int>("JwtSettings:ExpiryMinutes", 60);

        var key = JwtSecurityValidator.GetSecurityKey(secret);
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(JwtRegisteredClaimNames.Email, email),
            new(ClaimTypes.Email, email),
            new(JwtRegisteredClaimNames.Name, name),
            new(ClaimTypes.Name, name),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var rolesList = roles.ToList();
        if (rolesList.Count == 0)
        {
            rolesList.Add("Developer");
        }

        foreach (var role in rolesList)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddMinutes(expiryMinutes),
            Issuer = issuer,
            Audience = audience,
            SigningCredentials = credentials
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var securityToken = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(securityToken);
    }
}
