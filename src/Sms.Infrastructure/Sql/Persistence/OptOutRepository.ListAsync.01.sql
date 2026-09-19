SELECT Id, PhoneNumber, Source, Reason, CreatedAt, UpdatedAt
FROM dbo.SmsOptOuts
WHERE TenantId = @TenantId
ORDER BY CreatedAt DESC, Id DESC
OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY;
