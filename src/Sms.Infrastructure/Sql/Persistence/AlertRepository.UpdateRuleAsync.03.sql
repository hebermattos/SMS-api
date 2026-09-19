UPDATE AlertRules
SET Name=@Name,
    Provider=@Provider,
    Status=@Status,
    Threshold=@Threshold,
    WindowMinutes=@WindowMinutes,
    RepeatMode=@RepeatMode,
    RepeatIntervalMinutes=@RepeatIntervalMinutes,
    IsActive=@IsActive,
    IsTriggered=FALSE,
    LastTriggeredAt=NULL,
    UpdatedAt=CURRENT_TIMESTAMP
WHERE TenantId=@TenantId
  AND Id=@Id
  AND DeletedAt IS NULL;
