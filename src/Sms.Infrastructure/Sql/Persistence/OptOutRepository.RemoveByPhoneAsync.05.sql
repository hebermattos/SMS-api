DELETE FROM dbo.SmsOptOuts WHERE TenantId = @TenantId AND PhoneHash = @PhoneHash;
