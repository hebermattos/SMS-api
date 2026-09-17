CREATE TABLE dbo.ApiClients
(
    Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_ApiClients PRIMARY KEY,
    TenantId UNIQUEIDENTIFIER NOT NULL,
    ClientId NVARCHAR(100) NOT NULL,
    SecretHash VARBINARY(32) NOT NULL,
    SecretSalt VARBINARY(32) NOT NULL,
    SecretIterations INT NOT NULL,
    IsActive BIT NOT NULL CONSTRAINT DF_ApiClients_IsActive DEFAULT (1),
    CreatedAt DATETIMEOFFSET NOT NULL,
    UpdatedAt DATETIMEOFFSET NULL,
    CONSTRAINT FK_ApiClients_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(Id),
    CONSTRAINT CK_ApiClients_SecretIterations CHECK (SecretIterations >= 100000)
);
GO

CREATE UNIQUE INDEX UX_ApiClients_ClientId ON dbo.ApiClients(ClientId);
GO

CREATE INDEX IX_ApiClients_TenantId ON dbo.ApiClients(TenantId);
GO
