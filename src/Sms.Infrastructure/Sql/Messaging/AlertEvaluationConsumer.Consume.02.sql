WITH evaluation AS
(
    SELECT
        r.Id,
        r.TenantId,
        r.Name,
        r.Provider,
        r.Status,
        r.WindowMinutes,
        r.RepeatMode,
        r.RepeatIntervalMinutes,
        r.IsTriggered,
        r.LastTriggeredAt,
        COALESCE(counts.MatchCount, 0)::INTEGER AS MatchCount
    FROM AlertRules r
    LEFT JOIN LATERAL
    (
        SELECT SUM(c.MessageCount) AS MatchCount
        FROM AlertStatusCounters c
        WHERE c.TenantId=r.TenantId
          AND c.Status=r.Status
          AND c.BucketStartUtc >= @OccurredAtUtc - make_interval(mins => r.WindowMinutes)
          AND c.BucketStartUtc <= @OccurredAtUtc
          AND (r.Provider IS NULL OR c.Provider=r.Provider)
    ) counts ON TRUE
    WHERE r.TenantId=@TenantId
      AND r.IsActive
      AND r.DeletedAt IS NULL
      AND r.Status=@Status
      AND (r.Provider IS NULL OR r.Provider=@Provider)
      AND COALESCE(counts.MatchCount, 0) >= r.Threshold
    FOR UPDATE OF r
),
fired AS
(
    INSERT INTO Alerts
        (Id, TenantId, RuleId, RuleName, Provider, Status, MatchCount, WindowMinutes, CreatedAt, IsRead)
    SELECT
        gen_random_uuid(),
        TenantId,
        Id,
        Name,
        Provider,
        Status,
        MatchCount,
        WindowMinutes,
        @OccurredAtUtc,
        FALSE
    FROM evaluation
    WHERE NOT IsTriggered
       OR
       (
           RepeatMode=2
           AND
           (
               LastTriggeredAt IS NULL
               OR LastTriggeredAt <= @OccurredAtUtc - make_interval(mins => RepeatIntervalMinutes)
           )
       )
    RETURNING RuleId
)
UPDATE AlertRules r
SET IsTriggered=CASE WHEN e.MatchCount >= r.Threshold THEN TRUE ELSE FALSE END,
    LastTriggeredAt=CASE WHEN f.RuleId IS NOT NULL THEN @OccurredAtUtc ELSE r.LastTriggeredAt END,
    UpdatedAt=CASE
        WHEN f.RuleId IS NOT NULL OR (r.IsTriggered AND e.MatchCount < r.Threshold) THEN @OccurredAtUtc
        ELSE r.UpdatedAt
    END
FROM evaluation e
LEFT JOIN fired f ON f.RuleId=e.Id
WHERE r.Id=e.Id
  AND r.TenantId=@TenantId;
