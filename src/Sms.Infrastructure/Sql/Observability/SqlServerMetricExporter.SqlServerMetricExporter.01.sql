INSERT INTO dbo.Metrics
            ([Timestamp], Name, Unit, MetricType, Value, [Count], Attributes)
        VALUES
            (@Timestamp, @Name, @Unit, @MetricType, @Value, @Count, @Attributes);
