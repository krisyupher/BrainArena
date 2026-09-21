using BrainArena.Application.Matches;

namespace BrainArena.Application.Tests.Matches;

public class ScoringServiceTests
{
    [Fact]
    public void ComputePoints_CorrectWithFullTimeRemaining_AwardsMaxSpeedBonus()
    {
        var points = ScoringService.ComputePoints(isCorrect: true, TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(20));

        Assert.Equal(150, points);
    }

    [Fact]
    public void ComputePoints_CorrectWithNoTimeRemaining_AwardsBaseOnly()
    {
        var points = ScoringService.ComputePoints(isCorrect: true, TimeSpan.Zero, TimeSpan.FromSeconds(20));

        Assert.Equal(100, points);
    }

    [Fact]
    public void ComputePoints_CorrectWithHalfTimeRemaining_AwardsHalfSpeedBonus()
    {
        var points = ScoringService.ComputePoints(isCorrect: true, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(20));

        Assert.Equal(125, points);
    }

    [Theory]
    [InlineData(20)]
    [InlineData(0)]
    public void ComputePoints_WrongAnswer_AlwaysZero(double remainingSeconds)
    {
        var points = ScoringService.ComputePoints(isCorrect: false, TimeSpan.FromSeconds(remainingSeconds), TimeSpan.FromSeconds(20));

        Assert.Equal(0, points);
    }

    [Fact]
    public void ComputePoints_ZeroTimeLimit_DoesNotDivideByZero()
    {
        var points = ScoringService.ComputePoints(isCorrect: true, TimeSpan.Zero, TimeSpan.Zero);

        Assert.Equal(100, points);
    }
}
