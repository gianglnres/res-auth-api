namespace ResAuthApi.Domain.Entities
{
    public class RefreshToken
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string TokenHash { get; set; } = default!; // SHA256 hash của token
        public string Email { get; set; } = default!;
        public DateTime ExpiresAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? RevokedAt { get; set; }    // nếu đã bị revoke
        public string? ReplacedByHash { get; set; } // hash của token thay thế (rotation)
        public string? RemoteIp { get; set; }       // optional
        public string? UserAgent { get; set; }      // optional
        public bool IsRevoked { get; set; }
        public string? RevokeReason { get; set; }         // lý do thu hồi
        public string? ClientType { get; set; }
    }
}
