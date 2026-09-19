UPDATE PlatformAdministrators
SET PasswordHash=@Hash, PasswordSalt=@Salt, PasswordIterations=@Iterations
WHERE Id=@Id;
