using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Sms.Api.Workers;
using Sms.Application.Alerts;

namespace Sms.Infrastructure.Tests;

public sealed class AlertEvaluationWorkerTests
{
    [Fact]
    public async Task WorkerEvaluatesRulesImmediately()
    {
        var repository = new Repository();
        var services = new ServiceCollection().AddSingleton<IAlertRepository>(repository).BuildServiceProvider();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["Alerts:EvaluationSeconds"] = "10" }).Build();
        var worker = new AlertEvaluationWorker(
            services.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<AlertEvaluationWorker>.Instance, configuration);

        await worker.StartAsync(default);
        for (var attempt = 0; attempt < 20 && repository.Evaluations == 0; attempt++)
            await Task.Delay(10);
        await worker.StopAsync(default);

        Assert.Equal(1, repository.Evaluations);
    }

    private sealed class Repository : IAlertRepository
    {
        public int Evaluations { get; private set; }
        public Task EvaluateAsync(CancellationToken cancellationToken = default) { Evaluations++; return Task.CompletedTask; }
        public Task<IReadOnlyList<AlertRule>> ListRulesAsync(Guid tenantId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task CreateRuleAsync(AlertRule rule, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> UpdateRuleAsync(AlertRule rule, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> DeleteRuleAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<AlertNotification>> ListAlertsAsync(Guid tenantId, bool unreadOnly, int skip, int take, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> MarkReadAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task MarkAllReadAsync(Guid tenantId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
