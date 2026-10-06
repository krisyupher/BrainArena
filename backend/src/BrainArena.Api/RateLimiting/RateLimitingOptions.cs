namespace BrainArena.Api.RateLimiting;

/// <summary>Token buckets: <see cref="TokenBucket.TokenLimit"/> is the burst size, refilled at TokensPerMinute.</summary>
public class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    /// <summary>Login + register, per client IP.</summary>
    public TokenBucket Auth { get; set; } = new() { TokenLimit = 20, TokensPerMinute = 6 };

    /// <summary>Anonymous room creation (each one mints a guest account), per client IP. Burst sized for a classroom behind one IP.</summary>
    public TokenBucket GuestCreation { get; set; } = new() { TokenLimit = 30, TokensPerMinute = 2 };

    /// <summary>Room creation by a signed-in user, per user.</summary>
    public TokenBucket RoomCreation { get; set; } = new() { TokenLimit = 20, TokensPerMinute = 10 };
}

public class TokenBucket
{
    public int TokenLimit { get; set; }
    public int TokensPerMinute { get; set; }
}
