SELECT Id, MessageId, Status, CreatedAt
            FROM dbo.SmsMessageStatusHistory
            WHERE TenantId = @TenantId AND MessageId = @MessageId
            ORDER BY CreatedAt, Id;
