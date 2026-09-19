SET XACT_ABORT ON;
BEGIN TRANSACTION;

UPDATE dbo.SmsOptOuts WITH (UPDLOCK, SERIALIZABLE)
SET Source = @Source, Reason = @Reason, UpdatedAt = @OccurredAt
WHERE TenantId = @TenantId AND PhoneHash = @PhoneHash;

IF @@ROWCOUNT = 0
BEGIN
    INSERT dbo.SmsOptOuts(Id, TenantId, PhoneHash, PhoneNumber, Source, Reason, CreatedAt)
    VALUES (@Id, @TenantId, @PhoneHash, @PhoneNumber, @Source, @Reason, @OccurredAt);
END

COMMIT TRANSACTION;
