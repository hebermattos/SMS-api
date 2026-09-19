namespace Sms.Infrastructure.Messaging;

public sealed class RabbitMqAlertOptions
{
    public string Host { get; init; } = "localhost";
    public int Port { get; init; } = 5672;
    public string User { get; init; } = "guest";
    public string Password { get; init; } = "guest";
    public string VirtualHost { get; init; } = "/";
    public string Exchange { get; init; } = "sms.alerts";
    public string Queue { get; init; } = "sms.alert.evaluation";
    public string RoutingKey { get; init; } = "alert.evaluate";

    public static RabbitMqAlertOptions From(IConfiguration configuration)
    {
        var section = configuration.GetSection("RabbitMq");
        return new RabbitMqAlertOptions
        {
            Host = section["Host"] ?? "localhost",
            Port = int.TryParse(section["Port"], out var port) ? port : 5672,
            User = section["User"] ?? "guest",
            Password = section["Password"] ?? "guest",
            VirtualHost = section["VirtualHost"] ?? "/",
            Exchange = section["Exchange"] ?? "sms.alerts",
            Queue = section["Queue"] ?? "sms.alert.evaluation",
            RoutingKey = section["RoutingKey"] ?? "alert.evaluate"
        };
    }
}
