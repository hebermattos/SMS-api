using Dapper;
using Npgsql;

namespace Sms.Api.Middleware;

public interface IUserActivityWriter
{
    Task WriteAsync(UserActivity activity, CancellationToken cancellationToken = default);
}

public sealed record UserActivity(
    Guid TenantId,
    string? UserId,
    string ActivityType,
    string Action,
    string? ResourceType,
    string? ResourceId,
    string Description,
    string Outcome);

public sealed class PostgresUserActivityWriter(string connectionString, TimeProvider timeProvider) : IUserActivityWriter
{
    private const string Sql = """
        INSERT INTO UserActivityLogs
            ("Timestamp", TenantId, UserId, ActivityType, Action, ResourceType, ResourceId, Description, Outcome)
        VALUES
            (@Timestamp, @TenantId, @UserId, @ActivityType, @Action, @ResourceType, @ResourceId, @Description, @Outcome);
        """;

    public async Task WriteAsync(UserActivity activity, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.ExecuteAsync(new CommandDefinition(Sql, new
        {
            Timestamp = timeProvider.GetUtcNow(),
            activity.TenantId,
            activity.UserId,
            activity.ActivityType,
            activity.Action,
            activity.ResourceType,
            activity.ResourceId,
            activity.Description,
            activity.Outcome
        }, cancellationToken: cancellationToken));
    }
}
