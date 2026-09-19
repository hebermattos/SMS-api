CREATE TABLE dbo.UserActivityLogs
(
    Id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_UserActivityLogs PRIMARY KEY,
    [Timestamp] DATETIMEOFFSET NOT NULL,
    TenantId UNIQUEIDENTIFIER NULL,
    Severity NVARCHAR(32) NOT NULL,
    Category NVARCHAR(256) NOT NULL,
    Message NVARCHAR(4000) NOT NULL,
    TraceId CHAR(32) NULL,
    SpanId CHAR(16) NULL,
    Attributes NVARCHAR(MAX) NULL,
    CONSTRAINT CK_UserActivityLogs_AttributesJson CHECK (Attributes IS NULL OR ISJSON(Attributes) = 1)
);
GO

CREATE INDEX IX_UserActivityLogs_Tenant_Timestamp
    ON dbo.UserActivityLogs(TenantId, [Timestamp] DESC, Id DESC)
    INCLUDE (Severity, Category, TraceId, SpanId);
GO

CREATE TABLE dbo.SystemLogs
(
    Id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_SystemLogs PRIMARY KEY,
    [Timestamp] DATETIMEOFFSET NOT NULL,
    Severity NVARCHAR(32) NOT NULL,
    Category NVARCHAR(256) NOT NULL,
    Message NVARCHAR(4000) NOT NULL,
    TraceId CHAR(32) NULL,
    SpanId CHAR(16) NULL,
    Attributes NVARCHAR(MAX) NULL,
    CONSTRAINT CK_SystemLogs_Severity CHECK (Severity IN ('Error', 'Critical')),
    CONSTRAINT CK_SystemLogs_AttributesJson CHECK (Attributes IS NULL OR ISJSON(Attributes) = 1)
);
GO

CREATE INDEX IX_SystemLogs_Timestamp
    ON dbo.SystemLogs([Timestamp] DESC, Id DESC)
    INCLUDE (Severity, Category, TraceId, SpanId);
GO

CREATE TABLE dbo.Traces
(
    Id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Traces PRIMARY KEY,
    StartedAt DATETIMEOFFSET NOT NULL,
    DurationMilliseconds FLOAT NOT NULL,
    TraceId CHAR(32) NOT NULL,
    SpanId CHAR(16) NOT NULL,
    ParentSpanId CHAR(16) NULL,
    Name NVARCHAR(256) NOT NULL,
    Source NVARCHAR(256) NOT NULL,
    Kind NVARCHAR(32) NOT NULL,
    Status NVARCHAR(32) NOT NULL,
    Attributes NVARCHAR(MAX) NULL,
    CONSTRAINT CK_Traces_AttributesJson CHECK (Attributes IS NULL OR ISJSON(Attributes) = 1)
);
GO

CREATE INDEX IX_Traces_StartedAt ON dbo.Traces(StartedAt DESC, Id DESC)
    INCLUDE (TraceId, SpanId, Name, Status, DurationMilliseconds);
GO
CREATE INDEX IX_Traces_TraceId ON dbo.Traces(TraceId, StartedAt, Id);
GO

CREATE TABLE dbo.Metrics
(
    Id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Metrics PRIMARY KEY,
    [Timestamp] DATETIMEOFFSET NOT NULL,
    Name NVARCHAR(256) NOT NULL,
    Unit NVARCHAR(64) NULL,
    MetricType NVARCHAR(64) NOT NULL,
    Value FLOAT NULL,
    [Count] BIGINT NULL,
    Attributes NVARCHAR(MAX) NULL,
    CONSTRAINT CK_Metrics_AttributesJson CHECK (Attributes IS NULL OR ISJSON(Attributes) = 1),
    CONSTRAINT CK_Metrics_Value CHECK (Value IS NOT NULL OR [Count] IS NOT NULL)
);
GO

CREATE INDEX IX_Metrics_Timestamp ON dbo.Metrics([Timestamp] DESC, Id DESC);
GO

CREATE INDEX IX_Metrics_Name_Timestamp ON dbo.Metrics(Name, [Timestamp] DESC, Id DESC)
    INCLUDE (MetricType, Value, [Count]);
GO
