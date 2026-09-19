SELECT Id, TenantId, Name, Provider, Status, Threshold, WindowMinutes, RepeatMode,
                   RepeatIntervalMinutes, IsActive, IsTriggered, LastTriggeredAt, CreatedAt, UpdatedAt
            FROM dbo.AlertRules
            WHERE TenantId=@TenantId AND DeletedAt IS NULL
            ORDER BY CreatedAt DESC;
