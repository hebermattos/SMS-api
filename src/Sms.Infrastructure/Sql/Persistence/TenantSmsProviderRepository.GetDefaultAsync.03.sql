SELECT TenantId, Provider, AccountId, ApiSecret, FromNumber, IsDefault, IsActive, Settings
FROM dbo.TenantSmsProviders
WHERE TenantId=@TenantId AND IsDefault=1 AND IsActive=1;
