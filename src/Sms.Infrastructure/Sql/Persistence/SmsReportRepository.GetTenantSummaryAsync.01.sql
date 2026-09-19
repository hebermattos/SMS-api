SELECT
                COUNT_BIG(1) AS TotalMessages,
                COALESCE(SUM(CASE WHEN Status = 1 THEN 1 ELSE 0 END), 0) AS Queued,
                COALESCE(SUM(CASE WHEN Status = 2 THEN 1 ELSE 0 END), 0) AS Sent,
                COALESCE(SUM(CASE WHEN Status = 3 THEN 1 ELSE 0 END), 0) AS Delivered,
                COALESCE(SUM(CASE WHEN Status = 4 THEN 1 ELSE 0 END), 0) AS Failed,
                COALESCE(SUM(CASE WHEN Status = 5 THEN 1 ELSE 0 END), 0) AS Received,
                COALESCE(SUM(CASE WHEN Direction = 1 THEN 1 ELSE 0 END), 0) AS Outbound,
                COALESCE(SUM(CASE WHEN Direction = 2 THEN 1 ELSE 0 END), 0) AS Inbound
            FROM dbo.SmsMessages
            WHERE TenantId = @TenantId
              AND (@From IS NULL OR CreatedAt >= @From)
              AND (@To IS NULL OR CreatedAt < @To)
              AND (@Status IS NULL OR Status = @Status)
              AND (@Direction IS NULL OR Direction = @Direction)
              AND (@Provider IS NULL OR Provider = @Provider);

            SELECT Provider,
                COUNT_BIG(1) AS TotalMessages,
                COALESCE(SUM(CASE WHEN Status = 3 THEN 1 ELSE 0 END), 0) AS Delivered,
                COALESCE(SUM(CASE WHEN Status = 4 THEN 1 ELSE 0 END), 0) AS Failed
            FROM dbo.SmsMessages
            WHERE TenantId = @TenantId
              AND (@From IS NULL OR CreatedAt >= @From)
              AND (@To IS NULL OR CreatedAt < @To)
              AND (@Status IS NULL OR Status = @Status)
              AND (@Direction IS NULL OR Direction = @Direction)
              AND (@Provider IS NULL OR Provider = @Provider)
            GROUP BY Provider
            ORDER BY Provider;
