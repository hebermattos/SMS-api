SELECT u.Id, u.TenantId, u.Username, u.Email, u.PasswordHash, u.PasswordSalt, u.PasswordIterations,
       u.Context, u.Role, u.IsActive
FROM dbo.PortalUsers u
LEFT JOIN dbo.Tenants t ON t.Id = u.TenantId
WHERE u.Username = @Username
  AND u.Context = @Context
  AND u.IsActive = 1
  AND
  (
      (@Context = 'platform' AND u.TenantId IS NULL)
      OR
      (@Context = 'tenant' AND t.Code = @TenantCode AND t.IsActive = 1)
  );
