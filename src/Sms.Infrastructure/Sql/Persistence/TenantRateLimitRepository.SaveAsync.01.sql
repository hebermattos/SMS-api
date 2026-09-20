INSERT INTO TenantRateLimits (TenantId, RequestsPerMinute, SmsPerMinute, UpdatedAt)
VALUES (@TenantId, @RequestsPerMinute, @SmsPerMinute, CURRENT_TIMESTAMP)
ON CONFLICT (TenantId) DO UPDATE
SET RequestsPerMinute = EXCLUDED.RequestsPerMinute,
    SmsPerMinute = EXCLUDED.SmsPerMinute,
    UpdatedAt = CURRENT_TIMESTAMP;
