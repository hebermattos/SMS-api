SELECT Name FROM dbo.Tenants WHERE Id=@TenantId AND IsActive=1;
            SELECT
                COUNT_BIG(CASE WHEN Direction=@Outbound THEN 1 END) AS Outbound,
                COUNT_BIG(CASE WHEN Direction=@Inbound THEN 1 END) AS Inbound,
                COUNT_BIG(CASE WHEN Direction=@Outbound AND Status=@Delivered THEN 1 END) AS Delivered,
                COUNT_BIG(CASE WHEN Direction=@Outbound AND Status=@Failed THEN 1 END) AS Failed,
                COUNT_BIG(CASE WHEN Direction=@Outbound AND Status IN (@Queued, @Sent) THEN 1 END) AS Pending
            FROM dbo.SmsMessages WHERE TenantId=@TenantId;
            SELECT Provider AS Name, FromNumber, IsDefault FROM dbo.TenantSmsProviders
            WHERE TenantId=@TenantId AND IsActive=1 ORDER BY IsDefault DESC, Provider;
