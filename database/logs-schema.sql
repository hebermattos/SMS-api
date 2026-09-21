CREATE TABLE UserActivityLogs
(
    Id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    "Timestamp" TIMESTAMPTZ NOT NULL,
    TenantId UUID NOT NULL,
    UserId VARCHAR(100) NULL,
    ActivityType VARCHAR(32) NOT NULL,
    Action VARCHAR(100) NOT NULL,
    ResourceType VARCHAR(100) NULL,
    ResourceId VARCHAR(100) NULL,
    Description VARCHAR(4000) NOT NULL,
    Outcome VARCHAR(16) NOT NULL,
    CHECK (Outcome IN ('Succeeded', 'Failed'))
);

CREATE INDEX IX_UserActivityLogs_Tenant_Timestamp
    ON UserActivityLogs(TenantId, "Timestamp" DESC, Id DESC);

CREATE TABLE PlatformActivityLogs
(
    Id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    "Timestamp" TIMESTAMPTZ NOT NULL,
    UserId VARCHAR(100) NULL,
    ActivityType VARCHAR(32) NOT NULL,
    Action VARCHAR(100) NOT NULL,
    ResourceType VARCHAR(100) NULL,
    ResourceId VARCHAR(100) NULL,
    Description VARCHAR(4000) NOT NULL,
    Outcome VARCHAR(16) NOT NULL,
    CHECK (Outcome IN ('Succeeded', 'Failed'))
);

CREATE INDEX IX_PlatformActivityLogs_Timestamp
    ON PlatformActivityLogs("Timestamp" DESC, Id DESC);

CREATE TABLE SystemLogs
(
    Id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    "Timestamp" TIMESTAMPTZ NOT NULL,
    Severity VARCHAR(32) NOT NULL,
    Category VARCHAR(256) NOT NULL,
    Message VARCHAR(4000) NOT NULL,
    TraceId CHAR(32) NULL,
    SpanId CHAR(16) NULL,
    Attributes TEXT NULL,
    CHECK (Severity IN ('Error', 'Critical')),
    CHECK (Attributes IS NULL OR Attributes IS JSON)
);

CREATE INDEX IX_SystemLogs_Timestamp
    ON SystemLogs("Timestamp" DESC, Id DESC)
    INCLUDE (Severity, Category, TraceId, SpanId);
