INSERT INTO Alerts
    (Id, TenantId, RuleId, RuleName, Provider, Status, MatchCount, WindowMinutes, CreatedAt, IsRead)
VALUES
    (gen_random_uuid(), @TenantId, @RuleId, @RuleName, @Provider, @Status, @MatchCount, @WindowMinutes, @OccurredAtUtc, FALSE);
