IF DB_ID(N'SmsApi') IS NULL CREATE DATABASE SmsApi;
GO
USE SmsApi;
GO
:r /database/001_initial.sql
:r /database/002_tenant_providers.sql
:r /database/003_api_clients.sql
:r /database/004_example_tenant.sql
