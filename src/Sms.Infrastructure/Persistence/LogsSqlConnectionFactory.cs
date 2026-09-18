using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace Sms.Infrastructure.Persistence;

public sealed class LogsSqlConnectionFactory
{
    private readonly string _connectionString;

    public LogsSqlConnectionFactory(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("LogsSqlServer")
            ?? throw new InvalidOperationException("Connection string 'LogsSqlServer' is not configured.");
    }

    public SqlConnection CreateConnection() => new(_connectionString);
}
