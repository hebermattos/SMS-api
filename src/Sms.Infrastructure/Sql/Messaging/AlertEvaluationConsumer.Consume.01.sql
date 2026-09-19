INSERT INTO AlertEvaluationInbox(EventId, ProcessedAtUtc)
VALUES(@EventId, CURRENT_TIMESTAMP)
ON CONFLICT (EventId) DO NOTHING;
