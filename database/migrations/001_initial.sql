CREATE TABLE dbo.Tenants
(
    Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Tenants PRIMARY KEY,
    Name NVARCHAR(200) NOT NULL,
    IsActive BIT NOT NULL CONSTRAINT DF_Tenants_IsActive DEFAULT (1),
    CreatedAt DATETIMEOFFSET NOT NULL
);
GO

CREATE TABLE dbo.SmsMessages
(
    Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_SmsMessages PRIMARY KEY,
    TenantId UNIQUEIDENTIFIER NOT NULL,
    [From] NVARCHAR(32) NOT NULL,
    [To] NVARCHAR(32) NOT NULL,
    Body NVARCHAR(MAX) NOT NULL,
    Provider NVARCHAR(50) NOT NULL,
    ProviderMessageId NVARCHAR(200) NULL,
    Direction INT NOT NULL,
    Status INT NOT NULL,
    CreatedAt DATETIMEOFFSET NOT NULL,
    UpdatedAt DATETIMEOFFSET NULL,
    CONSTRAINT FK_SmsMessages_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(Id)
);
GO

CREATE INDEX IX_SmsMessages_TenantId_CreatedAt ON dbo.SmsMessages(TenantId, CreatedAt DESC);
GO

CREATE UNIQUE INDEX UX_SmsMessages_Tenant_Provider_Message
    ON dbo.SmsMessages(TenantId, Provider, ProviderMessageId)
    WHERE ProviderMessageId IS NOT NULL;
GO
