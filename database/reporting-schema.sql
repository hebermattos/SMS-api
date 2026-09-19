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
