SELECT Id, TenantId, Username, Email, Context, Role, IsActive, CreatedAt
            FROM dbo.PortalUsers
            WHERE TenantId = @TenantId AND Context = 'tenant'
            ORDER BY Username;
