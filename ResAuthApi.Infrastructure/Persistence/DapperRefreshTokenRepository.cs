using Dapper;
using ResAuthApi.Application.Interfaces;
using ResAuthApi.Domain.Entities;

namespace ResAuthApi.Infrastructure.Persistence
{
    public class DapperRefreshTokenRepository : IRefreshTokenRepository
    {
        private readonly MySqlConnectionFactory _factory;
        public DapperRefreshTokenRepository(MySqlConnectionFactory factory) => _factory = factory;

        public async Task AddAsync(RefreshToken token)
        {
            const string sql = @"
                INSERT INTO RefreshTokens 
                (Id, TokenHash, Email, CreatedAt, ExpiresAt, RevokedAt, ReplacedByHash, RemoteIp, UserAgent, ClientType)
                VALUES (@Id, @TokenHash, @Email, @CreatedAt, @ExpiresAt, @RevokedAt, @ReplacedByHash, @RemoteIp, @UserAgent, @ClientType)";
            using var db = _factory.Create();
            await db.ExecuteAsync(sql, token);
        }

        public async Task<RefreshToken?> GetByHashAsync(string hash)
        {
            const string sql = "SELECT * FROM RefreshTokens WHERE TokenHash = @hash LIMIT 1";
            using var db = _factory.Create();
            return await db.QueryFirstOrDefaultAsync<RefreshToken>(sql, new { hash });
        }

        public async Task UpdateAsync(RefreshToken token)
        {
            const string sql = @"
                UPDATE RefreshTokens
                SET TokenHash=@TokenHash, ExpiresAt=@ExpiresAt, RevokedAt=@RevokedAt, 
                    ReplacedByHash=@ReplacedByHash, IsRevoked=@IsRevoked, RevokeReason=@RevokeReason
                WHERE Id=@Id";
            using var db = _factory.Create();
            await db.ExecuteAsync(sql, token);
        }

        public async Task DeleteExpiredAsync(DateTime now)
        {
            const string sql = "DELETE FROM RefreshTokens WHERE ExpiresAt < @now OR RevokedAt IS NOT NULL";
            using var db = _factory.Create();
            await db.ExecuteAsync(sql, new { now });
        }

        public async Task RevokeAsync(Guid tokenId, string reason)
        {
            const string sql = @"
                UPDATE RefreshTokens
                SET IsRevoked = 1,
                    RevokedAt = UTC_TIMESTAMP(),
                    RevokeReason = @Reason
                WHERE Id = @Id";
            using var db = _factory.Create();
            await db.ExecuteAsync(sql, new { Id = tokenId, Reason = reason });
        }
    }
}
