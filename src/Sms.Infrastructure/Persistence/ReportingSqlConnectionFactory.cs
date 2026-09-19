using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace Sms.Infrastructure.Persistence;

public sealed class ReportingSqlConnectionFactory
{
    private readonly string _connectionString;

    public ReportingSqlConnectionFactory(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("ReportingSqlServer")
            ?? throw new InvalidOperationException("Connection string 'ReportingSqlServer' is not configured.");
    }

    public SqlConnection CreateConnection() => new(_connectionString);
}
