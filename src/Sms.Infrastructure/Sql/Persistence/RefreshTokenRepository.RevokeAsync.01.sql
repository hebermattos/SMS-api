UPDATE RefreshTokens
SET RevokedAt = CURRENT_TIMESTAMP
WHERE TokenHash = @TokenHash
  AND UserId = @UserId
  AND RevokedAt IS NULL;
