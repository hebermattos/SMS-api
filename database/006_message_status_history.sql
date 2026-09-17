CREATE TABLE dbo.SmsMessageStatusHistory
(
    Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_SmsMessageStatusHistory PRIMARY KEY,
    TenantId UNIQUEIDENTIFIER NOT NULL,
    MessageId UNIQUEIDENTIFIER NOT NULL,
    Status INT NOT NULL,
    CreatedAt DATETIMEOFFSET NOT NULL,
    CONSTRAINT FK_SmsMessageStatusHistory_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(Id),
    CONSTRAINT FK_SmsMessageStatusHistory_Messages FOREIGN KEY (MessageId) REFERENCES dbo.SmsMessages(Id)
);
GO

CREATE INDEX IX_SmsMessageStatusHistory_Tenant_Message_CreatedAt
    ON dbo.SmsMessageStatusHistory(TenantId, MessageId, CreatedAt);
GO

INSERT INTO dbo.SmsMessageStatusHistory (Id, TenantId, MessageId, Status, CreatedAt)
SELECT NEWID(), TenantId, Id, Status, COALESCE(UpdatedAt, CreatedAt)
FROM dbo.SmsMessages;
GO
