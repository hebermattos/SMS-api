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

CREATE TABLE Traces
(
    Id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    StartedAt TIMESTAMPTZ NOT NULL,
    DurationMilliseconds DOUBLE PRECISION NOT NULL,
    TraceId CHAR(32) NOT NULL,
    SpanId CHAR(16) NOT NULL,
    ParentSpanId CHAR(16) NULL,
    Name VARCHAR(256) NOT NULL,
    Source VARCHAR(256) NOT NULL,
    Kind VARCHAR(32) NOT NULL,
    Status VARCHAR(32) NOT NULL,
    Attributes TEXT NULL,
    CHECK (Attributes IS NULL OR Attributes IS JSON)
);

CREATE INDEX IX_Traces_StartedAt ON Traces(StartedAt DESC, Id DESC)
    INCLUDE (TraceId, SpanId, Name, Status, DurationMilliseconds);
CREATE INDEX IX_Traces_TraceId ON Traces(TraceId, StartedAt, Id);

CREATE TABLE Metrics
(
    Id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    "Timestamp" TIMESTAMPTZ NOT NULL,
    Name VARCHAR(256) NOT NULL,
    Unit VARCHAR(64) NULL,
    MetricType VARCHAR(64) NOT NULL,
    Value DOUBLE PRECISION NULL,
    "Count" BIGINT NULL,
    Attributes TEXT NULL,
    CHECK (Attributes IS NULL OR Attributes IS JSON),
    CHECK (Value IS NOT NULL OR "Count" IS NOT NULL)
);

CREATE INDEX IX_Metrics_Timestamp ON Metrics("Timestamp" DESC, Id DESC);
CREATE INDEX IX_Metrics_Name_Timestamp ON Metrics(Name, "Timestamp" DESC, Id DESC)
    INCLUDE (MetricType, Value, "Count");
