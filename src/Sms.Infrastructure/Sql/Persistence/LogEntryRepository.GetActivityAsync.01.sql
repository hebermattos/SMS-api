SELECT Id, "Timestamp", Severity, Category, Message, TraceId, SpanId, Attributes
FROM UserActivityLogs
WHERE TenantId=@TenantId
  AND (@From IS NULL OR "Timestamp" >= @From)
  AND (@To IS NULL OR "Timestamp" < @To)
  AND (@CursorTimestamp IS NULL OR "Timestamp" < @CursorTimestamp OR ("Timestamp" = @CursorTimestamp AND Id < @CursorId))
ORDER BY "Timestamp" DESC, Id DESC
LIMIT @Take;
