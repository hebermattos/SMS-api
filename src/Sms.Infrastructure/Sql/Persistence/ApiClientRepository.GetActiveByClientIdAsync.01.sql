SELECT c.TenantId, c.ClientId, c.SecretHash, c.SecretSalt, c.SecretIterations
            FROM dbo.ApiClients c INNER JOIN dbo.Tenants t ON t.Id=c.TenantId
            WHERE c.ClientId = @ClientId AND c.IsActive = 1 AND t.IsActive = 1;
