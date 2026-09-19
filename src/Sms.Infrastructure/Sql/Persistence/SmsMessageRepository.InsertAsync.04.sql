SET XACT_ABORT ON;
            BEGIN TRANSACTION;

            INSERT INTO dbo.SmsMessages (Id, TenantId, [From], [To], Body, Provider, ProviderMessageId, Direction, Status, CreatedAt, ScheduledAtUtc, UpdatedAt)
            VALUES (@Id, @TenantId, @From, @To, @Body, @Provider, @ProviderMessageId, @Direction, @Status, @CreatedAt, @ScheduledAtUtc, @UpdatedAt);

            INSERT INTO dbo.SmsMessageStatusHistory (Id, TenantId, MessageId, Status, CreatedAt)
            VALUES (NEWID(), @TenantId, @Id, @Status, @CreatedAt);
            IF @Status BETWEEN 1 AND 5
            BEGIN
            DECLARE @BucketStartUtc DATETIMEOFFSET = DATEADD(MINUTE, DATEDIFF(MINUTE, 0, CAST(@CreatedAt AS datetime2)), 0) AT TIME ZONE 'UTC';
            UPDATE dbo.AlertStatusCounters WITH (UPDLOCK, SERIALIZABLE)
            SET MessageCount = MessageCount + 1, UpdatedAtUtc = @CreatedAt
            WHERE TenantId = @TenantId AND Provider = @Provider AND Status = @Status AND BucketStartUtc = @BucketStartUtc;
            IF NOT EXISTS (SELECT 1 FROM dbo.AlertStatusCounters WHERE TenantId=@TenantId AND Provider=@Provider AND Status=@Status AND BucketStartUtc=@BucketStartUtc)
                INSERT dbo.AlertStatusCounters(TenantId, Provider, Status, BucketStartUtc, MessageCount, UpdatedAtUtc)
                VALUES (@TenantId, @Provider, @Status, @BucketStartUtc, 1, @CreatedAt);
            END;

            COMMIT TRANSACTION;
