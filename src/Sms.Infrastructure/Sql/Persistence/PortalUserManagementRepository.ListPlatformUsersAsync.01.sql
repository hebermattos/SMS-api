SELECT Id, TenantId, Username, Email, Context, Role, IsActive, CreatedAt
            FROM dbo.PortalUsers
            WHERE Context = 'platform'
            ORDER BY Username;
