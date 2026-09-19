INSERT dbo.PortalUsers
                    (Id, TenantId, Username, Email, PasswordHash, PasswordSalt, PasswordIterations,
                     Context, Role, IsActive, CreatedAt)
                VALUES
                    (@Id, NULL, @Username, @Email, @PasswordHash, @PasswordSalt, @PasswordIterations,
                     'platform', @Role, 1, SYSDATETIMEOFFSET());
