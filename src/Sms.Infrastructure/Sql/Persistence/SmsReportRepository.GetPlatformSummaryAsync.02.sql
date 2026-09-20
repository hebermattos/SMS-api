SELECT
    m.TenantId,
    t.Name AS TenantName,
    COUNT(*) AS TotalMessages,
    COALESCE(SUM(CASE WHEN m.Status=6 THEN 1 ELSE 0 END), 0)::INTEGER AS Scheduled,
    COALESCE(SUM(CASE WHEN m.Status=1 THEN 1 ELSE 0 END), 0)::INTEGER AS Queued,
    COALESCE(SUM(CASE WHEN m.Status=2 THEN 1 ELSE 0 END), 0)::INTEGER AS Sent,
    COALESCE(SUM(CASE WHEN m.Status=3 THEN 1 ELSE 0 END), 0)::INTEGER AS Delivered,
    COALESCE(SUM(CASE WHEN m.Status=4 THEN 1 ELSE 0 END), 0)::INTEGER AS Failed,
    COALESCE(SUM(CASE WHEN m.Status=5 THEN 1 ELSE 0 END), 0)::INTEGER AS Received
FROM SmsMessages m
INNER JOIN Tenants t ON t.Id=m.TenantId
WHERE (@From IS NULL OR m.CreatedAt >= @From)
  AND (@To IS NULL OR m.CreatedAt < @To)
  AND (@Status IS NULL OR m.Status=@Status)
  AND (@Direction IS NULL OR m.Direction=@Direction)
  AND (@Provider IS NULL OR m.Provider=@Provider)
GROUP BY m.TenantId, t.Name
ORDER BY t.Name;
