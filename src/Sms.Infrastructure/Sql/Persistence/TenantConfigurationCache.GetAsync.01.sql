SELECT Id, Name, TimeZoneId, IsActive, CreatedAt
FROM dbo.Tenants
WHERE Id = @TenantId;

SELECT Id, ClientId, IsActive, CreatedAt
FROM dbo.ApiClients
WHERE TenantId = @TenantId
ORDER BY CreatedAt DESC, Id;

SELECT TenantId, Provider, AccountId, ApiSecret, FromNumber, IsDefault, IsActive, Settings
FROM dbo.TenantSmsProviders
WHERE TenantId = @TenantId
ORDER BY Provider;
