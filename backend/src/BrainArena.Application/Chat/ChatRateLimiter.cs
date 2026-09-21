using System.Collections.Concurrent;
using BrainArena.Application.Abstractions;

namespace BrainArena.Application.Chat;

public class ChatRateLimiter : IChatRateLimiter
{
    private static readonly TimeSpan Window = TimeSpan.FromSeconds(2);
    private readonly ConcurrentDictionary<Guid, DateTimeOffset> _lastSentAt = new();

    public bool TryConsume(Guid userId)
    {
        var now = DateTimeOffset.UtcNow;
        var allowed = false;

        _lastSentAt.AddOrUpdate(
            userId,
            _ =>
            {
                allowed = true;
                return now;
            },
            (_, last) =>
            {
                if (now - last < Window)
                {
                    return last;
                }
                allowed = true;
                return now;
            });

        return allowed;
    }
}
