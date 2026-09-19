UPDATE dbo.PortalUsers
            SET IsActive = @IsActive, UpdatedAt = SYSDATETIMEOFFSET()
            WHERE TenantId = @TenantId AND Id = @Id AND Context = 'tenant';
