SELECT Id, Name, TimeZoneId, IsActive, CreatedAt FROM dbo.Tenants
            ORDER BY CreatedAt DESC, Id OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY;
