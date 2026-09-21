WITH evaluation AS
(
    SELECT
        r.Id,
        r.TenantId,
        r.Threshold,
        COALESCE(counts.MatchCount, 0)::INTEGER AS MatchCount
    FROM AlertRules r
    LEFT JOIN LATERAL
    (
        SELECT SUM(c.MessageCount) AS MatchCount
        FROM AlertStatusCounters c
        WHERE c.TenantId=r.TenantId
          AND c.Status=r.Status
          AND c.BucketStartUtc >= CURRENT_TIMESTAMP - make_interval(mins => r.WindowMinutes)
          AND c.BucketStartUtc <= CURRENT_TIMESTAMP
          AND (r.Provider IS NULL OR c.Provider=r.Provider)
    ) counts ON TRUE
    WHERE r.IsActive
      AND r.IsTriggered
      AND r.DeletedAt IS NULL
    FOR UPDATE OF r
)
UPDATE AlertRules r
SET IsTriggered=FALSE,
    UpdatedAt=CURRENT_TIMESTAMP
FROM evaluation e
WHERE r.Id=e.Id
  AND r.TenantId=e.TenantId
  AND e.MatchCount < e.Threshold;
