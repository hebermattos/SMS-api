using Dapper;
using Sms.Application.Providers;
using Sms.Application.Security;

namespace Sms.Infrastructure.Persistence;

public sealed class TenantSmsProviderRepository(SqlConnectionFactory connectionFactory, ISecretProtector secretProtector) : ITenantSmsProviderRepository
{
    private const string Columns = "TenantId, Provider, AccountId, ApiSecret, FromNumber, IsDefault, IsActive, Settings";

    public async Task<TenantSmsProviderConfiguration?> GetAsync(Guid tenantId, string provider, CancellationToken cancellationToken = default)
    {
        var sql = $"SELECT {Columns} FROM dbo.TenantSmsProviders WHERE TenantId=@TenantId AND Provider=@Provider AND IsActive=1;";
        using var connection=connectionFactory.CreateConnection();
        return Decrypt(await connection.QuerySingleOrDefaultAsync<TenantSmsProviderConfiguration>(new CommandDefinition(sql,new {TenantId=tenantId,Provider=provider},cancellationToken:cancellationToken)));
    }

    public async Task<TenantSmsProviderConfiguration?> GetDefaultAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var sql=$"SELECT {Columns} FROM dbo.TenantSmsProviders WHERE TenantId=@TenantId AND IsDefault=1 AND IsActive=1;";
        using var connection=connectionFactory.CreateConnection();
        return Decrypt(await connection.QuerySingleOrDefaultAsync<TenantSmsProviderConfiguration>(new CommandDefinition(sql,new {TenantId=tenantId},cancellationToken:cancellationToken)));
    }

    public async Task<TenantSmsProviderConfiguration?> GetByAccountAsync(string provider,string accountId,CancellationToken cancellationToken=default)
    {
        var sql=$"SELECT {Columns} FROM dbo.TenantSmsProviders WHERE Provider=@Provider AND AccountId=@AccountId AND IsActive=1;";
        using var connection=connectionFactory.CreateConnection();
        return Decrypt(await connection.QuerySingleOrDefaultAsync<TenantSmsProviderConfiguration>(new CommandDefinition(sql,new {Provider=provider,AccountId=accountId},cancellationToken:cancellationToken)));
    }

    public async Task UpsertAsync(TenantSmsProviderConfiguration configuration,CancellationToken cancellationToken=default)
    {
        const string sql="""UPDATE dbo.TenantSmsProviders SET AccountId=@AccountId,ApiSecret=@ApiSecret,FromNumber=@FromNumber,IsDefault=@IsDefault,IsActive=@IsActive,Settings=@Settings,UpdatedAt=@Now WHERE TenantId=@TenantId AND Provider=@Provider;
IF @@ROWCOUNT=0 INSERT dbo.TenantSmsProviders(Id,TenantId,Provider,AccountId,ApiSecret,FromNumber,IsDefault,IsActive,Settings,CreatedAt) VALUES(@Id,@TenantId,@Provider,@AccountId,@ApiSecret,@FromNumber,@IsDefault,@IsActive,@Settings,@Now);""";
        using var connection=connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(sql,new {Id=Guid.NewGuid(),configuration.TenantId,configuration.Provider,configuration.AccountId,ApiSecret=secretProtector.Protect(configuration.ApiSecret),configuration.FromNumber,configuration.IsDefault,configuration.IsActive,Settings=ProtectOptional(configuration.Settings),Now=DateTimeOffset.UtcNow},cancellationToken:cancellationToken));
    }

    private string? ProtectOptional(string? value)=>string.IsNullOrWhiteSpace(value)?null:secretProtector.Protect(value);
    private string? UnprotectOptional(string? value)=>string.IsNullOrWhiteSpace(value)?null:secretProtector.Unprotect(value);
    private TenantSmsProviderConfiguration? Decrypt(TenantSmsProviderConfiguration? value)=>value is null?null:value with { ApiSecret=secretProtector.Unprotect(value.ApiSecret), Settings=UnprotectOptional(value.Settings) };
}
