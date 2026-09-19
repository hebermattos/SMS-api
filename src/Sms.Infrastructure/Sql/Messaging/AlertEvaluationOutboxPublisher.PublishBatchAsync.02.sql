UPDATE dbo.AlertEvaluationOutbox SET PublishedAtUtc=SYSUTCDATETIME(), AttemptCount=AttemptCount+1, LastAttemptAtUtc=SYSUTCDATETIME() WHERE Id=@Id AND PublishedAtUtc IS NULL;
