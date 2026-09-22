using BrainArena.Application.Common;
using BrainArena.Application.Matches;
using BrainArena.Domain.Entities;
using BrainArena.Domain.Enums;

namespace BrainArena.Application.Tests.Matches;

public class MultipleChoiceGameModeTests
{
    private readonly MultipleChoiceGameMode mode = new();

    private static MatchQuestion BuildMatchQuestion(int correctOptionIndex = 1) => new()
    {
        Id = Guid.NewGuid(),
        MatchId = Guid.NewGuid(),
        QuestionId = Guid.NewGuid(),
        OrderIndex = 0,
        Question = new Question
        {
            Id = Guid.NewGuid(),
            Topic = RoomTopic.Math,
            Type = QuestionType.MultipleChoice,
            Text = "2 + 2 = ?",
            Options = ["3", "4", "5", "6"],
            CorrectOptionIndex = correctOptionIndex,
            Explanation = "2 + 2 = 4"
        }
    };

    [Fact]
    public void EvaluateAnswer_CorrectWithFullTimeRemaining_AwardsMaxSpeedBonus()
    {
        var matchQuestion = BuildMatchQuestion(correctOptionIndex: 1);

        var result = mode.EvaluateAnswer(matchQuestion, new SubmittedAnswer(1, null), TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(20));

        Assert.True(result.IsCorrect);
        Assert.Equal(150, result.Points);
    }

    [Fact]
    public void EvaluateAnswer_WrongOption_ScoresZero()
    {
        var matchQuestion = BuildMatchQuestion(correctOptionIndex: 1);

        var result = mode.EvaluateAnswer(matchQuestion, new SubmittedAnswer(2, null), TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(20));

        Assert.False(result.IsCorrect);
        Assert.Equal(0, result.Points);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    [InlineData(null)]
    public void EvaluateAnswer_OptionIndexOutOfRangeOrMissing_ThrowsAppException(int? optionIndex)
    {
        var matchQuestion = BuildMatchQuestion();

        var exception = Assert.Throws<AppException>(
            () => mode.EvaluateAnswer(matchQuestion, new SubmittedAnswer(optionIndex, null), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(20)));

        Assert.Equal(400, exception.StatusCode);
    }

    [Fact]
    public void ToClientPayload_NeverIncludesTheCorrectAnswer()
    {
        var matchQuestion = BuildMatchQuestion(correctOptionIndex: 1);

        var payload = mode.ToClientPayload(matchQuestion, index: 0, totalQuestions: 5, DateTimeOffset.UtcNow);

        Assert.Equal(MultipleChoiceGameMode.Key, payload.Kind);
        Assert.Equal(4, payload.Options!.Count);
    }

    [Fact]
    public void ToRevealPayload_IncludesTheCorrectOptionButNoNumericAnswer()
    {
        var matchQuestion = BuildMatchQuestion(correctOptionIndex: 2);

        var payload = mode.ToRevealPayload(matchQuestion, index: 0, DateTimeOffset.UtcNow, []);

        Assert.Equal(MultipleChoiceGameMode.Key, payload.Kind);
        Assert.Equal(2, payload.CorrectOptionIndex);
        Assert.Null(payload.CorrectNumericAnswer);
    }
}
