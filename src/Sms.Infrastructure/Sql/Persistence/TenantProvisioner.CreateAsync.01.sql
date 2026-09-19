INSERT INTO dbo.ApiClients
                    (Id, TenantId, ClientId, SecretHash, SecretSalt, SecretIterations, IsActive, CreatedAt)
                VALUES
                    (@Id, @TenantId, @ClientId, @SecretHash, @SecretSalt, @SecretIterations, 1, @Now);
