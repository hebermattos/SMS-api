using Dapper;
using Microsoft.Data.SqlClient;
using Sms.Application.Administration;
using Sms.Application.Auth;
using Sms.Application.Providers;
using Sms.Application.Security;

namespace Sms.Infrastructure.Persistence;

public sealed class AdministrationRepository(SqlConnectionFactory factory, ISecretProtector protector) : IAdministrationRepository
{
    public async Task<IReadOnlyList<TenantSummary>> ListTenantsAsync(int skip, int take, CancellationToken cancellationToken)
    {
        // This cross-tenant metadata query is exposed only by the PlatformAdmin policy.
        using var connection = factory.CreateConnection();
        return (await connection.QueryAsync<TenantSummary>(new CommandDefinition("""
            SELECT Id, Name, TimeZoneId, IsActive, CreatedAt FROM dbo.Tenants
            ORDER BY CreatedAt DESC, Id OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY;
            """, new { Skip = skip, Take = take }, cancellationToken: cancellationToken))).AsList();
    }

    public async Task<TenantSummary?> GetTenantAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        using var connection = factory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<TenantSummary>(new CommandDefinition(
            "SELECT Id, Name, TimeZoneId, IsActive, CreatedAt FROM dbo.Tenants WHERE Id=@TenantId;", new { TenantId = tenantId }, cancellationToken: cancellationToken));
    }

    public async Task<bool> UpdateTenantAsync(Guid tenantId, string name, string timeZoneId, bool isActive, CancellationToken cancellationToken)
    {
        using var connection = factory.CreateConnection();
        return await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE dbo.Tenants SET Name=@Name, TimeZoneId=@TimeZoneId, IsActive=@IsActive WHERE Id=@TenantId;",
            new { TenantId = tenantId, Name = name, TimeZoneId = timeZoneId, IsActive = isActive }, cancellationToken: cancellationToken)) == 1;
    }

    public async Task<IReadOnlyList<ClientSummary>> ListClientsAsync(Guid tenantId, int skip, int take, CancellationToken cancellationToken)
    {
        using var connection = factory.CreateConnection();
        return (await connection.QueryAsync<ClientSummary>(new CommandDefinition("""
            SELECT Id, ClientId, IsActive, CreatedAt FROM dbo.ApiClients WHERE TenantId=@TenantId
            ORDER BY CreatedAt DESC, Id OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY;
            """, new { TenantId = tenantId, Skip = skip, Take = take }, cancellationToken: cancellationToken))).AsList();
    }

    public async Task CreateClientAsync(CreateApiClient client, CancellationToken cancellationToken)
    {
        using var connection = factory.CreateConnection();
        try
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                INSERT dbo.ApiClients(Id, TenantId, ClientId, SecretHash, SecretSalt, SecretIterations, IsActive, CreatedAt)
                VALUES(@Id, @TenantId, @ClientId, @SecretHash, @SecretSalt, @SecretIterations, 1, @Now);
                """, new { Id = Guid.NewGuid(), client.TenantId, client.ClientId, client.SecretHash, client.SecretSalt,
                    client.SecretIterations, Now = DateTimeOffset.UtcNow }, cancellationToken: cancellationToken));
        }
        catch (SqlException exception) when (exception.Number is 2601 or 2627) { throw new AdministrationConflictException(); }
    }

    public async Task<bool> SetClientActiveAsync(Guid tenantId, Guid clientId, bool isActive, CancellationToken cancellationToken)
    {
        using var connection = factory.CreateConnection();
        return await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE dbo.ApiClients SET IsActive=@IsActive, UpdatedAt=@Now WHERE TenantId=@TenantId AND Id=@ClientId;
            """, new { TenantId = tenantId, ClientId = clientId, IsActive = isActive, Now = DateTimeOffset.UtcNow }, cancellationToken: cancellationToken)) == 1;
    }

    public async Task<string?> RotateClientSecretAsync(Guid tenantId, Guid clientId, byte[] hash, byte[] salt, int iterations, CancellationToken cancellationToken)
    {
        using var connection = factory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<string>(new CommandDefinition("""
            UPDATE dbo.ApiClients SET SecretHash=@Hash, SecretSalt=@Salt, SecretIterations=@Iterations, UpdatedAt=@Now
            OUTPUT INSERTED.ClientId WHERE TenantId=@TenantId AND Id=@ClientId;
            """, new { TenantId = tenantId, ClientId = clientId, Hash = hash, Salt = salt, Iterations = iterations, Now = DateTimeOffset.UtcNow }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<TenantSmsProviderConfiguration>> ListProvidersAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        using var connection = factory.CreateConnection();
        var values = await connection.QueryAsync<TenantSmsProviderConfiguration>(new CommandDefinition("""
            SELECT TenantId, Provider, AccountId, ApiSecret, FromNumber, IsDefault, IsActive, Settings
            FROM dbo.TenantSmsProviders WHERE TenantId=@TenantId ORDER BY Provider;
            """, new { TenantId = tenantId }, cancellationToken: cancellationToken));
        return values.Select(value => value with
        {
            ApiSecret = protector.Unprotect(value.ApiSecret),
            Settings = string.IsNullOrWhiteSpace(value.Settings) ? null : protector.Unprotect(value.Settings)
        }).ToArray();
    }
}
