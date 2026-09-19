SELECT Id, ClientId, IsActive, CreatedAt FROM dbo.ApiClients WHERE TenantId=@TenantId
            ORDER BY CreatedAt DESC, Id OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY;
