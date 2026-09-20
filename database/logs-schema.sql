CREATE TABLE UserActivityLogs
(
    Id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    "Timestamp" TIMESTAMPTZ NOT NULL,
    TenantId UUID NULL,
    Severity VARCHAR(32) NOT NULL,
    Category VARCHAR(256) NOT NULL,
    Message VARCHAR(4000) NOT NULL,
    TraceId CHAR(32) NULL,
    SpanId CHAR(16) NULL,
    Attributes TEXT NULL,
    CHECK (Attributes IS NULL OR Attributes IS JSON)
);

CREATE INDEX IX_UserActivityLogs_Tenant_Timestamp
    ON UserActivityLogs(TenantId, "Timestamp" DESC, Id DESC)
    INCLUDE (Severity, Category, TraceId, SpanId);

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
