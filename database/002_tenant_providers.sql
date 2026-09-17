CREATE TABLE dbo.TenantSmsProviders
(
    Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_TenantSmsProviders PRIMARY KEY,
    TenantId UNIQUEIDENTIFIER NOT NULL,
    Provider NVARCHAR(50) NOT NULL,
    AccountId NVARCHAR(200) NOT NULL,
    ApiSecret NVARCHAR(1000) NOT NULL,
    FromNumber NVARCHAR(32) NULL,
    IsDefault BIT NOT NULL CONSTRAINT DF_TenantSmsProviders_IsDefault DEFAULT (0),
    IsActive BIT NOT NULL CONSTRAINT DF_TenantSmsProviders_IsActive DEFAULT (1),
    CreatedAt DATETIMEOFFSET NOT NULL,
    UpdatedAt DATETIMEOFFSET NULL,
    CONSTRAINT FK_TenantSmsProviders_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(Id)
);
GO

CREATE UNIQUE INDEX UX_TenantSmsProviders_Tenant_Provider
    ON dbo.TenantSmsProviders(TenantId, Provider);
GO

CREATE UNIQUE INDEX UX_TenantSmsProviders_Default
    ON dbo.TenantSmsProviders(TenantId)
    WHERE IsDefault = 1 AND IsActive = 1;
GO
