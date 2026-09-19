WITH evaluation AS
(
    SELECT
        r.Id,
        r.TenantId,
        r.Name,
        r.Provider,
        r.Status,
        r.WindowMinutes,
        COALESCE(counts.MatchCount, 0)::INTEGER AS MatchCount
    FROM AlertRules r
    LEFT JOIN LATERAL
    (
        SELECT SUM(c.MessageCount) AS MatchCount
        FROM AlertStatusCounters c
        WHERE c.TenantId=r.TenantId
          AND c.Status=r.Status
          AND c.BucketStartUtc >= @OccurredAtUtc - make_interval(mins => r.WindowMinutes)
          AND (r.Provider IS NULL OR c.Provider=r.Provider)
    ) counts ON TRUE
    WHERE r.TenantId=@TenantId
      AND r.IsActive
      AND r.DeletedAt IS NULL
      AND r.Status=@Status
      AND (r.Provider IS NULL OR r.Provider=@Provider)
      AND
      (
          NOT r.IsTriggered
          OR
          (
              r.RepeatMode=2
              AND
              (
                  r.LastTriggeredAt IS NULL
                  OR r.LastTriggeredAt <= @OccurredAtUtc - make_interval(mins => r.RepeatIntervalMinutes)
              )
          )
      )
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
    RETURNING RuleId
)
UPDATE AlertRules r
SET IsTriggered=TRUE,
    LastTriggeredAt=@OccurredAtUtc,
    UpdatedAt=@OccurredAtUtc
FROM fired f
WHERE r.Id=f.RuleId
  AND r.TenantId=@TenantId;
