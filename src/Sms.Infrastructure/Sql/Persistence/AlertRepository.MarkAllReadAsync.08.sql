UPDATE dbo.Alerts SET IsRead=1,ReadAt=SYSUTCDATETIME() WHERE TenantId=@TenantId AND IsRead=0;
