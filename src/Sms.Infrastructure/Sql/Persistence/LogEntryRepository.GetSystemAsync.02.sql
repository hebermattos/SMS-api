SELECT Id, "Timestamp", Severity, Category, Message, TraceId, SpanId, Attributes
FROM SystemLogs
WHERE (@From IS NULL OR "Timestamp" >= @From)
  AND (@To IS NULL OR "Timestamp" < @To)
ORDER BY "Timestamp" DESC, Id DESC
OFFSET @Skip LIMIT @Take;
