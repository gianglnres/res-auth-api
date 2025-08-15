using ResAuthApi.Domain.Entities;

namespace ResAuthApi.Application.Interfaces
{
    public interface IRefreshTokenRepository
    {
        Task AddAsync(RefreshToken token);
        Task<RefreshToken?> GetByHashAsync(string hash);
        Task UpdateAsync(RefreshToken token);
        Task DeleteExpiredAsync(DateTime now);

        Task RevokeAsync(Guid tokenId, string reason);            // 🔎 Audit: set RevokedAt + reason
    }
}
