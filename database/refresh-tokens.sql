CREATE TABLE IF NOT EXISTS RefreshTokens
(
    Id UUID PRIMARY KEY,
    UserId UUID NOT NULL,
    FamilyId UUID NOT NULL,
    Username CITEXT NOT NULL,
    TenantId UUID NULL,
    Context VARCHAR(20) NOT NULL,
    Role VARCHAR(20) NOT NULL,
    TokenHash BYTEA NOT NULL UNIQUE,
    ExpiresAt TIMESTAMPTZ NOT NULL,
    CreatedAt TIMESTAMPTZ NOT NULL,
    RevokedAt TIMESTAMPTZ NULL,
    ReplacedByHash BYTEA NULL,
    CHECK (Context IN ('tenant', 'platform')),
    CHECK (Role IN ('user', 'administrator'))
);

CREATE INDEX IF NOT EXISTS IX_RefreshTokens_FamilyId
    ON RefreshTokens(FamilyId);

CREATE INDEX IF NOT EXISTS IX_RefreshTokens_User_Active
    ON RefreshTokens(UserId, ExpiresAt DESC)
    WHERE RevokedAt IS NULL;

CREATE INDEX IF NOT EXISTS IX_RefreshTokens_ExpiresAt
    ON RefreshTokens(ExpiresAt);
