using Dapper;
using Sms.Application.Auth;

namespace Sms.Infrastructure.Persistence;

public sealed class RefreshTokenRepository(SqlConnectionFactory connections) : IRefreshTokenRepository
{
    public async Task CreateAsync(RefreshTokenSession session, byte[] tokenHash, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO RefreshTokens
                (Id, UserId, Username, TenantId, Context, Role, TokenHash, ExpiresAt, CreatedAt)
            VALUES
                (@Id, @UserId, @Username, @TenantId, @Context, @Role, @TokenHash, @ExpiresAt, CURRENT_TIMESTAMP);
            """;
        using var connection = connections.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            session.Id, session.UserId, session.Username, session.TenantId,
            session.Context, session.Role, TokenHash = tokenHash, session.ExpiresAt
        }, cancellationToken: cancellationToken));
    }

    public async Task<RefreshTokenSession?> RotateAsync(
        byte[] currentTokenHash,
        byte[] replacementTokenHash,
        Guid replacementId,
        DateTimeOffset replacementExpiresAt,
        CancellationToken cancellationToken = default)
    {
        using var connection = connections.CreateNpgsqlConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        const string selectSql = """
            SELECT Id, UserId, Username, TenantId, Context, Role, ExpiresAt
            FROM RefreshTokens
            WHERE TokenHash = @TokenHash
              AND RevokedAt IS NULL
              AND ExpiresAt > CURRENT_TIMESTAMP
            FOR UPDATE;
            """;
        var session = await connection.QuerySingleOrDefaultAsync<RefreshTokenSession>(
            new CommandDefinition(selectSql, new { TokenHash = currentTokenHash }, transaction, cancellationToken: cancellationToken));
        if (session is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }

        const string revokeSql = """
            UPDATE RefreshTokens
            SET RevokedAt = CURRENT_TIMESTAMP, ReplacedByHash = @ReplacementHash
            WHERE Id = @Id AND RevokedAt IS NULL;
            """;
        await connection.ExecuteAsync(new CommandDefinition(revokeSql,
            new { session.Id, ReplacementHash = replacementTokenHash }, transaction, cancellationToken: cancellationToken));

        const string insertSql = """
            INSERT INTO RefreshTokens
                (Id, UserId, Username, TenantId, Context, Role, TokenHash, ExpiresAt, CreatedAt)
            VALUES
                (@Id, @UserId, @Username, @TenantId, @Context, @Role, @TokenHash, @ExpiresAt, CURRENT_TIMESTAMP);
            """;
        await connection.ExecuteAsync(new CommandDefinition(insertSql, new
        {
            Id = replacementId, session.UserId, session.Username, session.TenantId,
            session.Context, session.Role, TokenHash = replacementTokenHash, ExpiresAt = replacementExpiresAt
        }, transaction, cancellationToken: cancellationToken));

        await transaction.CommitAsync(cancellationToken);
        return session;
    }
}
