CREATE EXTENSION IF NOT EXISTS citext;
CREATE EXTENSION IF NOT EXISTS pgcrypto;

CREATE TABLE Tenants
(
    Id UUID PRIMARY KEY,
    Name VARCHAR(200) NOT NULL,
    Code CITEXT NOT NULL DEFAULT (gen_random_uuid()::text),
    TimeZoneId VARCHAR(100) NOT NULL DEFAULT 'UTC',
    IsActive BOOLEAN NOT NULL DEFAULT TRUE,
    CreatedAt TIMESTAMPTZ NOT NULL
);
CREATE UNIQUE INDEX UX_Tenants_Code ON Tenants(Code);

CREATE TABLE PlatformAdministrators
(
    Id UUID PRIMARY KEY,
    Username CITEXT NOT NULL UNIQUE,
    Email CITEXT NOT NULL,
    PasswordHash BYTEA NOT NULL,
    PasswordSalt BYTEA NOT NULL,
    PasswordIterations INTEGER NOT NULL CHECK (PasswordIterations >= 600000),
    IsActive BOOLEAN NOT NULL DEFAULT TRUE,
    CreatedAt TIMESTAMPTZ NOT NULL
);
CREATE UNIQUE INDEX UX_PlatformAdministrators_Email ON PlatformAdministrators(Email);

CREATE TABLE PortalUsers
(
    Id UUID PRIMARY KEY,
    TenantId UUID NULL REFERENCES Tenants(Id),
    Username CITEXT NOT NULL,
    Email CITEXT NOT NULL,
    PasswordHash BYTEA NOT NULL,
    PasswordSalt BYTEA NOT NULL,
    PasswordIterations INTEGER NOT NULL CHECK (PasswordIterations >= 600000),
    Context VARCHAR(20) NOT NULL,
    Role VARCHAR(20) NOT NULL,
    IsActive BOOLEAN NOT NULL DEFAULT TRUE,
    CreatedAt TIMESTAMPTZ NOT NULL,
    UpdatedAt TIMESTAMPTZ NULL,
    CHECK (Context IN ('tenant', 'platform')),
    CHECK (Role IN ('user', 'administrator')),
    CHECK
    (
        (Context = 'tenant' AND TenantId IS NOT NULL)
        OR (Context = 'platform' AND TenantId IS NULL)
    )
);
CREATE UNIQUE INDEX UX_PortalUsers_PlatformEmail
    ON PortalUsers(Email) WHERE Context = 'platform';
CREATE UNIQUE INDEX UX_PortalUsers_TenantEmail
    ON PortalUsers(TenantId, Email) WHERE Context = 'tenant';
CREATE UNIQUE INDEX UX_PortalUsers_PlatformUsername
    ON PortalUsers(Username) WHERE Context = 'platform';
CREATE UNIQUE INDEX UX_PortalUsers_TenantUsername
    ON PortalUsers(TenantId, Username) WHERE Context = 'tenant';
CREATE INDEX IX_PortalUsers_TenantId ON PortalUsers(TenantId) WHERE TenantId IS NOT NULL;

CREATE TABLE SmsMessages
(
    Id UUID PRIMARY KEY,
    TenantId UUID NOT NULL REFERENCES Tenants(Id),
    "From" VARCHAR(256) NOT NULL,
    "To" VARCHAR(256) NOT NULL,
    Body TEXT NOT NULL,
    Provider VARCHAR(50) NOT NULL,
    ProviderMessageId VARCHAR(200) NULL,
    Direction INTEGER NOT NULL,
    Status INTEGER NOT NULL,
    CreatedAt TIMESTAMPTZ NOT NULL,
    ScheduledAtUtc TIMESTAMPTZ NULL,
    UpdatedAt TIMESTAMPTZ NULL,
    UNIQUE (TenantId, Id),
    CHECK (Direction IN (1, 2)),
    CHECK (Status BETWEEN 1 AND 6),
    CHECK
    (
        (Status = 6 AND Direction = 1 AND ScheduledAtUtc IS NOT NULL)
        OR Status <> 6
    )
);
CREATE INDEX IX_SmsMessages_TenantId_CreatedAt ON SmsMessages(TenantId, CreatedAt DESC, Id DESC);
CREATE INDEX IX_SmsMessages_CreatedAt ON SmsMessages(CreatedAt DESC, Id DESC)
    INCLUDE (TenantId, Provider, Direction, Status);
