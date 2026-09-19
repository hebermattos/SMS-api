SELECT Id, TenantId, Username, PasswordHash, PasswordSalt, PasswordIterations,
                   Context, Role, IsActive
            FROM dbo.PortalUsers
            WHERE Id = @Id
              AND IsActive = 1
              AND (Context = 'platform' OR EXISTS
                  (SELECT 1 FROM dbo.Tenants t WHERE t.Id = TenantId AND t.IsActive = 1));
