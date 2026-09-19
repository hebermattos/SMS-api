UPDATE dbo.PortalUsers
            SET IsActive = @IsActive, UpdatedAt = SYSDATETIMEOFFSET()
            WHERE Id = @Id AND Context = 'platform';
