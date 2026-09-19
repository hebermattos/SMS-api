SELECT Id,Username,Email,PasswordHash,PasswordSalt,PasswordIterations,IsActive FROM dbo.PlatformAdministrators WHERE Username=@Username;
