INSERT INTO RefreshTokens
    (Id, UserId, Username, TenantId, Context, Role, TokenHash, ExpiresAt, CreatedAt)
VALUES
    (@Id, @UserId, @Username, @TenantId, @Context, @Role, @TokenHash, @ExpiresAt, CURRENT_TIMESTAMP);
