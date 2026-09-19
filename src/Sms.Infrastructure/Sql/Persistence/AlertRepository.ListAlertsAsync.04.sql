SELECT Id, RuleId, RuleName, Provider, Status, MatchCount, WindowMinutes, CreatedAt, IsRead, ReadAt
            FROM dbo.Alerts
            WHERE TenantId=@TenantId AND (@UnreadOnly=0 OR IsRead=0)
            ORDER BY CreatedAt DESC OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY;
