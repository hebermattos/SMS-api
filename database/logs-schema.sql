CREATE TABLE dbo.LogEntries
(
    Id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_LogEntries PRIMARY KEY,
    [Timestamp] DATETIMEOFFSET NOT NULL,
    TenantId UNIQUEIDENTIFIER NULL,
    Severity NVARCHAR(32) NOT NULL,
    Category NVARCHAR(256) NOT NULL,
    Message NVARCHAR(4000) NOT NULL,
    TraceId CHAR(32) NULL,
    SpanId CHAR(16) NULL,
    Attributes NVARCHAR(MAX) NULL,
    CONSTRAINT CK_LogEntries_AttributesJson CHECK (Attributes IS NULL OR ISJSON(Attributes) = 1)
);
GO

CREATE INDEX IX_LogEntries_Tenant_Timestamp
    ON dbo.LogEntries(TenantId, [Timestamp] DESC, Id DESC)
    INCLUDE (Severity, Category, TraceId, SpanId);
GO
