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
