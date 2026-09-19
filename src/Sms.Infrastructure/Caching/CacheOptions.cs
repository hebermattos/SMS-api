using Microsoft.Extensions.Configuration;

namespace Sms.Infrastructure.Caching;

public sealed record CacheOptions(bool Enabled)
{
    public static CacheOptions From(IConfiguration configuration)
    {
        var configured = configuration["Cache:Enabled"];
        return new CacheOptions(!bool.TryParse(configured, out var enabled) || enabled);
    }
}
