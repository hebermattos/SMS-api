SET XACT_ABORT ON;
            BEGIN TRANSACTION;

            DECLARE @InsertedMessages TABLE (Id UNIQUEIDENTIFIER, Provider NVARCHAR(50));

            IF NOT EXISTS (
                SELECT 1 FROM dbo.SmsMessages WITH (UPDLOCK, HOLDLOCK)
                WHERE TenantId = @TenantId AND Provider = @Provider AND ProviderMessageId = @ProviderMessageId
            )
            BEGIN
                INSERT INTO dbo.SmsMessages (Id, TenantId, [From], [To], Body, Provider, ProviderMessageId, Direction, Status, CreatedAt, ScheduledAtUtc, UpdatedAt)
                OUTPUT INSERTED.Id, INSERTED.Provider INTO @InsertedMessages
                VALUES (@Id, @TenantId, @From, @To, @Body, @Provider, @ProviderMessageId, @Direction, @Status, @CreatedAt, @ScheduledAtUtc, @UpdatedAt);
            END

            INSERT INTO dbo.SmsMessageStatusHistory (Id, TenantId, MessageId, Status, CreatedAt)
            SELECT NEWID(), @TenantId, Id, @Status, @CreatedAt FROM @InsertedMessages;

            DECLARE @BucketStartUtc DATETIMEOFFSET = DATEADD(MINUTE, DATEDIFF(MINUTE, 0, CAST(@CreatedAt AS datetime2)), 0) AT TIME ZONE 'UTC';
            UPDATE c WITH (UPDLOCK, SERIALIZABLE)
            SET MessageCount = MessageCount + 1, UpdatedAtUtc = @CreatedAt
            FROM dbo.AlertStatusCounters c
            INNER JOIN @InsertedMessages i ON i.Provider = c.Provider
            WHERE c.TenantId = @TenantId AND c.Status = @Status AND c.BucketStartUtc = @BucketStartUtc;
            INSERT dbo.AlertStatusCounters(TenantId, Provider, Status, BucketStartUtc, MessageCount, UpdatedAtUtc)
            SELECT @TenantId, i.Provider, @Status, @BucketStartUtc, COUNT(*), @CreatedAt
            FROM @InsertedMessages i
            WHERE NOT EXISTS (SELECT 1 FROM dbo.AlertStatusCounters c WHERE c.TenantId=@TenantId AND c.Provider=i.Provider AND c.Status=@Status AND c.BucketStartUtc=@BucketStartUtc)
            GROUP BY i.Provider;

            COMMIT TRANSACTION;
