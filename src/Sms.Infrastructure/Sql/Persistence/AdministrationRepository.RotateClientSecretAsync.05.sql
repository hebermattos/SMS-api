UPDATE dbo.ApiClients SET SecretHash=@Hash, SecretSalt=@Salt, SecretIterations=@Iterations, UpdatedAt=@Now
            OUTPUT INSERTED.ClientId WHERE TenantId=@TenantId AND Id=@ClientId;
