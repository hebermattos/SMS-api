SELECT CONVERT(bit, CASE WHEN EXISTS
(
    SELECT 1 FROM dbo.SmsOptOuts WHERE TenantId = @TenantId AND PhoneHash = @PhoneHash
) THEN 1 ELSE 0 END);
