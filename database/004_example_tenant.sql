-- Development-only bootstrap tenant. The API client secret is "example-secret-change-me".
IF NOT EXISTS (SELECT 1 FROM dbo.Tenants WHERE Id='11111111-1111-4111-8111-111111111111')
BEGIN
 INSERT dbo.Tenants(Id,Name,IsActive,CreatedAt) VALUES('11111111-1111-4111-8111-111111111111',N'Example Tenant',1,SYSDATETIMEOFFSET());
END
GO
