SELECT
    Provider,
    COALESCE(SUM(TotalMessages), 0)::BIGINT AS TotalMessages,
    COALESCE(SUM(CASE WHEN QueueStatus=3 THEN TotalMessages ELSE 0 END), 0)::INTEGER AS Scheduled,
    COALESCE(SUM(CASE WHEN QueueStatus=2 THEN TotalMessages ELSE 0 END), 0)::INTEGER AS Queued,
    COALESCE(SUM(CASE WHEN Status=2 THEN TotalMessages ELSE 0 END), 0)::INTEGER AS Sent,
    COALESCE(SUM(CASE WHEN Status=3 THEN TotalMessages ELSE 0 END), 0)::INTEGER AS Delivered,
    COALESCE(SUM(CASE WHEN Status=4 THEN TotalMessages ELSE 0 END), 0)::INTEGER AS Failed,
    COALESCE(SUM(CASE WHEN Status=5 THEN TotalMessages ELSE 0 END), 0)::INTEGER AS Received,
    COALESCE(SUM(CASE WHEN Direction=1 THEN TotalMessages ELSE 0 END), 0)::INTEGER AS Outbound,
    COALESCE(SUM(CASE WHEN Direction=2 THEN TotalMessages ELSE 0 END), 0)::INTEGER AS Inbound
FROM SmsDailyOverview
WHERE TenantId=@TenantId
  AND (@From IS NULL OR ReportDate >= CAST(@From AT TIME ZONE 'UTC' AS date))
  AND (@To IS NULL OR ReportDate < CAST(@To AT TIME ZONE 'UTC' AS date))
  AND (@Status IS NULL OR Status=@Status)
  AND (@Direction IS NULL OR Direction=@Direction)
  AND (@Provider IS NULL OR Provider=@Provider)
GROUP BY Provider
ORDER BY Provider;
