using Dapper;
using Microsoft.Data.SqlClient;
using Sms.Application.Administration;
using Sms.Application.Providers;
using Sms.Application.Security;

namespace Sms.Infrastructure.Persistence;

public sealed class TenantSmsProviderRepository(SqlConnectionFactory connectionFactory, ISecretProtector secretProtector) : ITenantSmsProviderRepository
{
    public async Task<TenantSmsProviderConfiguration?> GetAsync(Guid tenantId, string provider, CancellationToken cancellationToken = default)
    {
        var sql = Sms.Infrastructure.Sql.SqlQuery.Load("Persistence/TenantSmsProviderRepository.GetAsync.02.sql");
        using var connection=connectionFactory.CreateConnection();
        return Decrypt(await connection.QuerySingleOrDefaultAsync<TenantSmsProviderConfiguration>(new CommandDefinition(sql,new {TenantId=tenantId,Provider=provider},cancellationToken:cancellationToken)));
    }

    public async Task<TenantSmsProviderConfiguration?> GetDefaultAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var sql=Sms.Infrastructure.Sql.SqlQuery.Load("Persistence/TenantSmsProviderRepository.GetDefaultAsync.03.sql");
        using var connection=connectionFactory.CreateConnection();
        return Decrypt(await connection.QuerySingleOrDefaultAsync<TenantSmsProviderConfiguration>(new CommandDefinition(sql,new {TenantId=tenantId},cancellationToken:cancellationToken)));
    }

    public async Task<TenantSmsProviderConfiguration?> GetByAccountAndNumberAsync(string provider, string accountId, string number, CancellationToken cancellationToken = default)
    {
        var sql = Sms.Infrastructure.Sql.SqlQuery.Load("Persistence/TenantSmsProviderRepository.GetByAccountAndNumberAsync.04.sql");
        using var connection = connectionFactory.CreateConnection();
        return Decrypt(await connection.QuerySingleOrDefaultAsync<TenantSmsProviderConfiguration>(new CommandDefinition(
            sql,
            new { Provider = provider, AccountId = accountId, FromNumber = number.Trim() },
            cancellationToken: cancellationToken)));
    }

    public async Task UpsertAsync(TenantSmsProviderConfiguration configuration,CancellationToken cancellationToken=default)
    {
        var sql = Sms.Infrastructure.Sql.SqlQuery.Load("Persistence/TenantSmsProviderRepository.UpsertAsync.01.sql");
        using var connection=connectionFactory.CreateConnection();
        try
        {
            await connection.ExecuteAsync(new CommandDefinition(sql,new {Id=Guid.NewGuid(),configuration.TenantId,configuration.Provider,configuration.AccountId,ApiSecret=secretProtector.Protect(configuration.ApiSecret),FromNumber=configuration.FromNumber?.Trim(),configuration.IsDefault,configuration.IsActive,Settings=ProtectOptional(configuration.Settings),Now=DateTimeOffset.UtcNow},cancellationToken:cancellationToken));
        }
        catch (SqlException exception) when (exception.Number is 2601 or 2627) { throw new AdministrationConflictException(); }
    }

    private string? ProtectOptional(string? value)=>string.IsNullOrWhiteSpace(value)?null:secretProtector.Protect(value);
    private string? UnprotectOptional(string? value)=>string.IsNullOrWhiteSpace(value)?null:secretProtector.Unprotect(value);
    private TenantSmsProviderConfiguration? Decrypt(TenantSmsProviderConfiguration? value)=>value is null?null:value with { ApiSecret=secretProtector.Unprotect(value.ApiSecret), Settings=UnprotectOptional(value.Settings) };
}
