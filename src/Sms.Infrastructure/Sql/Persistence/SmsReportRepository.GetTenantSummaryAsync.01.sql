SELECT
    Provider,
    COUNT(*) AS TotalMessages,
    COALESCE(SUM(CASE WHEN QueueStatus=3 THEN 1 ELSE 0 END), 0)::INTEGER AS Scheduled,
    COALESCE(SUM(CASE WHEN QueueStatus=2 THEN 1 ELSE 0 END), 0)::INTEGER AS Queued,
    COALESCE(SUM(CASE WHEN Status=2 THEN 1 ELSE 0 END), 0)::INTEGER AS Sent,
    COALESCE(SUM(CASE WHEN Status=3 THEN 1 ELSE 0 END), 0)::INTEGER AS Delivered,
    COALESCE(SUM(CASE WHEN Status=4 THEN 1 ELSE 0 END), 0)::INTEGER AS Failed,
    COALESCE(SUM(CASE WHEN Status=5 THEN 1 ELSE 0 END), 0)::INTEGER AS Received,
    COALESCE(SUM(CASE WHEN Direction=1 THEN 1 ELSE 0 END), 0)::INTEGER AS Outbound,
    COALESCE(SUM(CASE WHEN Direction=2 THEN 1 ELSE 0 END), 0)::INTEGER AS Inbound
FROM ReportingSmsMessages
WHERE TenantId=@TenantId
  AND (@From IS NULL OR CreatedAtUtc >= @From)
  AND (@To IS NULL OR CreatedAtUtc < @To)
  AND (@Status IS NULL OR Status=@Status)
  AND (@Direction IS NULL OR Direction=@Direction)
  AND (@Provider IS NULL OR Provider=@Provider)
GROUP BY Provider
ORDER BY Provider;
