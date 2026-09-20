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


CREATE TABLE UserSmsOverview
(
    TenantId UUID NOT NULL,
    UserId UUID NOT NULL,
    ReportDate DATE NOT NULL,
    TotalMessages BIGINT NOT NULL DEFAULT 0,
    Delivered BIGINT NOT NULL DEFAULT 0,
    Failed BIGINT NOT NULL DEFAULT 0,
    Pending BIGINT NOT NULL DEFAULT 0,
    UpdatedAtUtc TIMESTAMPTZ NOT NULL,
    PRIMARY KEY (TenantId, UserId, ReportDate)
);

CREATE INDEX IX_UserSmsOverview_Tenant_Total
    ON UserSmsOverview(TenantId, ReportDate DESC, TotalMessages DESC, UserId);
