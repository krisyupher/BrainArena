using BrainArena.Application.Matches;

namespace BrainArena.Application.Tests.Matches;

public class MatchRankingTests
{
    private static readonly Guid A = Guid.NewGuid();
    private static readonly Guid B = Guid.NewGuid();

    private static RankingCandidate Candidate(Guid id, int score, int correct = 0, int correctSeconds = 0, int joinOrder = 0) =>
        new(id, score, correct, TimeSpan.FromSeconds(correctSeconds), joinOrder);

    private static Guid[] Order(params RankingCandidate[] candidates) =>
        MatchRanking.Order(candidates).Select(c => c.UserId).ToArray();

    [Fact]
    public void HigherScoreRanksFirst()
    {
        Assert.Equal([B, A], Order(Candidate(A, 100, joinOrder: 0), Candidate(B, 250, joinOrder: 1)));
    }

    [Fact]
    public void EqualScore_MoreCorrectAnswersRanksFirst()
    {
        Assert.Equal([B, A], Order(Candidate(A, 300, correct: 2, joinOrder: 0), Candidate(B, 300, correct: 3, joinOrder: 1)));
    }

    [Fact]
    public void EqualScoreAndCorrectCount_FasterOnCorrectAnswersRanksFirst()
    {
        Assert.Equal([B, A], Order(
            Candidate(A, 300, correct: 2, correctSeconds: 14, joinOrder: 0),
            Candidate(B, 300, correct: 2, correctSeconds: 9, joinOrder: 1)));
    }

    [Fact]
    public void CompleteTie_EarlierJoinRanksFirst_RegardlessOfInputOrder()
    {
        var first = Candidate(A, 0, joinOrder: 0);
        var second = Candidate(B, 0, joinOrder: 1);

        Assert.Equal([A, B], Order(second, first));
        Assert.Equal([A, B], Order(first, second));
    }
}
