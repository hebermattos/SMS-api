SELECT TenantId, Provider, AccountId, ApiSecret, FromNumber, IsDefault, IsActive, Settings
            FROM dbo.TenantSmsProviders WHERE TenantId=@TenantId ORDER BY Provider;
