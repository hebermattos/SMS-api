SET QUOTED_IDENTIFIER ON;
GO

CREATE TABLE dbo.PlatformAdministrators
(
    Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_PlatformAdministrators PRIMARY KEY,
    Username NVARCHAR(100) COLLATE Latin1_General_100_CI_AS NOT NULL CONSTRAINT UQ_PlatformAdministrators_Username UNIQUE,
    Email NVARCHAR(320) COLLATE Latin1_General_100_CI_AS NOT NULL,
    PasswordHash VARBINARY(32) NOT NULL,
    PasswordSalt VARBINARY(32) NOT NULL,
    PasswordIterations INT NOT NULL CONSTRAINT CK_PlatformAdministrators_Iterations CHECK (PasswordIterations >= 600000),
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
    PasswordIterations INT NOT NULL CONSTRAINT CK_PortalUsers_Iterations CHECK (PasswordIterations >= 600000),
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
    Code NVARCHAR(100) COLLATE Latin1_General_100_CI_AS NOT NULL
        CONSTRAINT DF_Tenants_Code DEFAULT (CONVERT(NVARCHAR(36), NEWID())),
    TimeZoneId NVARCHAR(100) NOT NULL CONSTRAINT DF_Tenants_TimeZoneId DEFAULT ('UTC'),
    IsActive BIT NOT NULL CONSTRAINT DF_Tenants_IsActive DEFAULT (1),
    CreatedAt DATETIMEOFFSET NOT NULL
);
GO
CREATE UNIQUE INDEX UX_Tenants_Code ON dbo.Tenants(Code);
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
    CONSTRAINT UQ_SmsMessages_Tenant_Id UNIQUE (TenantId, Id),
    CONSTRAINT CK_SmsMessages_Direction CHECK (Direction IN (1, 2)),
    CONSTRAINT CK_SmsMessages_Status CHECK (Status BETWEEN 1 AND 5)
);
GO
CREATE INDEX IX_SmsMessages_TenantId_CreatedAt ON dbo.SmsMessages(TenantId, CreatedAt DESC, Id DESC);
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
    CONSTRAINT CK_ApiClients_SecretIterations CHECK (SecretIterations >= 600000)
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
CREATE INDEX IX_SmsMessageStatusHistory_Tenant_Status_CreatedAt
    ON dbo.SmsMessageStatusHistory(TenantId, Status, CreatedAt)
    INCLUDE (MessageId);
GO


CREATE TABLE dbo.AlertRules
(
    Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_AlertRules PRIMARY KEY,
    TenantId UNIQUEIDENTIFIER NOT NULL,
    Name NVARCHAR(120) NOT NULL,
    Provider NVARCHAR(50) NULL,
    Status INT NOT NULL,
    Threshold INT NOT NULL,
    WindowMinutes INT NOT NULL,
    RepeatMode INT NOT NULL,
    RepeatIntervalMinutes INT NULL,
    IsActive BIT NOT NULL,
    IsTriggered BIT NOT NULL CONSTRAINT DF_AlertRules_IsTriggered DEFAULT (0),
    LastTriggeredAt DATETIMEOFFSET NULL,
    CreatedAt DATETIMEOFFSET NOT NULL,
    UpdatedAt DATETIMEOFFSET NULL,
    CONSTRAINT FK_AlertRules_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(Id),
    CONSTRAINT UQ_AlertRules_Tenant_Id UNIQUE (TenantId, Id),
    CONSTRAINT CK_AlertRules_Status CHECK (Status BETWEEN 1 AND 5),
    CONSTRAINT CK_AlertRules_Threshold CHECK (Threshold BETWEEN 1 AND 1000000),
    CONSTRAINT CK_AlertRules_Window CHECK (WindowMinutes BETWEEN 1 AND 43200),
    CONSTRAINT CK_AlertRules_RepeatMode CHECK (RepeatMode IN (1,2)),
    CONSTRAINT CK_AlertRules_RepeatInterval CHECK
    (
        (RepeatMode=1 AND RepeatIntervalMinutes IS NULL)
        OR (RepeatMode=2 AND RepeatIntervalMinutes BETWEEN 1 AND 43200)
    )
);
GO
CREATE UNIQUE INDEX UX_AlertRules_Tenant_Name ON dbo.AlertRules(TenantId, Name);
GO
CREATE INDEX IX_AlertRules_Active ON dbo.AlertRules(IsActive, TenantId) INCLUDE (Status, Provider, Threshold, WindowMinutes);
GO

