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
        return (await connection.QueryAsync<TenantSummary>(new CommandDefinition(Sms.Infrastructure.Sql.SqlQuery.Load("Persistence/AdministrationRepository.ListTenantsAsync.01.sql"), new { Skip = skip, Take = take }, cancellationToken: cancellationToken))).AsList();
    }

    public async Task<TenantSummary?> GetTenantAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        using var connection = factory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<TenantSummary>(new CommandDefinition(
            Sms.Infrastructure.Sql.SqlQuery.Load("Persistence/AdministrationRepository.GetTenantAsync.07.sql"), new { TenantId = tenantId }, cancellationToken: cancellationToken));
    }

    public async Task<bool> UpdateTenantAsync(Guid tenantId, string name, string timeZoneId, bool isActive, CancellationToken cancellationToken)
    {
        using var connection = factory.CreateConnection();
        return await connection.ExecuteAsync(new CommandDefinition(
            Sms.Infrastructure.Sql.SqlQuery.Load("Persistence/AdministrationRepository.UpdateTenantAsync.08.sql"),
            new { TenantId = tenantId, Name = name, TimeZoneId = timeZoneId, IsActive = isActive }, cancellationToken: cancellationToken)) == 1;
    }

    public async Task<IReadOnlyList<ClientSummary>> ListClientsAsync(Guid tenantId, int skip, int take, CancellationToken cancellationToken)
    {
        using var connection = factory.CreateConnection();
        return (await connection.QueryAsync<ClientSummary>(new CommandDefinition(Sms.Infrastructure.Sql.SqlQuery.Load("Persistence/AdministrationRepository.ListClientsAsync.02.sql"), new { TenantId = tenantId, Skip = skip, Take = take }, cancellationToken: cancellationToken))).AsList();
    }

    public async Task CreateClientAsync(CreateApiClient client, CancellationToken cancellationToken)
    {
        using var connection = factory.CreateConnection();
        try
        {
            await connection.ExecuteAsync(new CommandDefinition(Sms.Infrastructure.Sql.SqlQuery.Load("Persistence/AdministrationRepository.CreateClientAsync.03.sql"), new { Id = Guid.NewGuid(), client.TenantId, client.ClientId, client.SecretHash, client.SecretSalt,
                    client.SecretIterations, Now = DateTimeOffset.UtcNow }, cancellationToken: cancellationToken));
        }
        catch (SqlException exception) when (exception.Number is 2601 or 2627) { throw new AdministrationConflictException(); }
    }

    public async Task<bool> SetClientActiveAsync(Guid tenantId, Guid clientId, bool isActive, CancellationToken cancellationToken)
    {
        using var connection = factory.CreateConnection();
        return await connection.ExecuteAsync(new CommandDefinition(Sms.Infrastructure.Sql.SqlQuery.Load("Persistence/AdministrationRepository.SetClientActiveAsync.04.sql"), new { TenantId = tenantId, ClientId = clientId, IsActive = isActive, Now = DateTimeOffset.UtcNow }, cancellationToken: cancellationToken)) == 1;
    }

    public async Task<string?> RotateClientSecretAsync(Guid tenantId, Guid clientId, byte[] hash, byte[] salt, int iterations, CancellationToken cancellationToken)
    {
        using var connection = factory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<string>(new CommandDefinition(Sms.Infrastructure.Sql.SqlQuery.Load("Persistence/AdministrationRepository.RotateClientSecretAsync.05.sql"), new { TenantId = tenantId, ClientId = clientId, Hash = hash, Salt = salt, Iterations = iterations, Now = DateTimeOffset.UtcNow }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<TenantSmsProviderConfiguration>> ListProvidersAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        using var connection = factory.CreateConnection();
        var values = await connection.QueryAsync<TenantSmsProviderConfiguration>(new CommandDefinition(Sms.Infrastructure.Sql.SqlQuery.Load("Persistence/AdministrationRepository.ListProvidersAsync.06.sql"), new { TenantId = tenantId }, cancellationToken: cancellationToken));
        return values.Select(value => value with
        {
            ApiSecret = protector.Unprotect(value.ApiSecret),
            Settings = string.IsNullOrWhiteSpace(value.Settings) ? null : protector.Unprotect(value.Settings)
        }).ToArray();
    }
}
