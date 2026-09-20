using Sms.Application.Auth;
using Sms.Application.Common;
using Sms.Application.OptOut;
using Sms.Domain.Messages;

namespace Sms.Application.Messages;

public sealed class SendSmsService(
    ITenantContext tenantContext,
    ISmsMessageRepository repository,
    ISmsProviderResolver providerResolver,
    ISmsSendEventPublisher eventPublisher,
    OptOutService optOut,
    ITenantTimeZoneProvider timeZones,
    IPortalUserRepository users,
    TimeProvider clock)
{
    public async Task<SendSmsResult> SendAsync(SendSmsRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.To)) throw new ArgumentException("Destination phone number is required.");
        if (string.IsNullOrWhiteSpace(request.Body)) throw new ArgumentException("Message body is required.");

        if (request.UserId.HasValue)
        {
            var user = await users.GetActiveByIdAsync(request.UserId.Value, cancellationToken);
            if (user is null
                || user.Context != "tenant"
                || user.TenantId != tenantContext.TenantId)
                throw new ArgumentException("UserId must identify an active user in the authenticated tenant.");
        }

        await optOut.EnsureCanSendAsync(tenantContext.TenantId, request.To, cancellationToken);

        var provider = providerResolver.Resolve(request.Provider);
        var now = clock.GetUtcNow();
        var scheduledAtUtc = await GetScheduledAtUtcAsync(request.ScheduledAt, now, cancellationToken);
        var status = scheduledAtUtc.HasValue ? SmsStatus.Scheduled : SmsStatus.Queued;
        var message = new SmsMessage
        {
            Id = Guid.NewGuid(),
            TenantId = tenantContext.TenantId,
            UserId = request.UserId,
            From = request.From ?? string.Empty,
            To = request.To.Trim(),
            Body = request.Body,
            Provider = provider.Name,
            Direction = SmsDirection.Outbound,
            Status = status,
            CreatedAt = now,
            ScheduledAtUtc = scheduledAtUtc
        };

        await repository.InsertAsync(message, cancellationToken);
        if (!scheduledAtUtc.HasValue)
            await eventPublisher.PublishAsync(message.TenantId, message.Id, cancellationToken);
        return new SendSmsResult(message.Id, provider.Name, null, status.ToString(), scheduledAtUtc);
    }

    private async Task<DateTimeOffset?> GetScheduledAtUtcAsync(DateTime? scheduledAt, DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (!scheduledAt.HasValue) return null;
        if (scheduledAt.Value.Kind != DateTimeKind.Unspecified)
            throw new ArgumentException("ScheduledAt must be a local date and time without a UTC offset.");

        var zone = await timeZones.GetAsync(tenantContext.TenantId, cancellationToken);
        var local = scheduledAt.Value;
        if (zone.IsInvalidTime(local))
            throw new ArgumentException("ScheduledAt does not exist in the tenant time zone due to a daylight-saving transition.");
        if (zone.IsAmbiguousTime(local))
            throw new ArgumentException("ScheduledAt is ambiguous in the tenant time zone due to a daylight-saving transition.");

        var utc = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, zone));
        if (utc <= now) throw new ArgumentException("ScheduledAt must be in the future in the tenant time zone.");
        if (utc > now.AddYears(1)) throw new ArgumentException("ScheduledAt cannot be more than one year in the future.");
        return utc;
    }
}
