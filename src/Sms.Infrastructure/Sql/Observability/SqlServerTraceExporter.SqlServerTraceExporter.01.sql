INSERT INTO Traces
    (StartedAt, DurationMilliseconds, TraceId, SpanId, ParentSpanId, Name, Source, Kind, Status, Attributes)
VALUES
    (@StartedAt, @DurationMilliseconds, @TraceId, @SpanId, @ParentSpanId, @Name, @Source, @Kind, @Status, @Attributes);
