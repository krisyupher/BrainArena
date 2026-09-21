using BrainArena.Application.Chat;

namespace BrainArena.Application.Tests.Chat;

public class ChatRateLimiterTests
{
    [Fact]
    public void TryConsume_AllowsTheFirstMessageFromAUser()
    {
        var limiter = new ChatRateLimiter();

        Assert.True(limiter.TryConsume(Guid.NewGuid()));
    }

    [Fact]
    public void TryConsume_BlocksAnImmediateSecondMessageFromTheSameUser()
    {
        var limiter = new ChatRateLimiter();
        var userId = Guid.NewGuid();

        limiter.TryConsume(userId);
        var secondAttempt = limiter.TryConsume(userId);

        Assert.False(secondAttempt);
    }

    [Fact]
    public void TryConsume_TracksEachUserIndependently()
    {
        var limiter = new ChatRateLimiter();
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();

        limiter.TryConsume(userA);
        var userBFirstAttempt = limiter.TryConsume(userB);

        Assert.True(userBFirstAttempt);
    }
}
