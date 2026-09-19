SELECT Name, TimeZoneId FROM dbo.Tenants WHERE Id=@TenantId AND IsActive=1;
SELECT Provider AS Name, FromNumber, IsDefault FROM dbo.TenantSmsProviders
WHERE TenantId=@TenantId AND IsActive=1 ORDER BY IsDefault DESC, Provider;
