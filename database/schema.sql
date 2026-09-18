SET QUOTED_IDENTIFIER ON;
GO

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
CREATE UNIQUE INDEX UX_SmsMessages_Tenant_Provider_Message ON dbo.SmsMessages(TenantId, Provider, ProviderMessageId) WHERE ProviderMessageId IS NOT NULL;
GO

CREATE TABLE dbo.TenantSmsProviders
(
    Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_TenantSmsProviders PRIMARY KEY,
    TenantId UNIQUEIDENTIFIER NOT NULL,
    Provider NVARCHAR(50) NOT NULL,
    AccountId NVARCHAR(200) NOT NULL,
    ApiSecret NVARCHAR(1000) NOT NULL,
    FromNumber NVARCHAR(32) NULL,
    Settings NVARCHAR(MAX) NULL,
    IsDefault BIT NOT NULL CONSTRAINT DF_TenantSmsProviders_IsDefault DEFAULT (0),
    IsActive BIT NOT NULL CONSTRAINT DF_TenantSmsProviders_IsActive DEFAULT (1),
    CreatedAt DATETIMEOFFSET NOT NULL,
    UpdatedAt DATETIMEOFFSET NULL,
    CONSTRAINT FK_TenantSmsProviders_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(Id)
);
GO
CREATE UNIQUE INDEX UX_TenantSmsProviders_Tenant_Provider ON dbo.TenantSmsProviders(TenantId, Provider);
GO
CREATE UNIQUE INDEX UX_TenantSmsProviders_Default ON dbo.TenantSmsProviders(TenantId) WHERE IsDefault=1 AND IsActive=1;
GO
CREATE UNIQUE INDEX UX_TenantSmsProviders_CallbackRoute ON dbo.TenantSmsProviders(Provider, AccountId, FromNumber) WHERE IsActive=1 AND FromNumber IS NOT NULL;
GO

CREATE TABLE dbo.ApiClients
(
    Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_ApiClients PRIMARY KEY,
    TenantId UNIQUEIDENTIFIER NOT NULL,
    ClientId NVARCHAR(100) NOT NULL,
    SecretHash VARBINARY(32) NOT NULL,
    SecretSalt VARBINARY(32) NOT NULL,
    SecretIterations INT NOT NULL,
    IsActive BIT NOT NULL CONSTRAINT DF_ApiClients_IsActive DEFAULT (1),
    CreatedAt DATETIMEOFFSET NOT NULL,
    UpdatedAt DATETIMEOFFSET NULL,
    CONSTRAINT FK_ApiClients_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(Id),
    CONSTRAINT CK_ApiClients_SecretIterations CHECK (SecretIterations >= 100000)
);
GO
CREATE UNIQUE INDEX UX_ApiClients_ClientId ON dbo.ApiClients(ClientId);
GO
CREATE INDEX IX_ApiClients_TenantId ON dbo.ApiClients(TenantId);
GO

CREATE TABLE dbo.SmsMessageStatusHistory
(
    Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_SmsMessageStatusHistory PRIMARY KEY,
    TenantId UNIQUEIDENTIFIER NOT NULL,
    MessageId UNIQUEIDENTIFIER NOT NULL,
    Status INT NOT NULL,
    CreatedAt DATETIMEOFFSET NOT NULL,
    CONSTRAINT FK_SmsMessageStatusHistory_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(Id),
    CONSTRAINT FK_SmsMessageStatusHistory_Messages FOREIGN KEY (MessageId) REFERENCES dbo.SmsMessages(Id)
);
GO
CREATE INDEX IX_SmsMessageStatusHistory_Tenant_Message_CreatedAt ON dbo.SmsMessageStatusHistory(TenantId, MessageId, CreatedAt);
GO
