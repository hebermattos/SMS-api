UPDATE dbo.AlertRules SET Name=@Name,Provider=@Provider,Status=@Status,Threshold=@Threshold,
                WindowMinutes=@WindowMinutes,RepeatMode=@RepeatMode,RepeatIntervalMinutes=@RepeatIntervalMinutes,
                IsActive=@IsActive,IsTriggered=0,LastTriggeredAt=NULL,UpdatedAt=SYSUTCDATETIME()
            WHERE TenantId=@TenantId AND Id=@Id AND DeletedAt IS NULL;
