SELECT Id, TenantId, Username, Email, PasswordHash, PasswordSalt, PasswordIterations,
                   Context, Role, IsActive
            FROM dbo.PortalUsers
            WHERE Username = @Username
              AND Context = @Context
              AND IsActive = 1
              AND (Context = 'platform' OR EXISTS
                  (SELECT 1 FROM dbo.Tenants t WHERE t.Id = TenantId AND t.IsActive = 1));
