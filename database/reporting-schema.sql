CREATE TABLE dbo.TenantSmsOverview
(
    TenantId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_TenantSmsOverview PRIMARY KEY,
    Outbound BIGINT NOT NULL CONSTRAINT DF_TenantSmsOverview_Outbound DEFAULT (0),
    Inbound BIGINT NOT NULL CONSTRAINT DF_TenantSmsOverview_Inbound DEFAULT (0),
    Delivered BIGINT NOT NULL CONSTRAINT DF_TenantSmsOverview_Delivered DEFAULT (0),
    Failed BIGINT NOT NULL CONSTRAINT DF_TenantSmsOverview_Failed DEFAULT (0),
    Pending BIGINT NOT NULL CONSTRAINT DF_TenantSmsOverview_Pending DEFAULT (0),
    UpdatedAtUtc DATETIMEOFFSET NOT NULL
);
GO

CREATE TABLE dbo.TenantSmsOverviewInbox
(
    EventId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_TenantSmsOverviewInbox PRIMARY KEY,
    ProcessedAtUtc DATETIMEOFFSET NOT NULL
);
GO

CREATE INDEX IX_TenantSmsOverviewInbox_ProcessedAtUtc
    ON dbo.TenantSmsOverviewInbox(ProcessedAtUtc, EventId);
GO
