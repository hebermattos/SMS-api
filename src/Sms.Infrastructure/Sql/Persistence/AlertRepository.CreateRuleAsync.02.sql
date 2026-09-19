INSERT INTO AlertRules
    (Id, TenantId, Name, Provider, Status, Threshold, WindowMinutes, RepeatMode, RepeatIntervalMinutes, IsActive, IsTriggered, CreatedAt)
VALUES
    (@Id, @TenantId, @Name, @Provider, @Status, @Threshold, @WindowMinutes, @RepeatMode, @RepeatIntervalMinutes, @IsActive, FALSE, @CreatedAt);
