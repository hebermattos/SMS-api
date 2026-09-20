SELECT RequestsPerMinute, SmsPerMinute
FROM TenantRateLimits
WHERE TenantId = @TenantId;