CREATE INDEX IX_SmsMessages_Scheduled ON SmsMessages(ScheduledAtUtc, Id)
    INCLUDE (TenantId) WHERE Status = 6;
CREATE INDEX IX_SmsMessages_QueuedScheduledRetry ON SmsMessages(UpdatedAt, ScheduledAtUtc, Id)
    INCLUDE (TenantId) WHERE Status = 1 AND ScheduledAtUtc IS NOT NULL;
CREATE UNIQUE INDEX UX_SmsMessages_Tenant_Provider_Message
    ON SmsMessages(TenantId, Provider, ProviderMessageId)
    WHERE ProviderMessageId IS NOT NULL;

CREATE TABLE SmsOptOuts
(
    Id UUID PRIMARY KEY,
    TenantId UUID NOT NULL REFERENCES Tenants(Id),
    PhoneHash BYTEA NOT NULL,
    PhoneNumber VARCHAR(256) NOT NULL,
    Source VARCHAR(30) NOT NULL,
    Reason VARCHAR(200) NULL,
    CreatedAt TIMESTAMPTZ NOT NULL,
    UpdatedAt TIMESTAMPTZ NULL,
    UNIQUE (TenantId, Id),
    CHECK (Source IN ('Manual', 'Import', 'InboundKeyword'))
);
CREATE UNIQUE INDEX UX_SmsOptOuts_Tenant_PhoneHash ON SmsOptOuts(TenantId, PhoneHash);
CREATE INDEX IX_SmsOptOuts_Tenant_CreatedAt ON SmsOptOuts(TenantId, CreatedAt DESC, Id DESC)
    INCLUDE (Source, Reason, UpdatedAt);

CREATE TABLE TenantSmsProviders
(
    Id UUID PRIMARY KEY,
    TenantId UUID NOT NULL REFERENCES Tenants(Id),
    Provider VARCHAR(50) NOT NULL,
    AccountId VARCHAR(200) NOT NULL,
    ApiSecret VARCHAR(1000) NOT NULL,
    FromNumber VARCHAR(32) NULL,
    Settings TEXT NULL,
    IsDefault BOOLEAN NOT NULL DEFAULT FALSE,
    IsActive BOOLEAN NOT NULL DEFAULT TRUE,
    CreatedAt TIMESTAMPTZ NOT NULL,
    UpdatedAt TIMESTAMPTZ NULL
);
CREATE UNIQUE INDEX UX_TenantSmsProviders_Tenant_Provider ON TenantSmsProviders(TenantId, Provider);
CREATE INDEX IX_TenantSmsProviders_Tenant_Active
    ON TenantSmsProviders(TenantId, IsDefault DESC, Provider)
    INCLUDE (FromNumber) WHERE IsActive;
CREATE UNIQUE INDEX UX_TenantSmsProviders_Default
    ON TenantSmsProviders(TenantId) WHERE IsDefault AND IsActive;
CREATE UNIQUE INDEX UX_TenantSmsProviders_CallbackRoute
    ON TenantSmsProviders(Provider, AccountId, FromNumber)
    WHERE IsActive AND FromNumber IS NOT NULL;

CREATE TABLE ApiClients
(
    Id UUID PRIMARY KEY,
    TenantId UUID NOT NULL REFERENCES Tenants(Id),
    ClientId CITEXT NOT NULL,
    SecretHash BYTEA NOT NULL,
    SecretSalt BYTEA NOT NULL,
    SecretIterations INTEGER NOT NULL CHECK (SecretIterations >= 600000),
    IsActive BOOLEAN NOT NULL DEFAULT TRUE,
    CreatedAt TIMESTAMPTZ NOT NULL,
    UpdatedAt TIMESTAMPTZ NULL
);
CREATE UNIQUE INDEX UX_ApiClients_ClientId ON ApiClients(ClientId);
CREATE INDEX IX_ApiClients_TenantId ON ApiClients(TenantId);

