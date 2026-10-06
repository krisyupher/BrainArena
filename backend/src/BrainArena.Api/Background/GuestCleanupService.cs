using BrainArena.Application.Abstractions;
using BrainArena.Infrastructure.Auth;
using Microsoft.Extensions.Options;

namespace BrainArena.Api.Background;

public class GuestCleanupOptions
{
    public const string SectionName = "GuestCleanup";

    public bool Enabled { get; set; } = true;
    public int IntervalMinutes { get; set; } = 60;
}

/// <summary>
/// Purges guest accounts once nobody can ever use them again: a guest has no password, so after its
/// one JWT expires (Jwt:ExpiryDays, plus a day of slack) its data is unreachable.
/// </summary>
public class GuestCleanupService(
    IServiceScopeFactory scopeFactory,
    IOptions<GuestCleanupOptions> options,
    IOptions<JwtOptions> jwtOptions,
    ILogger<GuestCleanupService> logger) : BackgroundService
{
    private const int BatchSize = 500;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled)
        {
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(options.Value.IntervalMinutes));
        do
        {
            try
            {
                var cutoff = DateTimeOffset.UtcNow.AddDays(-(jwtOptions.Value.ExpiryDays + 1));
                using var scope = scopeFactory.CreateScope();
                var cleanup = scope.ServiceProvider.GetRequiredService<IGuestCleanup>();

                int deleted, total = 0;
                do
                {
                    deleted = await cleanup.DeleteGuestsCreatedBeforeAsync(cutoff, BatchSize, stoppingToken);
                    total += deleted;
                } while (deleted == BatchSize);

                if (total > 0)
                {
                    logger.LogInformation("Deleted {Count} expired guest accounts", total);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Guest cleanup failed");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
