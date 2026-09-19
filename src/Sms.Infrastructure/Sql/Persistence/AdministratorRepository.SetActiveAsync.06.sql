SELECT IsActive FROM dbo.PlatformAdministrators WITH (UPDLOCK, HOLDLOCK) WHERE Id=@Id;
