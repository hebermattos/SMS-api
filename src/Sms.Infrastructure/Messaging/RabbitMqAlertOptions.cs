using Microsoft.Extensions.Configuration;

namespace Sms.Infrastructure.Messaging;

public sealed class RabbitMqAlertOptions
{
    public string Host { get; init; } = "localhost";
    public int Port { get; init; } = 5672;
    public int ManagementPort { get; init; } = 15672;
    public string User { get; init; } = "guest";
    public string Password { get; init; } = "guest";
    public string VirtualHost { get; init; } = "/";
    public string Queue { get; init; } = "sms.alert.evaluation";
    public string RuleEvaluationQueue { get; init; } = "sms.alert.rule-evaluation";
    public string SendQueue { get; init; } = "sms.send";
    public string ReportingQueue { get; init; } = "sms.reporting.overview";

    public static RabbitMqAlertOptions From(IConfiguration configuration)
    {
        var section = configuration.GetSection("RabbitMq");
        return new RabbitMqAlertOptions
        {
            Host = section["Host"] ?? "localhost",
            Port = int.TryParse(section["Port"], out var port) ? port : 5672,
            ManagementPort = int.TryParse(section["ManagementPort"], out var managementPort) ? managementPort : 15672,
            User = section["User"] ?? "guest",
            Password = section["Password"] ?? "guest",
            VirtualHost = section["VirtualHost"] ?? "/",
            Queue = section["Queue"] ?? "sms.alert.evaluation",
            RuleEvaluationQueue = section["RuleEvaluationQueue"] ?? "sms.alert.rule-evaluation",
            SendQueue = section["SendQueue"] ?? "sms.send",
            ReportingQueue = section["ReportingQueue"] ?? "sms.reporting.overview"
        };
    }
}
