using System.ComponentModel.DataAnnotations;

namespace IdentityService.Api;

public static class RateLimitPolicies
{
    public const string Auth = "auth";
}

public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    [Range(1, 100_000)]
    public int AuthPermitLimit { get; set; } = 10;

    [Range(typeof(TimeSpan), "00:00:01", "01:00:00")]
    public TimeSpan AuthWindow { get; set; } = TimeSpan.FromMinutes(1);
}
