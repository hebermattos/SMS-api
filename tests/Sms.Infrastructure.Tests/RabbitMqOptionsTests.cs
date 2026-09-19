using Microsoft.Extensions.Configuration;
using Sms.Infrastructure.Messaging;

namespace Sms.Infrastructure.Tests;

public sealed class RabbitMqOptionsTests
{
    [Fact]
    public void From_UsesDefaults()
    {
        var options = RabbitMqAlertOptions.From(new ConfigurationBuilder().Build());

        Assert.Equal("localhost", options.Host);
        Assert.Equal(5672, options.Port);
        Assert.Equal("sms.alert.evaluation", options.Queue);
        Assert.Equal("sms.send", options.SendQueue);
    }

    [Fact]
    public void From_ReadsAlertAndSendSettings()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["RabbitMq:Host"] = "broker",
            ["RabbitMq:Port"] = "5673",
            ["RabbitMq:User"] = "sms",
            ["RabbitMq:Password"] = "secret",
            ["RabbitMq:VirtualHost"] = "/sms",
            ["RabbitMq:Exchange"] = "alerts.exchange",
            ["RabbitMq:Queue"] = "alerts.queue",
            ["RabbitMq:RoutingKey"] = "alerts.route",
            ["RabbitMq:SendExchange"] = "send.exchange",
            ["RabbitMq:SendQueue"] = "send.queue",
            ["RabbitMq:SendRoutingKey"] = "send.route"
        }).Build();

        var options = RabbitMqAlertOptions.From(configuration);

        Assert.Equal("broker", options.Host);
        Assert.Equal(5673, options.Port);
        Assert.Equal("sms", options.User);
        Assert.Equal("secret", options.Password);
        Assert.Equal("/sms", options.VirtualHost);
        Assert.Equal("alerts.exchange", options.Exchange);
        Assert.Equal("alerts.queue", options.Queue);
        Assert.Equal("alerts.route", options.RoutingKey);
        Assert.Equal("send.exchange", options.SendExchange);
        Assert.Equal("send.queue", options.SendQueue);
        Assert.Equal("send.route", options.SendRoutingKey);
    }
}