CREATE TABLE SmsMessageStatusHistory
(
    Id UUID PRIMARY KEY,
    TenantId UUID NOT NULL REFERENCES Tenants(Id),
    MessageId UUID NOT NULL,
    Status INTEGER NOT NULL,
    CreatedAt TIMESTAMPTZ NOT NULL,
    FOREIGN KEY (TenantId, MessageId) REFERENCES SmsMessages(TenantId, Id)
);
CREATE INDEX IX_SmsMessageStatusHistory_Tenant_Message_CreatedAt
    ON SmsMessageStatusHistory(TenantId, MessageId, CreatedAt);
CREATE INDEX IX_SmsMessageStatusHistory_Tenant_Status_CreatedAt
    ON SmsMessageStatusHistory(TenantId, Status, CreatedAt)
    INCLUDE (MessageId);

CREATE TABLE AlertRules
(
    Id UUID PRIMARY KEY,
    TenantId UUID NOT NULL REFERENCES Tenants(Id),
    Name VARCHAR(120) NOT NULL,
    Provider VARCHAR(50) NULL,
    Status INTEGER NOT NULL,
    Threshold INTEGER NOT NULL,
    WindowMinutes INTEGER NOT NULL,
    RepeatMode INTEGER NOT NULL,
    RepeatIntervalMinutes INTEGER NULL,
    IsActive BOOLEAN NOT NULL,
    IsTriggered BOOLEAN NOT NULL DEFAULT FALSE,
    LastTriggeredAt TIMESTAMPTZ NULL,
    CreatedAt TIMESTAMPTZ NOT NULL,
    UpdatedAt TIMESTAMPTZ NULL,
    DeletedAt TIMESTAMPTZ NULL,
    UNIQUE (TenantId, Id),
    CHECK (Status BETWEEN 1 AND 5),
    CHECK (Threshold BETWEEN 1 AND 1000000),
    CHECK (WindowMinutes BETWEEN 1 AND 43200),
    CHECK (RepeatMode IN (1, 2)),
    CHECK
    (
        (RepeatMode = 1 AND RepeatIntervalMinutes IS NULL)
        OR (RepeatMode = 2 AND RepeatIntervalMinutes BETWEEN 1 AND 43200)
    )
);
CREATE UNIQUE INDEX UX_AlertRules_Tenant_Name
    ON AlertRules(TenantId, Name) WHERE DeletedAt IS NULL;
CREATE INDEX IX_AlertRules_Active
    ON AlertRules(IsActive, TenantId)
    INCLUDE (Status, Provider, Threshold, WindowMinutes)
    WHERE DeletedAt IS NULL;

CREATE TABLE Alerts
(
    Id UUID PRIMARY KEY,
    TenantId UUID NOT NULL REFERENCES Tenants(Id),
    RuleId UUID NOT NULL,
    RuleName VARCHAR(120) NOT NULL,
    Provider VARCHAR(50) NULL,
    Status INTEGER NOT NULL,
    MatchCount INTEGER NOT NULL,
    WindowMinutes INTEGER NOT NULL,
    CreatedAt TIMESTAMPTZ NOT NULL,
    IsRead BOOLEAN NOT NULL DEFAULT FALSE,
    ReadAt TIMESTAMPTZ NULL,
    FOREIGN KEY (TenantId, RuleId) REFERENCES AlertRules(TenantId, Id),
    CHECK (Status BETWEEN 1 AND 5),
    CHECK (MatchCount >= 0),
    CHECK
    (
        (NOT IsRead AND ReadAt IS NULL)
        OR (IsRead AND ReadAt IS NOT NULL)
    )
);
CREATE INDEX IX_Alerts_Tenant_CreatedAt ON Alerts(TenantId, CreatedAt DESC, Id DESC)
    INCLUDE (IsRead, RuleId, Status, Provider, MatchCount);
CREATE INDEX IX_Alerts_Tenant_Unread_CreatedAt ON Alerts(TenantId, CreatedAt DESC, Id DESC)
    INCLUDE (RuleId, RuleName, Provider, Status, MatchCount, WindowMinutes)
    WHERE NOT IsRead;

