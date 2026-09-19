INSERT INTO dbo.Tenants (Id, Name, Code, TimeZoneId, IsActive, CreatedAt) VALUES (@Id, @Name, @Code, 'UTC', 1, @Now);
