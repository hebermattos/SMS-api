WITH evaluation AS
(
    SELECT
        r.Id,
        r.TenantId,
        r.Name,
        r.Provider,
        r.Status,
        r.Threshold,
        r.WindowMinutes,
        r.RepeatMode,
        r.RepeatIntervalMinutes,
        r.IsTriggered,
        r.LastTriggeredAt,
        COALESCE(counts.MatchCount, 0)::INTEGER AS MatchCount,
        (
            COALESCE(counts.MatchCount, 0) >= r.Threshold
            AND
            (
                NOT r.IsTriggered
                OR
                (
                    r.RepeatMode=2
                    AND
                    (
                        r.LastTriggeredAt IS NULL
                        OR r.LastTriggeredAt <= CURRENT_TIMESTAMP - make_interval(mins => r.RepeatIntervalMinutes)
                    )
                )
            )
        ) AS ShouldFire
    FROM AlertRules r
    LEFT JOIN LATERAL
    (
        SELECT SUM(c.MessageCount) AS MatchCount
        FROM AlertStatusCounters c
        WHERE c.TenantId=r.TenantId
          AND c.Status=r.Status
          AND c.BucketStartUtc >= CURRENT_TIMESTAMP - make_interval(mins => r.WindowMinutes)
          AND (r.Provider IS NULL OR c.Provider=r.Provider)
    ) counts ON TRUE
    WHERE r.TenantId=@TenantId
      AND r.IsActive
      AND r.DeletedAt IS NULL
    FOR UPDATE OF r
),
fired AS
(
    INSERT INTO Alerts
        (Id, TenantId, RuleId, RuleName, Provider, Status, MatchCount, WindowMinutes, CreatedAt, IsRead)
    SELECT
        gen_random_uuid(),
        e.TenantId,
        e.Id,
        e.Name,
        e.Provider,
        e.Status,
        e.MatchCount,
        e.WindowMinutes,
        CURRENT_TIMESTAMP,
        FALSE
    FROM evaluation e
    WHERE e.ShouldFire
    RETURNING RuleId
)
UPDATE AlertRules r
SET IsTriggered=(e.MatchCount >= e.Threshold),
    LastTriggeredAt=CASE WHEN f.RuleId IS NOT NULL THEN CURRENT_TIMESTAMP ELSE r.LastTriggeredAt END,
    UpdatedAt=CASE
        WHEN f.RuleId IS NOT NULL OR (r.IsTriggered AND e.MatchCount < e.Threshold)
            THEN CURRENT_TIMESTAMP
        ELSE r.UpdatedAt
    END
FROM evaluation e
LEFT JOIN fired f ON f.RuleId=e.Id
WHERE r.Id=e.Id
  AND r.TenantId=@TenantId;
