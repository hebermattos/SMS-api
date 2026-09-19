INSERT INTO PlatformAdministrators
    (Id, Username, Email, PasswordHash, PasswordSalt, PasswordIterations, IsActive, CreatedAt)
VALUES
    (@Id, @Username, @Email, @PasswordHash, @PasswordSalt, @PasswordIterations, @IsActive, CURRENT_TIMESTAMP);
