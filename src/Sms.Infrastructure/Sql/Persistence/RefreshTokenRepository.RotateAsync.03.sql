INSERT INTO RefreshTokens
    (Id, UserId, Username, TenantId, Context, Role, IsPlatformAdministrator, TokenHash, ExpiresAt, CreatedAt)
VALUES
    (@Id, @UserId, @Username, @TenantId, @Context, @Role, @IsPlatformAdministrator, @TokenHash, @ExpiresAt, CURRENT_TIMESTAMP);
