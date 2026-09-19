UPDATE dbo.PortalUsers
            SET PasswordHash = @Hash, PasswordSalt = @Salt,
                PasswordIterations = @Iterations, UpdatedAt = SYSDATETIMEOFFSET()
            WHERE Id = @Id AND Context = 'platform';
