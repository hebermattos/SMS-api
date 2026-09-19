SELECT Id, TenantId, Username, Email, Context, Role, IsActive, CreatedAt
FROM PortalUsers
WHERE TenantId=@TenantId AND Context='tenant'
ORDER BY Username;
