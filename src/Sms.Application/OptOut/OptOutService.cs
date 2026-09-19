namespace Sms.Application.OptOut;

public sealed class OptOutService(IOptOutRepository repository)
{
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
        { "STOP", "UNSUBSCRIBE", "CANCEL" };
    private static readonly HashSet<string> StartWords = new(StringComparer.OrdinalIgnoreCase)
        { "START" };

    public Task<IReadOnlyList<BlockedNumber>> ListAsync(Guid tenantId, int skip, int take, CancellationToken cancellationToken = default)
    {
        if (skip < 0) throw new ArgumentException("skip must be zero or greater.");
        return repository.ListAsync(tenantId, skip, Math.Clamp(take, 1, 200), cancellationToken);
    }

    public Task AddAsync(Guid tenantId, string phoneNumber, string? reason, CancellationToken cancellationToken = default) =>
        repository.AddOrUpdateAsync(tenantId, PhoneNumberNormalizer.Normalize(phoneNumber), "Manual", CleanReason(reason), DateTimeOffset.UtcNow, cancellationToken);

    public async Task ImportAsync(Guid tenantId, IEnumerable<AddBlockedNumber> rows, CancellationToken cancellationToken = default)
    {
        var values = rows.Take(1001).ToArray();
        if (values.Length > 1000) throw new ArgumentException("A maximum of 1,000 numbers can be imported at once.");
        foreach (var row in values)
            await repository.AddOrUpdateAsync(tenantId, PhoneNumberNormalizer.Normalize(row.PhoneNumber), "Import", CleanReason(row.Reason), DateTimeOffset.UtcNow, cancellationToken);
    }

    public Task<bool> RemoveAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default) =>
        repository.RemoveAsync(tenantId, id, cancellationToken);

    public async Task EnsureCanSendAsync(Guid tenantId, string phoneNumber, CancellationToken cancellationToken = default)
    {
        var normalized = PhoneNumberNormalizer.Normalize(phoneNumber);
        if (await repository.IsBlockedAsync(tenantId, normalized, cancellationToken)) throw new BlockedRecipientException();
    }

    public async Task ProcessInboundAsync(Guid tenantId, string phoneNumber, string body, DateTimeOffset occurredAt, CancellationToken cancellationToken = default)
    {
        var command = body.Trim();
        if (StopWords.Contains(command))
            await repository.AddOrUpdateAsync(tenantId, PhoneNumberNormalizer.Normalize(phoneNumber), "InboundKeyword", command.ToUpperInvariant(), occurredAt, cancellationToken);
        else if (StartWords.Contains(command))
            await repository.RemoveByPhoneAsync(tenantId, PhoneNumberNormalizer.Normalize(phoneNumber), cancellationToken);
    }

    private static string? CleanReason(string? reason)
    {
        reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        if (reason?.Length > 200) throw new ArgumentException("Reason must be 200 characters or fewer.");
        return reason;
    }
}
