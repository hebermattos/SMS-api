UPDATE dbo.PortalUsers
            SET PasswordHash = @Hash, PasswordSalt = @Salt,
                PasswordIterations = @Iterations, UpdatedAt = SYSDATETIMEOFFSET()
            WHERE TenantId = @TenantId AND Id = @Id AND Context = 'tenant';
