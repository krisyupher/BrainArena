using System.Threading.RateLimiting;
using BrainArena.Api.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace BrainArena.Api.RateLimiting;

public static class RateLimitPolicies
{
    public const string Auth = "auth";
    public const string RoomCreation = "room-creation";

    public static IServiceCollection AddBrainArenaRateLimiting(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<RateLimitingOptions>(config.GetSection(RateLimitingOptions.SectionName));

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // Same problem+json shape as ExceptionHandlingMiddleware, so the frontend's `error.title` handling just works.
            options.OnRejected = (context, ct) =>
            {
                context.HttpContext.Response.ContentType = "application/problem+json";
                return new ValueTask(context.HttpContext.Response.WriteAsJsonAsync(
                    new ProblemDetails
                    {
                        Status = StatusCodes.Status429TooManyRequests,
                        Title = "Too many requests. Please wait a minute and try again."
                    },
                    ct));
            };

            options.AddPolicy(Auth, http => Bucket($"ip:{ClientIp(http)}", Settings(http).Auth));

            options.AddPolicy(RoomCreation, http =>
            {
                var userId = http.User.GetUserIdOrNull();
                return userId is null
                    ? Bucket($"guest-ip:{ClientIp(http)}", Settings(http).GuestCreation)
                    : Bucket($"user:{userId}", Settings(http).RoomCreation);
            });
        });

        return services;
    }

    private static RateLimitingOptions Settings(HttpContext http) =>
        http.RequestServices.GetRequiredService<IOptions<RateLimitingOptions>>().Value;

    private static string ClientIp(HttpContext http) =>
        http.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    private static RateLimitPartition<string> Bucket(string key, TokenBucket settings) =>
        RateLimitPartition.GetTokenBucketLimiter(key, _ => new TokenBucketRateLimiterOptions
        {
            TokenLimit = settings.TokenLimit,
            TokensPerPeriod = settings.TokensPerMinute,
            ReplenishmentPeriod = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        });
}