CREATE TABLE dbo.Alerts
(
    Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Alerts PRIMARY KEY,
    TenantId UNIQUEIDENTIFIER NOT NULL,
    RuleId UNIQUEIDENTIFIER NOT NULL,
    RuleName NVARCHAR(120) NOT NULL,
    Provider NVARCHAR(50) NULL,
    Status INT NOT NULL,
    MatchCount INT NOT NULL,
    WindowMinutes INT NOT NULL,
    CreatedAt DATETIMEOFFSET NOT NULL,
    IsRead BIT NOT NULL CONSTRAINT DF_Alerts_IsRead DEFAULT (0),
    ReadAt DATETIMEOFFSET NULL,
    CONSTRAINT FK_Alerts_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(Id),
    CONSTRAINT FK_Alerts_TenantRule FOREIGN KEY (TenantId, RuleId) REFERENCES dbo.AlertRules(TenantId, Id),
    CONSTRAINT CK_Alerts_Status CHECK (Status BETWEEN 1 AND 5),
    CONSTRAINT CK_Alerts_MatchCount CHECK (MatchCount >= 0),
    CONSTRAINT CK_Alerts_ReadState CHECK
    (
        (IsRead = 0 AND ReadAt IS NULL)
        OR (IsRead = 1 AND ReadAt IS NOT NULL)
    )
);
GO
CREATE INDEX IX_Alerts_Tenant_CreatedAt ON dbo.Alerts(TenantId, CreatedAt DESC, Id DESC)
    INCLUDE (IsRead, RuleId, Status, Provider, MatchCount);
GO

CREATE TABLE dbo.AlertStatusCounters
(
    TenantId UNIQUEIDENTIFIER NOT NULL,
    Provider NVARCHAR(50) NOT NULL,
    Status INT NOT NULL,
    BucketStartUtc DATETIMEOFFSET NOT NULL,
    MessageCount INT NOT NULL CONSTRAINT CK_AlertStatusCounters_MessageCount CHECK (MessageCount > 0),
    UpdatedAtUtc DATETIMEOFFSET NOT NULL,
    CONSTRAINT PK_AlertStatusCounters PRIMARY KEY (TenantId, Provider, Status, BucketStartUtc),
    CONSTRAINT FK_AlertStatusCounters_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(Id) ON DELETE CASCADE,
    CONSTRAINT CK_AlertStatusCounters_Status CHECK (Status BETWEEN 1 AND 5)
);
GO
CREATE INDEX IX_AlertStatusCounters_Tenant_Status_Bucket
    ON dbo.AlertStatusCounters(TenantId, Status, BucketStartUtc)
    INCLUDE (Provider, MessageCount);
GO
CREATE INDEX IX_AlertStatusCounters_Tenant_Provider_Status_Bucket
    ON dbo.AlertStatusCounters(TenantId, Provider, Status, BucketStartUtc)
    INCLUDE (MessageCount);
GO


CREATE TABLE dbo.AlertEvaluationOutbox
(
    Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_AlertEvaluationOutbox PRIMARY KEY,
    TenantId UNIQUEIDENTIFIER NOT NULL,
    Provider NVARCHAR(50) NOT NULL,
    Status INT NOT NULL,
    OccurredAtUtc DATETIMEOFFSET NOT NULL,
    CreatedAtUtc DATETIMEOFFSET NOT NULL,
    PublishedAtUtc DATETIMEOFFSET NULL,
    AttemptCount INT NOT NULL CONSTRAINT DF_AlertEvaluationOutbox_AttemptCount DEFAULT (0),
    LastAttemptAtUtc DATETIMEOFFSET NULL,
    CONSTRAINT FK_AlertEvaluationOutbox_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(Id) ON DELETE CASCADE,
    CONSTRAINT CK_AlertEvaluationOutbox_Status CHECK (Status BETWEEN 1 AND 5)
);
GO
CREATE INDEX IX_AlertEvaluationOutbox_Pending ON dbo.AlertEvaluationOutbox(CreatedAtUtc, Id)
    INCLUDE (TenantId, Provider, Status, OccurredAtUtc)
    WHERE PublishedAtUtc IS NULL;
GO

CREATE INDEX IX_AlertEvaluationOutbox_PublishedAtUtc
    ON dbo.AlertEvaluationOutbox(PublishedAtUtc, Id)
    WHERE PublishedAtUtc IS NOT NULL;
GO

CREATE TABLE dbo.AlertEvaluationInbox
(
    EventId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_AlertEvaluationInbox PRIMARY KEY,
    ProcessedAtUtc DATETIMEOFFSET NOT NULL
);
GO
CREATE INDEX IX_AlertEvaluationInbox_ProcessedAtUtc
    ON dbo.AlertEvaluationInbox(ProcessedAtUtc, EventId);
GO

CREATE TRIGGER dbo.TR_SmsMessageStatusHistory_AlertEvaluationOutbox
ON dbo.SmsMessageStatusHistory
AFTER INSERT
AS
BEGIN
    SET NOCOUNT ON;
    INSERT dbo.AlertEvaluationOutbox(Id, TenantId, Provider, Status, OccurredAtUtc, CreatedAtUtc)
    SELECT NEWID(), i.TenantId, m.Provider, i.Status, i.CreatedAt, SYSUTCDATETIME()
    FROM inserted i
    INNER JOIN dbo.SmsMessages m ON m.TenantId = i.TenantId AND m.Id = i.MessageId;