CREATE TABLE AlertStatusCounters
(
    TenantId UUID NOT NULL,
    Provider VARCHAR(50) NOT NULL,
    Status INTEGER NOT NULL,
    BucketStartUtc TIMESTAMPTZ NOT NULL,
    MessageCount INTEGER NOT NULL CHECK (MessageCount > 0),
    UpdatedAtUtc TIMESTAMPTZ NOT NULL,
    PRIMARY KEY (TenantId, Provider, Status, BucketStartUtc),
    FOREIGN KEY (TenantId) REFERENCES Tenants(Id) ON DELETE CASCADE,
    CHECK (Status BETWEEN 1 AND 5)
);
CREATE INDEX IX_AlertStatusCounters_Tenant_Status_Bucket
    ON AlertStatusCounters(TenantId, Status, BucketStartUtc)
    INCLUDE (Provider, MessageCount);
CREATE INDEX IX_AlertStatusCounters_Tenant_Provider_Status_Bucket
    ON AlertStatusCounters(TenantId, Provider, Status, BucketStartUtc)
    INCLUDE (MessageCount);

CREATE TABLE AlertEvaluationOutbox
(
    Id UUID PRIMARY KEY,
    TenantId UUID NOT NULL REFERENCES Tenants(Id) ON DELETE CASCADE,
    Provider VARCHAR(50) NOT NULL,
    Status INTEGER NOT NULL,
    OccurredAtUtc TIMESTAMPTZ NOT NULL,
    CreatedAtUtc TIMESTAMPTZ NOT NULL,
    PublishedAtUtc TIMESTAMPTZ NULL,
    AttemptCount INTEGER NOT NULL DEFAULT 0,
    LastAttemptAtUtc TIMESTAMPTZ NULL,
    CHECK (Status BETWEEN 1 AND 5)
);
CREATE INDEX IX_AlertEvaluationOutbox_Pending
    ON AlertEvaluationOutbox(CreatedAtUtc, Id)
    INCLUDE (TenantId, Provider, Status, OccurredAtUtc)
    WHERE PublishedAtUtc IS NULL;
CREATE INDEX IX_AlertEvaluationOutbox_PublishedAtUtc
    ON AlertEvaluationOutbox(PublishedAtUtc, Id)
    WHERE PublishedAtUtc IS NOT NULL;

CREATE TABLE AlertEvaluationInbox
(
    EventId UUID PRIMARY KEY,
    ProcessedAtUtc TIMESTAMPTZ NOT NULL
);
CREATE INDEX IX_AlertEvaluationInbox_ProcessedAtUtc
    ON AlertEvaluationInbox(ProcessedAtUtc, EventId);

CREATE TABLE SmsSendInbox
(
    EventId UUID PRIMARY KEY,
    ProcessedAtUtc TIMESTAMPTZ NOT NULL
);
CREATE INDEX IX_SmsSendInbox_ProcessedAtUtc
    ON SmsSendInbox(ProcessedAtUtc, EventId);

CREATE TABLE TenantSmsOverviewOutbox
(
    SequenceNumber BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    EventId UUID NOT NULL UNIQUE,
    TenantId UUID NOT NULL REFERENCES Tenants(Id) ON DELETE CASCADE,
    OutboundDelta BIGINT NOT NULL,
    InboundDelta BIGINT NOT NULL,
    DeliveredDelta BIGINT NOT NULL,
    FailedDelta BIGINT NOT NULL,
    PendingDelta BIGINT NOT NULL,
    OccurredAtUtc TIMESTAMPTZ NOT NULL,
    PublishedAtUtc TIMESTAMPTZ NULL,
    AttemptCount INTEGER NOT NULL DEFAULT 0,
    LastAttemptAtUtc TIMESTAMPTZ NULL
);
CREATE INDEX IX_TenantSmsOverviewOutbox_Pending
    ON TenantSmsOverviewOutbox(SequenceNumber)
    INCLUDE (EventId, TenantId, OutboundDelta, InboundDelta, DeliveredDelta, FailedDelta, PendingDelta, OccurredAtUtc)
    WHERE PublishedAtUtc IS NULL;

