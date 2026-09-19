INSERT INTO dbo.Tenants (Id, Name, TimeZoneId, IsActive, CreatedAt)
            VALUES (@Id, @Name, 'UTC', 1, @CreatedAt);
