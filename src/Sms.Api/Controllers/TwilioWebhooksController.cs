using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sms.Application.Messages;
using Sms.Application.Providers;
using Sms.Domain.Messages;
using Sms.Infrastructure.Providers;

namespace Sms.Api.Controllers;

[AllowAnonymous]
[ApiController]
[Route("api/v1/webhooks/twilio")]
public sealed class TwilioWebhooksController(
    ITenantSmsProviderRepository providers,
    ISmsMessageRepository messages,
    TwilioWebhookValidator validator) : ControllerBase
{
    [HttpPost("inbound")]
    public async Task<IActionResult> Inbound(CancellationToken cancellationToken)
    {
        var form = await Request.ReadFormAsync(cancellationToken);
        var config = await ResolveAndValidateAsync(form, cancellationToken);
        if (config is null) return Forbid();

        var sid = form["MessageSid"].ToString();
        var from = form["From"].ToString();
        var to = form["To"].ToString();
        if (string.IsNullOrWhiteSpace(sid) || string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to)) return BadRequest();

        await messages.InsertInboundIfNotExistsAsync(new SmsMessage
        {
            Id = Guid.NewGuid(), TenantId = config.TenantId, From = from, To = to,
            Body = form["Body"].ToString(), Provider = "Twilio", ProviderMessageId = sid,
            Direction = SmsDirection.Inbound, Status = SmsStatus.Received, CreatedAt = DateTimeOffset.UtcNow
        }, cancellationToken);

        return Content("<Response></Response>", "application/xml");
    }

    [HttpPost("status")]
    public async Task<IActionResult> Status(CancellationToken cancellationToken)
    {
        var form = await Request.ReadFormAsync(cancellationToken);
        var config = await ResolveAndValidateAsync(form, cancellationToken);
        if (config is null) return Forbid();

        var sid = form["MessageSid"].ToString();
        if (string.IsNullOrWhiteSpace(sid)) return BadRequest();
        var status = MapStatus(form["MessageStatus"].ToString());
        await messages.UpdateStatusByProviderMessageIdAsync(config.TenantId, "Twilio", sid, status, DateTimeOffset.UtcNow, cancellationToken);
        return NoContent();
    }

    private async Task<TenantSmsProviderConfiguration?> ResolveAndValidateAsync(IFormCollection form, CancellationToken cancellationToken)
    {
        var accountSid = form["AccountSid"].ToString();
        if (string.IsNullOrWhiteSpace(accountSid)) return null;
        var config = await providers.GetByAccountAsync("Twilio", accountSid, cancellationToken);
        if (config is null) return null;

        var signature = Request.Headers["X-Twilio-Signature"].ToString();
        var url = $"{Request.Scheme}://{Request.Host}{Request.PathBase}{Request.Path}{Request.QueryString}";
        var parameters = form.SelectMany(x => x.Value.Select(value => new KeyValuePair<string, string>(x.Key, value ?? string.Empty)));
        return validator.Validate(url, parameters, signature, config.ApiSecret) ? config : null;
    }

    private static SmsStatus MapStatus(string status) => status.ToLowerInvariant() switch
    {
        "sent" => SmsStatus.Sent,
        "delivered" => SmsStatus.Delivered,
        "failed" or "undelivered" => SmsStatus.Failed,
        _ => SmsStatus.Queued
    };
}
