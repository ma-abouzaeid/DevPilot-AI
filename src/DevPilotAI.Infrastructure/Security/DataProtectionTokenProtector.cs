using DevPilotAI.Application.Common.Interfaces;
using Microsoft.AspNetCore.DataProtection;

namespace DevPilotAI.Infrastructure.Security;

public class DataProtectionTokenProtector : ITokenProtector
{
    private readonly IDataProtector _protector;

    public DataProtectionTokenProtector(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector("DevPilot.GitHub.Tokens.v1");
    }

    public string Protect(string plainText) => _protector.Protect(plainText);

    public string Unprotect(string protectedText) => _protector.Unprotect(protectedText);
}
