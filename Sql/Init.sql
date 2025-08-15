CREATE TABLE RefreshTokens (
    Id CHAR(36) NOT NULL PRIMARY KEY, -- UUID
    TokenHash VARCHAR(128) NOT NULL UNIQUE,
    Email VARCHAR(256) NOT NULL,
    ExpiresAt DATETIME NOT NULL,
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    ReplacedByHash VARCHAR(128),
    RemoteIp VARCHAR(64),
    UserAgent VARCHAR(512),
    IsRevoked BOOLEAN NOT NULL DEFAULT FALSE,
    RevokedAt DATETIME,
    RevokeReason VARCHAR(255),
    ClientType VARCHAR(50) NOT NULL DEFAULT 'web'
);

CREATE INDEX IX_RefreshTokens_Email ON RefreshTokens (Email);
CREATE INDEX IX_RefreshTokens_ExpiresAt ON RefreshTokens (ExpiresAt);
