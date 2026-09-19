SELECT Id, TenantId, [From], [To], Body, Provider, ProviderMessageId, Direction, Status, CreatedAt, UpdatedAt FROM dbo.SmsMessages WHERE TenantId = @TenantId AND Id = @Id;
