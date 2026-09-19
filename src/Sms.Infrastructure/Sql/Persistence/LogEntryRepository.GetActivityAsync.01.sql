SELECT Id, "Timestamp", Severity, Category, Message, TraceId, SpanId, Attributes
FROM UserActivityLogs
WHERE TenantId=@TenantId
  AND (@From IS NULL OR "Timestamp" >= @From)
  AND (@To IS NULL OR "Timestamp" < @To)
ORDER BY "Timestamp" DESC, Id DESC
OFFSET @Skip LIMIT @Take;
