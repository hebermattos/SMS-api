SELECT
    UserId,
    Username,
    ReportDate,
    TotalMessages,
    Delivered,
    Failed,
    Pending,
    UpdatedAtUtc
FROM UserSmsOverview
WHERE TenantId=@TenantId
ORDER BY ReportDate DESC, TotalMessages DESC, Username, UserId;
