SELECT IsActive
FROM PlatformAdministrators
WHERE Id=@Id
FOR UPDATE;
