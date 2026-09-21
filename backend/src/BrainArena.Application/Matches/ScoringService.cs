namespace BrainArena.Application.Matches;

/// <summary>
/// Pure scoring math, kept separate from orchestration so it can be unit tested without a
/// running match. Correct answer = 100 points + a speed bonus of up to 50, scaled by how much
/// time was left when the player answered. Wrong or missing answers score 0.
/// </summary>
public static class ScoringService
{
    public const int BasePoints = 100;
    public const int MaxSpeedBonus = 50;

    public static int ComputePoints(bool isCorrect, TimeSpan timeRemaining, TimeSpan timeLimit)
    {
        if (!isCorrect)
        {
            return 0;
        }

        if (timeLimit <= TimeSpan.Zero)
        {
            return BasePoints;
        }

        var ratio = Math.Clamp(timeRemaining.TotalSeconds / timeLimit.TotalSeconds, 0, 1);
        var bonus = (int)Math.Round(MaxSpeedBonus * ratio);
        return BasePoints + bonus;
    }
}
