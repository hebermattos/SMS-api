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
        Assert.Equal(15672, options.ManagementPort);
        Assert.Equal("sms.alert.evaluation", options.Queue);
        Assert.Equal("sms.send", options.SendQueue);
        Assert.Equal("sms.reporting.overview", options.ReportingQueue);
    }

    [Fact]
    public void From_ReadsAlertAndSendSettings()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["RabbitMq:Host"] = "broker",
            ["RabbitMq:Port"] = "5673",
            ["RabbitMq:ManagementPort"] = "15673",
            ["RabbitMq:User"] = "sms",
            ["RabbitMq:Password"] = "secret",
            ["RabbitMq:VirtualHost"] = "/sms",
            ["RabbitMq:Queue"] = "alerts.queue",
            ["RabbitMq:SendQueue"] = "send.queue",
            ["RabbitMq:ReportingQueue"] = "reporting.queue"
        }).Build();

        var options = RabbitMqAlertOptions.From(configuration);

        Assert.Equal("broker", options.Host);
        Assert.Equal(5673, options.Port);
        Assert.Equal(15673, options.ManagementPort);
        Assert.Equal("sms", options.User);
        Assert.Equal("secret", options.Password);
        Assert.Equal("/sms", options.VirtualHost);
        Assert.Equal("alerts.queue", options.Queue);
        Assert.Equal("send.queue", options.SendQueue);
        Assert.Equal("reporting.queue", options.ReportingQueue);
    }

    [Fact]
    public void From_InvalidPorts_FallBackToDefaults()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["RabbitMq:Port"] = "invalid",
            ["RabbitMq:ManagementPort"] = "invalid"
        }).Build();

        var options = RabbitMqAlertOptions.From(configuration);

        Assert.Equal(5672, options.Port);
        Assert.Equal(15672, options.ManagementPort);
    }
}
