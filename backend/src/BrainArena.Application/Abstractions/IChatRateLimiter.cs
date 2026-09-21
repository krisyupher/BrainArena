namespace BrainArena.Application.Abstractions;

/// <summary>
/// Enforces the 1-message-per-2-seconds rate limit server-side (not just disabled client-side).
/// The concrete implementation must be a singleton to track state across requests/connections.
/// </summary>
public interface IChatRateLimiter
{
    /// <summary>Returns true and records the send if the user is allowed to send now; false if they must wait.</summary>
    bool TryConsume(Guid userId);
}
