SELECT Outbound, Inbound, Delivered, Failed, Pending
FROM dbo.TenantSmsOverview
WHERE TenantId = @TenantId;
