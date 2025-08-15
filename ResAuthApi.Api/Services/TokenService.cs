using Microsoft.Extensions.Caching.Memory;
using Microsoft.IdentityModel.Tokens;
using ResAuthApi.Api.Utils;
using ResAuthApi.Application.Interfaces;
using ResAuthApi.Domain.Entities;
using Serilog;
using StackExchange.Redis;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;

namespace ResAuthApi.Api.Services
{
    public class TokenService : ITokenService
    {
        private readonly RsaSecurityKey _signingKey;
        private readonly IConfiguration _cfg;
        private readonly IRefreshTokenRepository _repo;
        private readonly IDatabase _redisDb;

        public TokenService(RsaSecurityKey signingKey, IConfiguration cfg, IRefreshTokenRepository repo, IConnectionMultiplexer redis)
        {
            _signingKey = signingKey;
            _cfg = cfg;
            _repo = repo;
            _redisDb = redis.GetDatabase();
        }

        public string GenerateInternalToken(IEnumerable<Claim> claims, int expiresInSeconds = 3600)
        {
            var email = claims.FirstOrDefault(c => c.Type == "email")?.Value;
            var cacheKey = $"access_token_{email}";

            // 1. Kiểm tra Redis cache
            var cachedToken = _redisDb.StringGet(cacheKey);
            if (!cachedToken.IsNullOrEmpty)
            {
                Log.Information("Read access token from Redis cache {cachedToken}", cachedToken);
                return cachedToken!;
            }

            // 2. Tạo token mới
            var creds = new SigningCredentials(_signingKey, SecurityAlgorithms.RsaSha256);
            var token = new JwtSecurityToken(
                issuer: _cfg["Jwt:Issuer"],
                audience: null,
                claims: claims,
                expires: DateTime.Now.AddSeconds(expiresInSeconds),
                signingCredentials: creds
            );

            var newToken = new JwtSecurityTokenHandler().WriteToken(token);

            // 3. Lưu vào Redis với TTL
            _redisDb.StringSet(cacheKey, newToken, TimeSpan.FromSeconds(expiresInSeconds));

            return newToken;
        }

        public async Task<string> CreateRefreshTokenAsync(string email, TimeSpan lifetime, string? ip = null, string? ua = null, string? clientType = "web")
        {
            var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
            var hash = TokenHasher.ComputeHash(raw);

            var entity = new RefreshToken
            {
                TokenHash = hash,
                Email = email,
                CreatedAt = DateTime.Now,
                ExpiresAt = DateTime.Now.Add(lifetime),
                RemoteIp = ip,
                UserAgent = ua,
                ClientType = clientType,
                IsRevoked = false,
                RevokedAt = null,
                RevokeReason = null
            };
            await _repo.AddAsync(entity);
            return raw; // trả raw cho client (đặt vào cookie HttpOnly)
        }

        public async Task<RefreshToken?> FindByRawTokenAsync(string raw)
        {
            var hash = TokenHasher.ComputeHash(raw);
            return await _repo.GetByHashAsync(hash);
        }

        public async Task<string> RotateAsync(RefreshToken existing, TimeSpan lifetime)
        {
            var newRaw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
            var newHash = TokenHasher.ComputeHash(newRaw);

            existing.RevokedAt = DateTime.Now;
            existing.ReplacedByHash = newHash;

            // Lưu token mới như 1 record mới (đơn giản)
            var newEntity = new RefreshToken
            {
                TokenHash = newHash,
                Email = existing.Email,
                CreatedAt = DateTime.Now,
                ExpiresAt = DateTime.Now.Add(lifetime),
                RemoteIp = existing.RemoteIp,
                UserAgent = existing.UserAgent,
                IsRevoked = true,                
                RevokedAt = DateTime.Now,
                RevokeReason = "rotated"
            };

            await _repo.AddAsync(newEntity);
            await _repo.UpdateAsync(existing);

            return newRaw;
        }

        public async Task RevokeAsync(RefreshToken token, string reason = "logout")
        {
            token.RevokedAt = DateTime.Now;
            token.IsRevoked = true;
            token.RevokeReason = reason;

            await _repo.UpdateAsync(token);
        }
    }
}
