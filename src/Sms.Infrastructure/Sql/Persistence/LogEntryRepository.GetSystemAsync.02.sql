SELECT Id, [Timestamp], Severity, Category, Message, TraceId, SpanId, Attributes
            FROM dbo.SystemLogs
            WHERE (@From IS NULL OR [Timestamp] >= @From)
              AND (@To IS NULL OR [Timestamp] < @To)
            ORDER BY [Timestamp] DESC, Id DESC
            OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY;