CREATE OR REPLACE FUNCTION enqueue_alert_evaluation()
RETURNS TRIGGER
LANGUAGE plpgsql
AS $$
BEGIN
    IF NEW.Status BETWEEN 1 AND 5 THEN
        INSERT INTO AlertEvaluationOutbox
            (Id, TenantId, Provider, Status, OccurredAtUtc, CreatedAtUtc)
        SELECT
            gen_random_uuid(), NEW.TenantId, m.Provider, NEW.Status, NEW.CreatedAt, CURRENT_TIMESTAMP
        FROM SmsMessages m
        WHERE m.TenantId = NEW.TenantId AND m.Id = NEW.MessageId;
    END IF;
    RETURN NEW;
END;
$$;

CREATE TRIGGER TR_SmsMessageStatusHistory_AlertEvaluationOutbox
AFTER INSERT ON SmsMessageStatusHistory
FOR EACH ROW EXECUTE FUNCTION enqueue_alert_evaluation();

CREATE OR REPLACE FUNCTION enqueue_tenant_overview_insert()
RETURNS TRIGGER
LANGUAGE plpgsql
AS $$
BEGIN
    INSERT INTO TenantSmsOverviewOutbox
        (EventId, TenantId, OutboundDelta, InboundDelta, DeliveredDelta, FailedDelta, PendingDelta, OccurredAtUtc)
    VALUES
        (
            gen_random_uuid(),
            NEW.TenantId,
            CASE WHEN NEW.Direction = 1 THEN 1 ELSE 0 END,
            CASE WHEN NEW.Direction = 2 THEN 1 ELSE 0 END,
            CASE WHEN NEW.Direction = 1 AND NEW.Status = 3 THEN 1 ELSE 0 END,
            CASE WHEN NEW.Direction = 1 AND NEW.Status = 4 THEN 1 ELSE 0 END,
            CASE WHEN NEW.Direction = 1 AND NEW.Status IN (1, 2, 6) THEN 1 ELSE 0 END,
            CURRENT_TIMESTAMP
        );
    RETURN NEW;
END;
$$;

CREATE TRIGGER TR_SmsMessages_TenantSmsOverview_Insert
AFTER INSERT ON SmsMessages
FOR EACH ROW EXECUTE FUNCTION enqueue_tenant_overview_insert();

CREATE OR REPLACE FUNCTION enqueue_tenant_overview_update()
RETURNS TRIGGER
LANGUAGE plpgsql
AS $$
BEGIN
    IF NEW.Direction IS DISTINCT FROM OLD.Direction OR NEW.Status IS DISTINCT FROM OLD.Status THEN
        INSERT INTO TenantSmsOverviewOutbox
            (EventId, TenantId, OutboundDelta, InboundDelta, DeliveredDelta, FailedDelta, PendingDelta, OccurredAtUtc)
        VALUES
            (
                gen_random_uuid(),
                NEW.TenantId,
                (CASE WHEN NEW.Direction = 1 THEN 1 ELSE 0 END) - (CASE WHEN OLD.Direction = 1 THEN 1 ELSE 0 END),
                (CASE WHEN NEW.Direction = 2 THEN 1 ELSE 0 END) - (CASE WHEN OLD.Direction = 2 THEN 1 ELSE 0 END),
                (CASE WHEN NEW.Direction = 1 AND NEW.Status = 3 THEN 1 ELSE 0 END) - (CASE WHEN OLD.Direction = 1 AND OLD.Status = 3 THEN 1 ELSE 0 END),
                (CASE WHEN NEW.Direction = 1 AND NEW.Status = 4 THEN 1 ELSE 0 END) - (CASE WHEN OLD.Direction = 1 AND OLD.Status = 4 THEN 1 ELSE 0 END),
                (CASE WHEN NEW.Direction = 1 AND NEW.Status IN (1, 2, 6) THEN 1 ELSE 0 END) - (CASE WHEN OLD.Direction = 1 AND OLD.Status IN (1, 2, 6) THEN 1 ELSE 0 END),
                CURRENT_TIMESTAMP
            );
    END IF;
    RETURN NEW;
END;
$$;

CREATE TRIGGER TR_SmsMessages_TenantSmsOverview_Update
AFTER UPDATE ON SmsMessages
FOR EACH ROW EXECUTE FUNCTION enqueue_tenant_overview_update();
