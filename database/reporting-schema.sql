CREATE TABLE dbo.TenantSmsOverview
(
    TenantId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_TenantSmsOverview PRIMARY KEY,
    Outbound BIGINT NOT NULL CONSTRAINT DF_TenantSmsOverview_Outbound DEFAULT (0),
    Inbound BIGINT NOT NULL CONSTRAINT DF_TenantSmsOverview_Inbound DEFAULT (0),
    Delivered BIGINT NOT NULL CONSTRAINT DF_TenantSmsOverview_Delivered DEFAULT (0),
    Failed BIGINT NOT NULL CONSTRAINT DF_TenantSmsOverview_Failed DEFAULT (0),
    Pending BIGINT NOT NULL CONSTRAINT DF_TenantSmsOverview_Pending DEFAULT (0),
    UpdatedAtUtc DATETIMEOFFSET NOT NULL,
    CONSTRAINT CK_TenantSmsOverview_NonNegative CHECK
        (Outbound >= 0 AND Inbound >= 0 AND Delivered >= 0 AND Failed >= 0 AND Pending >= 0)
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
