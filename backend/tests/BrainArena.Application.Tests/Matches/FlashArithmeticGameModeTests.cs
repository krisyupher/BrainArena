using BrainArena.Application.Common;
using BrainArena.Application.Matches;
using BrainArena.Domain.Entities;
using BrainArena.Domain.Enums;

namespace BrainArena.Application.Tests.Matches;

public class FlashArithmeticGameModeTests
{
    private readonly FlashArithmeticGameMode mode = new();

    private static MatchQuestion BuildMatchQuestion(string text = "20,22", decimal correctAnswer = 42m, int level = 3) => new()
    {
        Id = Guid.NewGuid(),
        MatchId = Guid.NewGuid(),
        QuestionId = Guid.NewGuid(),
        OrderIndex = 0,
        Question = new Question
        {
            Id = Guid.NewGuid(),
            Topic = RoomTopic.Math,
            Type = QuestionType.FlashArithmetic,
            Difficulty = level,
            Text = text,
            Options = null,
            CorrectOptionIndex = null,
            CorrectNumericAnswer = correctAnswer,
            Explanation = "20 + 22 = 42"
        }
    };

    [Fact]
    public void EvaluateAnswer_CorrectSum_AwardsPoints()
    {
        var matchQuestion = BuildMatchQuestion(correctAnswer: 42m);

        var result = mode.EvaluateAnswer(matchQuestion, new SubmittedAnswer(null, 42m), TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(20));

        Assert.True(result.IsCorrect);
        Assert.Equal(150, result.Points);
    }

    [Fact]
    public void EvaluateAnswer_WrongSum_ScoresZero()
    {
        var matchQuestion = BuildMatchQuestion(correctAnswer: 42m);

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
    public void ToClientPayload_NeverIncludesTheCorrectSum_ButDoesIncludeTheLevel()
    {
        var matchQuestion = BuildMatchQuestion(text: "20,22", level: 5);

        var payload = mode.ToClientPayload(matchQuestion, index: 0, totalQuestions: 5, DateTimeOffset.UtcNow);

        Assert.Equal(FlashArithmeticGameMode.Key, payload.Kind);
        Assert.Equal("20,22", payload.Text);
        Assert.Null(payload.Options);
        Assert.Equal(5, payload.Level);
        // QuestionClientPayload has no numeric-answer field at all — structurally impossible to leak it here.
    }

    [Fact]
    public void ToRevealPayload_IncludesTheCorrectSumButNoOptionIndex()
    {
        var matchQuestion = BuildMatchQuestion(correctAnswer: 42m);

        var payload = mode.ToRevealPayload(matchQuestion, index: 0, DateTimeOffset.UtcNow, []);

        Assert.Equal(FlashArithmeticGameMode.Key, payload.Kind);
        Assert.Equal(42m, payload.CorrectNumericAnswer);
        Assert.Null(payload.CorrectOptionIndex);
    }

    [Theory]
    [InlineData(RoomKind.Multiplayer)]
    [InlineData(RoomKind.Solitary)]
    public void RequiresIncrementalGeneration_OnlyTrueForSolitary(RoomKind kind)
    {
        var room = new Room { Id = Guid.NewGuid(), Name = "Flash Room", Kind = kind };

        Assert.Equal(kind == RoomKind.Solitary, mode.RequiresIncrementalGeneration(room));
    }

    [Fact]
    public async Task PrepareQuestionsAsync_GeneratesExactlyTheRequestedCount_AllAtTheSameFixedLevel()
    {
        var room = new Room { Id = Guid.NewGuid(), Name = "Flash Room", Topic = RoomTopic.Math, QuestionCount = 8, Difficulty = Difficulty.Hard };

        var questions = await mode.PrepareQuestionsAsync(room, questionRepo: null!, CancellationToken.None);

        Assert.Equal(8, questions.Count);
        Assert.All(questions, q => Assert.Equal(QuestionType.FlashArithmetic, q.Type));
        Assert.All(questions, q => Assert.NotNull(q.CorrectNumericAnswer));
        Assert.All(questions, q => Assert.Null(q.Options));
        // Fixed-difficulty fairness requirement: every round of a Multiplayer/Tournament match is
        // generated at the identical Level, never adapting mid-match.
        Assert.All(questions, q => Assert.Equal(questions[0].Difficulty, q.Difficulty));
    }

    [Fact]
    public async Task PrepareQuestionsAsync_TextIsACommaDelimitedSequenceWhoseSumIsTheCorrectAnswer()
    {
        var room = new Room { Id = Guid.NewGuid(), Name = "Flash Room", Topic = RoomTopic.Math, QuestionCount = 5 };

        var questions = await mode.PrepareQuestionsAsync(room, questionRepo: null!, CancellationToken.None);

        Assert.All(questions, q =>
        {
            var numbers = q.Text.Split(',').Select(int.Parse).ToList();
            Assert.Equal(numbers.Sum(), q.CorrectNumericAnswer);
            Assert.All(numbers, n => Assert.InRange(n, 1, 99));
        });
    }

    [Fact]
    public async Task PrepareNextQuestionAsync_FirstRound_ResetsToTheStartingLevelForDifficulty_RegardlessOfPassedInState()
    {
        var room = new Room { Id = Guid.NewGuid(), Name = "Flash Room", Topic = RoomTopic.Math, Difficulty = Difficulty.Hard };

        var (question, nextState) = await mode.PrepareNextQuestionAsync(room, currentAdaptiveState: 99, previousAnswerWasCorrect: null, questionRepo: null!, CancellationToken.None);

        Assert.Equal(5, nextState); // Hard's starting Level
        Assert.Equal(5, question.Difficulty);
    }

    [Fact]
    public async Task PrepareNextQuestionAsync_CorrectAnswer_StepsTheLevelUp()
    {
        var room = new Room { Id = Guid.NewGuid(), Name = "Flash Room", Topic = RoomTopic.Math };

        var (_, nextState) = await mode.PrepareNextQuestionAsync(room, currentAdaptiveState: 4, previousAnswerWasCorrect: true, questionRepo: null!, CancellationToken.None);

        Assert.Equal(5, nextState);
    }

    [Fact]
    public async Task PrepareNextQuestionAsync_WrongOrMissedAnswer_StepsTheLevelDown()
    {
        var room = new Room { Id = Guid.NewGuid(), Name = "Flash Room", Topic = RoomTopic.Math };

        var (_, nextState) = await mode.PrepareNextQuestionAsync(room, currentAdaptiveState: 4, previousAnswerWasCorrect: false, questionRepo: null!, CancellationToken.None);

        Assert.Equal(3, nextState);
    }

    [Fact]
    public async Task PrepareNextQuestionAsync_LevelNeverDropsBelowOne()
    {
        var room = new Room { Id = Guid.NewGuid(), Name = "Flash Room", Topic = RoomTopic.Math };

        var (_, nextState) = await mode.PrepareNextQuestionAsync(room, currentAdaptiveState: 1, previousAnswerWasCorrect: false, questionRepo: null!, CancellationToken.None);

        Assert.Equal(1, nextState);
    }

    [Fact]
    public async Task PrepareNextQuestionAsync_LevelNeverExceedsTheCeiling()
    {
        var room = new Room { Id = Guid.NewGuid(), Name = "Flash Room", Topic = RoomTopic.Math };

        var (_, nextState) = await mode.PrepareNextQuestionAsync(room, currentAdaptiveState: 20, previousAnswerWasCorrect: true, questionRepo: null!, CancellationToken.None);

        Assert.Equal(20, nextState);
    }
}
