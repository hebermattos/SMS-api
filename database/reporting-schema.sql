CREATE TABLE TenantSmsOverview
(
    TenantId UUID PRIMARY KEY,
    Outbound BIGINT NOT NULL DEFAULT 0,
    Inbound BIGINT NOT NULL DEFAULT 0,
    Delivered BIGINT NOT NULL DEFAULT 0,
    Failed BIGINT NOT NULL DEFAULT 0,
    Pending BIGINT NOT NULL DEFAULT 0,
    UpdatedAtUtc TIMESTAMPTZ NOT NULL
);

CREATE TABLE TenantSmsOverviewInbox
(
    EventId UUID PRIMARY KEY,
    ProcessedAtUtc TIMESTAMPTZ NOT NULL
);

CREATE INDEX IX_TenantSmsOverviewInbox_ProcessedAtUtc
    ON TenantSmsOverviewInbox(ProcessedAtUtc, EventId);

CREATE TABLE ReportingSmsMessages
(
    MessageId UUID PRIMARY KEY,
    TenantId UUID NOT NULL,
    TenantName VARCHAR(200) NOT NULL,
    UserId UUID NULL,
    Username VARCHAR(200) NULL,
    Provider VARCHAR(50) NOT NULL,
    Direction INTEGER NOT NULL,
    QueueStatus INTEGER NOT NULL,
    Status INTEGER NOT NULL,
    CreatedAtUtc TIMESTAMPTZ NOT NULL,
    UpdatedAtUtc TIMESTAMPTZ NOT NULL,
    CHECK (Direction IN (1, 2)),
    CHECK (QueueStatus BETWEEN 1 AND 4),
    CHECK (Status BETWEEN 1 AND 5)
);

CREATE INDEX IX_ReportingSmsMessages_Tenant_Created
    ON ReportingSmsMessages(TenantId, CreatedAtUtc DESC, MessageId)
    INCLUDE (Provider, Direction, QueueStatus, Status, UserId);

CREATE TABLE TenantSmsDailyOverview
(
    ReportDate DATE NOT NULL,
    TenantId UUID NOT NULL,
    TenantName VARCHAR(200) NOT NULL,
    TotalMessages BIGINT NOT NULL DEFAULT 0,
    Scheduled BIGINT NOT NULL DEFAULT 0,
    Queued BIGINT NOT NULL DEFAULT 0,
    Sent BIGINT NOT NULL DEFAULT 0,
    Delivered BIGINT NOT NULL DEFAULT 0,
    Failed BIGINT NOT NULL DEFAULT 0,
    Received BIGINT NOT NULL DEFAULT 0,
    Outbound BIGINT NOT NULL DEFAULT 0,
    Inbound BIGINT NOT NULL DEFAULT 0,
    Pending BIGINT NOT NULL DEFAULT 0,
    UpdatedAtUtc TIMESTAMPTZ NOT NULL,
    PRIMARY KEY (ReportDate, TenantId)
);

CREATE INDEX IX_TenantSmsDailyOverview_Tenant_Date
    ON TenantSmsDailyOverview(TenantId, ReportDate DESC);

CREATE TABLE ProviderSmsDailyOverview
(
    ReportDate DATE NOT NULL,
    TenantId UUID NOT NULL,
    TenantName VARCHAR(200) NOT NULL,
    Provider VARCHAR(50) NOT NULL,
    TotalMessages BIGINT NOT NULL DEFAULT 0,
    Scheduled BIGINT NOT NULL DEFAULT 0,
    Queued BIGINT NOT NULL DEFAULT 0,
    Sent BIGINT NOT NULL DEFAULT 0,
    Delivered BIGINT NOT NULL DEFAULT 0,
    Failed BIGINT NOT NULL DEFAULT 0,
    Received BIGINT NOT NULL DEFAULT 0,
    Outbound BIGINT NOT NULL DEFAULT 0,
    Inbound BIGINT NOT NULL DEFAULT 0,
    Pending BIGINT NOT NULL DEFAULT 0,
    UpdatedAtUtc TIMESTAMPTZ NOT NULL,
    PRIMARY KEY (ReportDate, TenantId, Provider)
);

CREATE INDEX IX_ProviderSmsDailyOverview_Tenant_Date
    ON ProviderSmsDailyOverview(TenantId, ReportDate DESC, Provider);

CREATE TABLE UserSmsOverview
(
    TenantId UUID NOT NULL,
    UserId UUID NOT NULL,
    Username VARCHAR(200) NOT NULL,
    ReportDate DATE NOT NULL,
    TotalMessages BIGINT NOT NULL DEFAULT 0,
    Scheduled BIGINT NOT NULL DEFAULT 0,
    Queued BIGINT NOT NULL DEFAULT 0,
    Sent BIGINT NOT NULL DEFAULT 0,
    Delivered BIGINT NOT NULL DEFAULT 0,
    Failed BIGINT NOT NULL DEFAULT 0,
    Received BIGINT NOT NULL DEFAULT 0,
    Outbound BIGINT NOT NULL DEFAULT 0,
    Inbound BIGINT NOT NULL DEFAULT 0,
    Pending BIGINT NOT NULL DEFAULT 0,
    UpdatedAtUtc TIMESTAMPTZ NOT NULL,
    PRIMARY KEY (TenantId, UserId, ReportDate)
);

CREATE INDEX IX_UserSmsOverview_Tenant_Total
    ON UserSmsOverview(TenantId, ReportDate DESC, TotalMessages DESC, UserId);

CREATE TABLE AlertMessageWindow
(
    EventId UUID PRIMARY KEY,
    TenantId UUID NOT NULL,
    Provider VARCHAR(50) NOT NULL,
    Status INTEGER NOT NULL,
    OccurredAtUtc TIMESTAMPTZ NOT NULL,
    ExpiresAtUtc TIMESTAMPTZ NOT NULL,
    CHECK (Status BETWEEN 1 AND 5),
    CHECK (ExpiresAtUtc = OccurredAtUtc + INTERVAL '24 hours')
);

CREATE INDEX IX_AlertMessageWindow_Tenant_Status_Occurred
    ON AlertMessageWindow(TenantId, Status, OccurredAtUtc DESC)
    INCLUDE (Provider, ExpiresAtUtc);

CREATE INDEX IX_AlertMessageWindow_Expires
    ON AlertMessageWindow(ExpiresAtUtc);
