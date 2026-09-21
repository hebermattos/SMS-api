SELECT Id, Username, Email, PasswordHash, PasswordSalt, PasswordIterations, IsActive
FROM PlatformAdministrators
WHERE LOWER(Username)=LOWER(@Username);
