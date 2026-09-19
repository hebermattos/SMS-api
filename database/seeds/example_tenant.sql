-- Test-only bootstrap credentials:
-- client_id: client
-- client_secret: client
INSERT INTO Tenants(Id, Name, Code, TimeZoneId, IsActive, CreatedAt)
VALUES('11111111-1111-4111-8111-111111111111', 'Example Tenant', 'example', 'America/Sao_Paulo', TRUE, CURRENT_TIMESTAMP)
ON CONFLICT (Id) DO NOTHING;

INSERT INTO ApiClients(Id, TenantId, ClientId, SecretHash, SecretSalt, SecretIterations, IsActive, CreatedAt)
VALUES(
    gen_random_uuid(),
    '11111111-1111-4111-8111-111111111111',
    'client',
    decode('0AA0EB0E589F2DCD0F11DAAFA15A1B2FAE9B6F97BC15A5CCD97A635DA4EFF17D', 'hex'),
    decode('0102030405060708090A0B0C0D0E0F101112131415161718191A1B1C1D1E1F20', 'hex'),
    600000,
    TRUE,
    CURRENT_TIMESTAMP
)
ON CONFLICT (ClientId) DO NOTHING;
