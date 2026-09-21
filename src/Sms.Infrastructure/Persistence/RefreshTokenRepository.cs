using Dapper;
using Sms.Application.Auth;

namespace Sms.Infrastructure.Persistence;

public sealed class RefreshTokenRepository(SqlConnectionFactory connections) : IRefreshTokenRepository
{
    public async Task CreateAsync(RefreshTokenSession session, byte[] tokenHash, CancellationToken cancellationToken = default)
    {
        var sql = Sms.Infrastructure.Sql.SqlQuery.Load("Persistence/RefreshTokenRepository.CreateAsync.01.sql");
        using var connection = connections.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            session.Id, session.UserId, session.Username, session.TenantId, session.Context, session.Role,
            TokenHash = tokenHash, session.ExpiresAt
        }, cancellationToken: cancellationToken));
    }

    public async Task<RefreshTokenSession?> RotateAsync(
        byte[] currentTokenHash, byte[] replacementTokenHash, Guid replacementId,
        DateTimeOffset replacementExpiresAt, CancellationToken cancellationToken = default)
    {
        using var connection = connections.CreateNpgsqlConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var selectSql = Sms.Infrastructure.Sql.SqlQuery.Load("Persistence/RefreshTokenRepository.RotateAsync.01.sql");
        var session = await connection.QuerySingleOrDefaultAsync<RefreshTokenSession>(
            new CommandDefinition(selectSql, new { TokenHash = currentTokenHash }, transaction, cancellationToken: cancellationToken));
        if (session is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }

        var revokeSql = Sms.Infrastructure.Sql.SqlQuery.Load("Persistence/RefreshTokenRepository.RotateAsync.02.sql");
        await connection.ExecuteAsync(new CommandDefinition(revokeSql,
            new { session.Id, ReplacementHash = replacementTokenHash }, transaction, cancellationToken: cancellationToken));

        var insertSql = Sms.Infrastructure.Sql.SqlQuery.Load("Persistence/RefreshTokenRepository.RotateAsync.03.sql");
        await connection.ExecuteAsync(new CommandDefinition(insertSql, new
        {
            Id = replacementId, session.UserId, session.Username, session.TenantId, session.Context, session.Role,
            TokenHash = replacementTokenHash, ExpiresAt = replacementExpiresAt
        }, transaction, cancellationToken: cancellationToken));

        await transaction.CommitAsync(cancellationToken);
        return session;
    }
}
