SELECT COUNT(*) FROM dbo.PlatformAdministrators WITH (UPDLOCK, HOLDLOCK) WHERE IsActive=1;
