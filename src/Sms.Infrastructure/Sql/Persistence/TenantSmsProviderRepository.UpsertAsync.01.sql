SET XACT_ABORT ON;
            BEGIN TRANSACTION;
            -- Serialize configuration writes for this tenant, including default-provider changes.
            SELECT Id FROM dbo.Tenants WITH (UPDLOCK, HOLDLOCK) WHERE Id=@TenantId;
            IF @IsDefault=1 AND @IsActive=1
                UPDATE dbo.TenantSmsProviders SET IsDefault=0, UpdatedAt=@Now
                WHERE TenantId=@TenantId AND Provider<>@Provider AND IsDefault=1;
            UPDATE dbo.TenantSmsProviders
            SET AccountId=@AccountId,ApiSecret=@ApiSecret,FromNumber=@FromNumber,IsDefault=@IsDefault,IsActive=@IsActive,Settings=@Settings,UpdatedAt=@Now
            WHERE TenantId=@TenantId AND Provider=@Provider;
            IF @@ROWCOUNT=0
                INSERT dbo.TenantSmsProviders(Id,TenantId,Provider,AccountId,ApiSecret,FromNumber,IsDefault,IsActive,Settings,CreatedAt)
                VALUES(@Id,@TenantId,@Provider,@AccountId,@ApiSecret,@FromNumber,@IsDefault,@IsActive,@Settings,@Now);
            COMMIT TRANSACTION;
