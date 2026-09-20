SELECT
    r.UserId,
    u.Username,
    r.TotalMessages,
    r.Delivered,
    r.Failed,
    r.Pending,
    r.UpdatedAtUtc
FROM UserSmsOverview r
JOIN PortalUsers u ON u.TenantId=r.TenantId AND u.Id=r.UserId
WHERE r.TenantId=@TenantId
ORDER BY r.TotalMessages DESC, u.Username, r.UserId;
