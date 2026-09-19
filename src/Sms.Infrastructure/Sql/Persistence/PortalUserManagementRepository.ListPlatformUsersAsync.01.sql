SELECT Id, TenantId, Username, Email, Context, Role, IsActive, CreatedAt
FROM PortalUsers
WHERE Context='platform'
ORDER BY Username;
