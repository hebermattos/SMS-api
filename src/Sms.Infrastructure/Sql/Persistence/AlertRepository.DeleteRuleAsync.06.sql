UPDATE dbo.AlertRules
SET IsActive = 0,
    IsTriggered = 0,
    DeletedAt = SYSUTCDATETIME(),
    UpdatedAt = SYSUTCDATETIME()
WHERE TenantId = @TenantId
  AND Id = @Id
  AND DeletedAt IS NULL;
