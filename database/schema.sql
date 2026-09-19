SET QUOTED_IDENTIFIER ON;
GO

CREATE TABLE dbo.PlatformAdministrators
(
    Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_PlatformAdministrators PRIMARY KEY,
    Username NVARCHAR(100) COLLATE Latin1_General_100_CI_AS NOT NULL CONSTRAINT UQ_PlatformAdministrators_Username UNIQUE,
    Email NVARCHAR(320) COLLATE Latin1_General_100_CI_AS NOT NULL,
    PasswordHash VARBINARY(32) NOT NULL,
    PasswordSalt VARBINARY(32) NOT NULL,
    PasswordIterations INT NOT NULL CONSTRAINT CK_PlatformAdministrators_Iterations CHECK (PasswordIterations >= 100000),
    IsActive BIT NOT NULL CONSTRAINT DF_PlatformAdministrators_IsActive DEFAULT (1),
    CreatedAt DATETIMEOFFSET NOT NULL
);
GO

CREATE TABLE dbo.PortalUsers
(
    Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_PortalUsers PRIMARY KEY,
    TenantId UNIQUEIDENTIFIER NULL,
    Username NVARCHAR(100) COLLATE Latin1_General_100_CI_AS NOT NULL,
    Email NVARCHAR(320) COLLATE Latin1_General_100_CI_AS NOT NULL,
    PasswordHash VARBINARY(32) NOT NULL,
    PasswordSalt VARBINARY(32) NOT NULL,
    PasswordIterations INT NOT NULL CONSTRAINT CK_PortalUsers_Iterations CHECK (PasswordIterations >= 100000),
    Context NVARCHAR(20) NOT NULL,
    Role NVARCHAR(20) NOT NULL,
    IsActive BIT NOT NULL CONSTRAINT DF_PortalUsers_IsActive DEFAULT (1),
    CreatedAt DATETIMEOFFSET NOT NULL,
    UpdatedAt DATETIMEOFFSET NULL,
    CONSTRAINT CK_PortalUsers_Context CHECK (Context IN ('tenant', 'platform')),
    CONSTRAINT CK_PortalUsers_Role CHECK (Role IN ('user', 'administrator')),
    CONSTRAINT CK_PortalUsers_TenantContext CHECK
    (
        (Context = 'tenant' AND TenantId IS NOT NULL)
        OR (Context = 'platform' AND TenantId IS NULL)
    )
);
GO
CREATE UNIQUE INDEX UX_PlatformAdministrators_Email ON dbo.PlatformAdministrators(Email);
GO

CREATE UNIQUE INDEX UX_PortalUsers_PlatformEmail
    ON dbo.PortalUsers(Email)
    WHERE Context = 'platform';
GO
CREATE UNIQUE INDEX UX_PortalUsers_TenantEmail
    ON dbo.PortalUsers(TenantId, Email)
    WHERE Context = 'tenant';
GO
CREATE UNIQUE INDEX UX_PortalUsers_PlatformUsername
    ON dbo.PortalUsers(Username)
    WHERE Context = 'platform';
GO
CREATE UNIQUE INDEX UX_PortalUsers_TenantUsername
    ON dbo.PortalUsers(TenantId, Username)
    WHERE Context = 'tenant';
GO
CREATE INDEX IX_PortalUsers_TenantId ON dbo.PortalUsers(TenantId) WHERE TenantId IS NOT NULL;
GO

CREATE TABLE dbo.Tenants
(
    Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Tenants PRIMARY KEY,
    Name NVARCHAR(200) NOT NULL,
    TimeZoneId NVARCHAR(100) NOT NULL CONSTRAINT DF_Tenants_TimeZoneId DEFAULT ('UTC'),
    IsActive BIT NOT NULL CONSTRAINT DF_Tenants_IsActive DEFAULT (1),
    CreatedAt DATETIMEOFFSET NOT NULL
);
GO

ALTER TABLE dbo.PortalUsers
    ADD CONSTRAINT FK_PortalUsers_Tenants
        FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(Id);
GO

CREATE TABLE dbo.SmsMessages
(
    Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_SmsMessages PRIMARY KEY,
    TenantId UNIQUEIDENTIFIER NOT NULL,
    [From] NVARCHAR(256) NOT NULL,
    [To] NVARCHAR(256) NOT NULL,
    Body NVARCHAR(MAX) NOT NULL,
    Provider NVARCHAR(50) NOT NULL,
    ProviderMessageId NVARCHAR(200) NULL,
    Direction INT NOT NULL,
    Status INT NOT NULL,
    CreatedAt DATETIMEOFFSET NOT NULL,
    UpdatedAt DATETIMEOFFSET NULL,
    CONSTRAINT FK_SmsMessages_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(Id),
    CONSTRAINT UQ_SmsMessages_Tenant_Id UNIQUE (TenantId, Id)
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
    CONSTRAINT FK_SmsMessageStatusHistory_TenantMessage
        FOREIGN KEY (TenantId, MessageId) REFERENCES dbo.SmsMessages(TenantId, Id)
);
GO
CREATE INDEX IX_SmsMessageStatusHistory_Tenant_Message_CreatedAt ON dbo.SmsMessageStatusHistory(TenantId, MessageId, CreatedAt);
GO
