namespace BrainArena.Application.Matches;

public record RankingCandidate(Guid UserId, int Score, int CorrectAnswers, TimeSpan CorrectAnswerTime, int JoinOrder);

/// <summary>
/// Strict standings order shared by the live scoreboard and the final ranking. Equal scores fall
/// back to: more correct answers, then less total time spent on correct answers, then room join
/// order (a tournament round room's join order is its bracket seeding). Never a shared rank —
/// tournament advancement takes the top N by position.
/// </summary>
public static class MatchRanking
{
    public static IReadOnlyList<RankingCandidate> Order(IEnumerable<RankingCandidate> candidates) =>
        candidates
            .OrderByDescending(c => c.Score)
            .ThenByDescending(c => c.CorrectAnswers)
            .ThenBy(c => c.CorrectAnswerTime)
            .ThenBy(c => c.JoinOrder)
            .ToList();
}
