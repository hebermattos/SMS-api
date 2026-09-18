using Sms.Application.Providers;

namespace Sms.Seed;

public static class ExampleProviders
{
    // Local/test fixtures only. These are not credentials issued by either provider.
    public static async Task SeedAsync(ITenantSmsProviderRepository providers, Guid tenantId, CancellationToken cancellationToken = default)
    {
        await providers.UpsertAsync(new(tenantId, "Twilio",
            "AC00000000000000000000000000000000", "fake-twilio-auth-token",
            "+15005550006", IsDefault: true, IsActive: true), cancellationToken);

        await providers.UpsertAsync(new(tenantId, "Mock",
            "mock-example", "fake-mock-secret", "+15550000000", IsDefault: false, IsActive: true), cancellationToken);

        await providers.UpsertAsync(new(tenantId, "Bandwidth",
            "fake-bandwidth-client-id", "fake-bandwidth-client-secret",
            "+12025550101", IsDefault: false, IsActive: true,
            Settings: """{"accountId":"0000000","applicationId":"00000000-0000-4000-8000-000000000000","webhookPassword":"fake-bandwidth-webhook-password"}"""), cancellationToken);
    }
}
