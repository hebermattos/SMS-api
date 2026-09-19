SET XACT_ABORT ON;
            BEGIN TRANSACTION;

            DECLARE @ChangedMessages TABLE (Id UNIQUEIDENTIFIER, PreviousStatus INT, Provider NVARCHAR(50));

            UPDATE dbo.SmsMessages
            SET Status = @Status, ProviderMessageId = COALESCE(@ProviderMessageId, ProviderMessageId), UpdatedAt = @UpdatedAt
            OUTPUT INSERTED.Id, DELETED.Status, INSERTED.Provider INTO @ChangedMessages
            WHERE TenantId = @TenantId AND Id = @Id;

            INSERT INTO dbo.SmsMessageStatusHistory (Id, TenantId, MessageId, Status, CreatedAt)
            SELECT NEWID(), @TenantId, Id, @Status, @UpdatedAt
            FROM @ChangedMessages
            WHERE PreviousStatus <> @Status;

            DECLARE @BucketStartUtc DATETIMEOFFSET = DATEADD(MINUTE, DATEDIFF(MINUTE, 0, CAST(@UpdatedAt AS datetime2)), 0) AT TIME ZONE 'UTC';
            UPDATE c WITH (UPDLOCK, SERIALIZABLE)
            SET MessageCount = c.MessageCount + x.Amount, UpdatedAtUtc = @UpdatedAt
            FROM dbo.AlertStatusCounters c
            INNER JOIN (SELECT Provider, COUNT(*) AS Amount FROM @ChangedMessages WHERE PreviousStatus <> @Status AND @Status BETWEEN 1 AND 5 GROUP BY Provider) x
              ON x.Provider = c.Provider
            WHERE c.TenantId = @TenantId AND c.Status = @Status AND c.BucketStartUtc = @BucketStartUtc;
            INSERT dbo.AlertStatusCounters(TenantId, Provider, Status, BucketStartUtc, MessageCount, UpdatedAtUtc)
            SELECT @TenantId, x.Provider, @Status, @BucketStartUtc, x.Amount, @UpdatedAt
            FROM (SELECT Provider, COUNT(*) AS Amount FROM @ChangedMessages WHERE PreviousStatus <> @Status AND @Status BETWEEN 1 AND 5 GROUP BY Provider) x
            WHERE NOT EXISTS (SELECT 1 FROM dbo.AlertStatusCounters c WHERE c.TenantId=@TenantId AND c.Provider=x.Provider AND c.Status=@Status AND c.BucketStartUtc=@BucketStartUtc);

            COMMIT TRANSACTION;
