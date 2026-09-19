SELECT EXISTS
(
    SELECT 1
    FROM PlatformAdministrators
    WHERE Id=@Id AND IsActive
);
