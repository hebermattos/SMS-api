WITH tenant_lock AS
(
    SELECT pg_advisory_xact_lock(hashtextextended(@TenantId::text, 0))
    FROM Tenants
    WHERE Id=@TenantId
),
cleared_defaults AS
(
    UPDATE TenantSmsProviders
    SET IsDefault=FALSE, UpdatedAt=@Now
    WHERE TenantId=@TenantId
      AND Provider<>@Provider
      AND IsDefault
      AND @IsDefault
      AND @IsActive
      AND EXISTS (SELECT 1 FROM tenant_lock)
    RETURNING 1
),
barrier AS
(
    SELECT COUNT(*) FROM cleared_defaults
)
INSERT INTO TenantSmsProviders
    (Id, TenantId, Provider, AccountId, ApiSecret, FromNumber, IsDefault, IsActive, Settings, CreatedAt)
SELECT
    @Id, @TenantId, @Provider, @AccountId, @ApiSecret, @FromNumber, @IsDefault, @IsActive, @Settings, @Now
FROM tenant_lock
CROSS JOIN barrier
ON CONFLICT (TenantId, Provider)
DO UPDATE SET
    AccountId=EXCLUDED.AccountId,
    ApiSecret=EXCLUDED.ApiSecret,
    FromNumber=EXCLUDED.FromNumber,
    IsDefault=EXCLUDED.IsDefault,
    IsActive=EXCLUDED.IsActive,
    Settings=EXCLUDED.Settings,
    UpdatedAt=@Now;
