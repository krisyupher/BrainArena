using BrainArena.Application.Common;
using BrainArena.Application.Matches;
using BrainArena.Domain.Entities;
using BrainArena.Domain.Enums;

namespace BrainArena.Application.Tests.Matches;

public class CalculationGameModeTests
{
    private readonly CalculationGameMode mode = new();

    private static MatchQuestion BuildMatchQuestion(decimal correctAnswer = 42m) => new()
    {
        Id = Guid.NewGuid(),
        MatchId = Guid.NewGuid(),
        QuestionId = Guid.NewGuid(),
        OrderIndex = 0,
        Question = new Question
        {
            Id = Guid.NewGuid(),
            Topic = RoomTopic.Math,
            Type = QuestionType.Calculation,
            Text = "20 + 22 = ?",
            Options = null,
            CorrectOptionIndex = null,
            CorrectNumericAnswer = correctAnswer,
            Explanation = "20 + 22 = 42"
        }
    };

    [Fact]
    public void EvaluateAnswer_CorrectWithFullTimeRemaining_MatchesTheSameCurveAsMultipleChoice()
    {
        var matchQuestion = BuildMatchQuestion(42m);

        var result = mode.EvaluateAnswer(matchQuestion, new SubmittedAnswer(null, 42m), TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(20));

        Assert.True(result.IsCorrect);
        Assert.Equal(150, result.Points);
    }

    [Fact]
    public void EvaluateAnswer_CorrectWithNoTimeRemaining_AwardsBaseOnly()
    {
        var matchQuestion = BuildMatchQuestion(42m);

        var result = mode.EvaluateAnswer(matchQuestion, new SubmittedAnswer(null, 42m), TimeSpan.Zero, TimeSpan.FromSeconds(20));

        Assert.True(result.IsCorrect);
        Assert.Equal(100, result.Points);
    }

    [Fact]
    public void EvaluateAnswer_WrongValue_ScoresZero()
    {
        var matchQuestion = BuildMatchQuestion(42m);

        var result = mode.EvaluateAnswer(matchQuestion, new SubmittedAnswer(null, 41m), TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(20));

        Assert.False(result.IsCorrect);
        Assert.Equal(0, result.Points);
    }

    [Fact]
    public void EvaluateAnswer_MissingNumericValue_ThrowsAppException()
    {
        var matchQuestion = BuildMatchQuestion();

        var exception = Assert.Throws<AppException>(
            () => mode.EvaluateAnswer(matchQuestion, new SubmittedAnswer(null, null), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(20)));

        Assert.Equal(400, exception.StatusCode);
    }

    [Fact]
    public void ToClientPayload_NeverIncludesTheCorrectAnswer()
    {
        var matchQuestion = BuildMatchQuestion(42m);

        var payload = mode.ToClientPayload(matchQuestion, index: 0, totalQuestions: 5, DateTimeOffset.UtcNow);

        Assert.Equal(CalculationGameMode.Key, payload.Kind);
        Assert.Null(payload.Options);
        // QuestionClientPayload has no numeric-answer field at all — structurally impossible to leak it here.
    }

    [Fact]
    public void ToRevealPayload_IncludesTheCorrectNumericAnswerButNoOptionIndex()
    {
        var matchQuestion = BuildMatchQuestion(42m);

        var payload = mode.ToRevealPayload(matchQuestion, index: 0, DateTimeOffset.UtcNow, []);

        Assert.Equal(CalculationGameMode.Key, payload.Kind);
        Assert.Equal(42m, payload.CorrectNumericAnswer);
        Assert.Null(payload.CorrectOptionIndex);
    }

    [Fact]
    public async Task PrepareQuestionsAsync_GeneratesExactlyTheRequestedCount_AllAsCalculationType()
    {
        var room = new Room { Id = Guid.NewGuid(), Name = "Calc Room", Topic = RoomTopic.Math, QuestionCount = 7 };

        var questions = await mode.PrepareQuestionsAsync(room, questionRepo: null!, CancellationToken.None);

        Assert.Equal(7, questions.Count);
        Assert.All(questions, q => Assert.Equal(QuestionType.Calculation, q.Type));
        Assert.All(questions, q => Assert.NotNull(q.CorrectNumericAnswer));
        Assert.All(questions, q => Assert.Null(q.Options));
    }

    [Fact]
    public async Task PrepareQuestionsAsync_ExplanationIsTheResolvedEquation_NotTheQuestionTextWithTheAnswerAppended()
    {
        // Regression test: Explanation used to be built as $"{Text} {answer}" where Text already
        // ends in "= ?", rendering as e.g. "12 × 5 = ? 60" on the results review screen.
        var room = new Room { Id = Guid.NewGuid(), Name = "Calc Room", Topic = RoomTopic.Math, QuestionCount = 10 };

        var questions = await mode.PrepareQuestionsAsync(room, questionRepo: null!, CancellationToken.None);

        Assert.All(questions, q =>
        {
            Assert.DoesNotContain("= ?", q.Explanation);
            Assert.EndsWith($"= {q.CorrectNumericAnswer}", q.Explanation);
        });
    }
}
