SELECT
                COUNT_BIG(1) AS TotalMessages,
                COALESCE(SUM(CASE WHEN m.Status = 1 THEN 1 ELSE 0 END), 0) AS Queued,
                COALESCE(SUM(CASE WHEN m.Status = 2 THEN 1 ELSE 0 END), 0) AS Sent,
                COALESCE(SUM(CASE WHEN m.Status = 3 THEN 1 ELSE 0 END), 0) AS Delivered,
                COALESCE(SUM(CASE WHEN m.Status = 4 THEN 1 ELSE 0 END), 0) AS Failed,
                COALESCE(SUM(CASE WHEN m.Status = 5 THEN 1 ELSE 0 END), 0) AS Received
            FROM dbo.SmsMessages m
            WHERE (@From IS NULL OR m.CreatedAt >= @From)
              AND (@To IS NULL OR m.CreatedAt < @To)
              AND (@Status IS NULL OR m.Status = @Status)
              AND (@Direction IS NULL OR m.Direction = @Direction)
              AND (@Provider IS NULL OR m.Provider = @Provider);

            SELECT m.TenantId, t.Name AS TenantName,
                COUNT_BIG(1) AS TotalMessages,
                COALESCE(SUM(CASE WHEN m.Status = 1 THEN 1 ELSE 0 END), 0) AS Queued,
                COALESCE(SUM(CASE WHEN m.Status = 2 THEN 1 ELSE 0 END), 0) AS Sent,
                COALESCE(SUM(CASE WHEN m.Status = 3 THEN 1 ELSE 0 END), 0) AS Delivered,
                COALESCE(SUM(CASE WHEN m.Status = 4 THEN 1 ELSE 0 END), 0) AS Failed,
                COALESCE(SUM(CASE WHEN m.Status = 5 THEN 1 ELSE 0 END), 0) AS Received
            FROM dbo.SmsMessages m
            INNER JOIN dbo.Tenants t ON t.Id = m.TenantId
            WHERE (@From IS NULL OR m.CreatedAt >= @From)
              AND (@To IS NULL OR m.CreatedAt < @To)
              AND (@Status IS NULL OR m.Status = @Status)
              AND (@Direction IS NULL OR m.Direction = @Direction)
              AND (@Provider IS NULL OR m.Provider = @Provider)
            GROUP BY m.TenantId, t.Name
            ORDER BY t.Name;
