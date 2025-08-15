using ResAuthApi.Domain.Entities;
using System.Security.Claims;

namespace ResAuthApi.Application.Interfaces
{
    public interface ITokenService
    {
        string GenerateInternalToken(IEnumerable<Claim> claims, int expiresInSeconds = 3600);
        Task<string> CreateRefreshTokenAsync(string email, TimeSpan lifetime, string? ip = null, string? ua = null, string? clientType = "web");
        Task<RefreshToken?> FindByRawTokenAsync(string raw);
        Task<string> RotateAsync(RefreshToken existing, TimeSpan lifetime);
        Task RevokeAsync(RefreshToken token, string reason = "logout");
    }
}