END;
GO


CREATE TABLE dbo.SmsSendInbox
(
    EventId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_SmsSendInbox PRIMARY KEY,
    ProcessedAtUtc DATETIMEOFFSET NOT NULL
);
GO
CREATE INDEX IX_SmsSendInbox_ProcessedAtUtc
    ON dbo.SmsSendInbox(ProcessedAtUtc, EventId);
GO

CREATE TABLE dbo.TenantSmsOverviewOutbox
(
    SequenceNumber BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_TenantSmsOverviewOutbox PRIMARY KEY,
    EventId UNIQUEIDENTIFIER NOT NULL CONSTRAINT UQ_TenantSmsOverviewOutbox_EventId UNIQUE,
    TenantId UNIQUEIDENTIFIER NOT NULL,
    OutboundDelta BIGINT NOT NULL,
    InboundDelta BIGINT NOT NULL,
    DeliveredDelta BIGINT NOT NULL,
    FailedDelta BIGINT NOT NULL,
    PendingDelta BIGINT NOT NULL,
    OccurredAtUtc DATETIMEOFFSET NOT NULL,
    PublishedAtUtc DATETIMEOFFSET NULL,
    AttemptCount INT NOT NULL CONSTRAINT DF_TenantSmsOverviewOutbox_AttemptCount DEFAULT (0),
    LastAttemptAtUtc DATETIMEOFFSET NULL,
    CONSTRAINT FK_TenantSmsOverviewOutbox_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(Id) ON DELETE CASCADE
);
GO

CREATE INDEX IX_TenantSmsOverviewOutbox_Pending
    ON dbo.TenantSmsOverviewOutbox(SequenceNumber)
    INCLUDE (EventId, TenantId, OutboundDelta, InboundDelta, DeliveredDelta, FailedDelta, PendingDelta, OccurredAtUtc)
    WHERE PublishedAtUtc IS NULL;
GO

CREATE TRIGGER dbo.TR_SmsMessages_TenantSmsOverview_Insert
ON dbo.SmsMessages
AFTER INSERT
AS
BEGIN
    SET NOCOUNT ON;

    INSERT dbo.TenantSmsOverviewOutbox
        (EventId, TenantId, OutboundDelta, InboundDelta, DeliveredDelta, FailedDelta, PendingDelta, OccurredAtUtc)
    SELECT
        NEWID(),
        TenantId,
        SUM(CASE WHEN Direction = 1 THEN CONVERT(BIGINT, 1) ELSE 0 END),
        SUM(CASE WHEN Direction = 2 THEN CONVERT(BIGINT, 1) ELSE 0 END),
        SUM(CASE WHEN Direction = 1 AND Status = 3 THEN CONVERT(BIGINT, 1) ELSE 0 END),
        SUM(CASE WHEN Direction = 1 AND Status = 4 THEN CONVERT(BIGINT, 1) ELSE 0 END),
        SUM(CASE WHEN Direction = 1 AND Status IN (1, 2) THEN CONVERT(BIGINT, 1) ELSE 0 END),
        SYSUTCDATETIME()
    FROM inserted
    GROUP BY TenantId;
END;
GO

CREATE TRIGGER dbo.TR_SmsMessages_TenantSmsOverview_Update
ON dbo.SmsMessages
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    INSERT dbo.TenantSmsOverviewOutbox
        (EventId, TenantId, OutboundDelta, InboundDelta, DeliveredDelta, FailedDelta, PendingDelta, OccurredAtUtc)
    SELECT
        NEWID(),
        i.TenantId,
        SUM(CONVERT(BIGINT, CASE WHEN i.Direction = 1 THEN 1 ELSE 0 END) - CASE WHEN d.Direction = 1 THEN 1 ELSE 0 END),
        SUM(CONVERT(BIGINT, CASE WHEN i.Direction = 2 THEN 1 ELSE 0 END) - CASE WHEN d.Direction = 2 THEN 1 ELSE 0 END),
        SUM(CONVERT(BIGINT, CASE WHEN i.Direction = 1 AND i.Status = 3 THEN 1 ELSE 0 END) - CASE WHEN d.Direction = 1 AND d.Status = 3 THEN 1 ELSE 0 END),
        SUM(CONVERT(BIGINT, CASE WHEN i.Direction = 1 AND i.Status = 4 THEN 1 ELSE 0 END) - CASE WHEN d.Direction = 1 AND d.Status = 4 THEN 1 ELSE 0 END),
        SUM(CONVERT(BIGINT, CASE WHEN i.Direction = 1 AND i.Status IN (1, 2) THEN 1 ELSE 0 END) - CASE WHEN d.Direction = 1 AND d.Status IN (1, 2) THEN 1 ELSE 0 END),
        SYSUTCDATETIME()
    FROM inserted i
    INNER JOIN deleted d ON d.Id = i.Id
    WHERE i.Direction <> d.Direction OR i.Status <> d.Status
    GROUP BY i.TenantId;
END;
GO
