UPDATE dbo.TenantSmsProviders
SET FromNumber = LTRIM(RTRIM(FromNumber))
WHERE FromNumber IS NOT NULL;
GO

CREATE UNIQUE INDEX UX_TenantSmsProviders_CallbackRoute
    ON dbo.TenantSmsProviders(Provider, AccountId, FromNumber)
    WHERE IsActive = 1 AND FromNumber IS NOT NULL;
GO
