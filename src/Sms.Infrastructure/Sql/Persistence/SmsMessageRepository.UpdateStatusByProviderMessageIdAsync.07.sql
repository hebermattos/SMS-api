SET XACT_ABORT ON;
            BEGIN TRANSACTION;

            DECLARE @ChangedMessages TABLE (Id UNIQUEIDENTIFIER, PreviousStatus INT, Provider NVARCHAR(50));

            UPDATE dbo.SmsMessages
            SET Status = @Status, UpdatedAt = @UpdatedAt
            OUTPUT INSERTED.Id, DELETED.Status, INSERTED.Provider INTO @ChangedMessages
            WHERE TenantId = @TenantId AND Provider = @Provider AND ProviderMessageId = @ProviderMessageId
              AND (
                    Status = @Queued
                    OR (Status = @Sent AND @Status IN (@Delivered, @Failed))
                  );

            INSERT INTO dbo.SmsMessageStatusHistory (Id, TenantId, MessageId, Status, CreatedAt)
            SELECT NEWID(), @TenantId, Id, @Status, @UpdatedAt
            FROM @ChangedMessages
            WHERE PreviousStatus <> @Status;

            DECLARE @BucketStartUtc DATETIMEOFFSET = DATEADD(MINUTE, DATEDIFF(MINUTE, 0, CAST(@UpdatedAt AS datetime2)), 0) AT TIME ZONE 'UTC';
            UPDATE c WITH (UPDLOCK, SERIALIZABLE)
            SET MessageCount = c.MessageCount + x.Amount, UpdatedAtUtc = @UpdatedAt
            FROM dbo.AlertStatusCounters c
            INNER JOIN (SELECT Provider, COUNT(*) AS Amount FROM @ChangedMessages WHERE PreviousStatus <> @Status GROUP BY Provider) x
              ON x.Provider = c.Provider
            WHERE c.TenantId = @TenantId AND c.Status = @Status AND c.BucketStartUtc = @BucketStartUtc;
            IF @@ROWCOUNT = 0
                INSERT dbo.AlertStatusCounters(TenantId, Provider, Status, BucketStartUtc, MessageCount, UpdatedAtUtc)
                SELECT @TenantId, Provider, @Status, @BucketStartUtc, COUNT(*), @UpdatedAt
                FROM @ChangedMessages WHERE PreviousStatus <> @Status GROUP BY Provider;

            COMMIT TRANSACTION;
