SELECT TOP (100) Id AS EventId, TenantId, Provider, Status, OccurredAtUtc
            FROM dbo.AlertEvaluationOutbox WITH (READPAST, UPDLOCK, ROWLOCK)
            WHERE PublishedAtUtc IS NULL
            ORDER BY CreatedAtUtc, Id;
