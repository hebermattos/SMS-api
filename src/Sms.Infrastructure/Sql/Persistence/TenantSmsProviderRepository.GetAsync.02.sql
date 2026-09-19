SELECT TenantId, Provider, AccountId, ApiSecret, FromNumber, IsDefault, IsActive, Settings
FROM dbo.TenantSmsProviders
WHERE TenantId=@TenantId AND Provider=@Provider AND IsActive=1;
