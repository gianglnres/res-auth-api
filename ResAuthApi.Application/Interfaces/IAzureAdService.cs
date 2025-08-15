using System.Security.Claims;

namespace ResAuthApi.Application.Interfaces
{
    public interface IAzureAdService
    {
        Task<string?> ExchangeCodeForIdToken(string code);
        IEnumerable<Claim> ExtractUserClaims(string idToken);
    }
}
