using BrainArena.Application.Abstractions;
using BrainArena.Application.Common;
using BrainArena.Domain.Entities;
using BrainArena.Domain.Enums;

namespace BrainArena.Application.Matches;

/// <summary>
/// Numbers flash on screen one at a time; the player types the sum once they're all shown. Like
/// CalculationGameMode, every problem is a fresh, not-yet-persisted Question (Text = a
/// comma-delimited number sequence, e.g. "23,7,45,12" — the frontend flashes each one in turn
/// rather than displaying Text directly; CorrectNumericAnswer = the sum).
///
/// Multiplayer/Tournament rooms use the ordinary upfront PrepareQuestionsAsync path — every round
/// generated at the SAME fixed Level (derived from Room.Difficulty), identical for every player,
/// a hard fairness requirement ("everyone sees the same numbers at the same time"). Solitary rooms
/// use the incremental PrepareNextQuestionAsync path instead — Level adapts live, rising on a
/// correct answer and dropping on a miss, since Solitary is always exactly 1 player and there's no
/// fairness concern to protect.
///
/// Question.Difficulty (an existing int field, otherwise unused by this mode's siblings) is
/// repurposed here as the per-round numeric Level carrier so ToClientPayload can tell the client
/// what Level a round was — NOT the same concept as Domain.Enums.Difficulty (the Easy/Medium/Hard
/// room-creation tier), which only ever picks the *starting/fixed* Level.
/// </summary>
public class FlashArithmeticGameMode : IGameMode
{
    public const string Key = "flash-arithmetic";

    private const int MinLevel = 1;
    private const int MaxLevel = 20;

    // Number-count growth is capped — beyond this, further Level increases only speed up the
    // flash (a frontend-only concern, mirrored from Level) rather than adding more numbers,
    // so a long streak can never eat the whole answer window before the player gets to respond.
    private const int MinNumberCount = 3;
    private const int MaxNumberCount = 8;

    public string ModeKey => Key;

    public Task<IReadOnlyList<Question>> PrepareQuestionsAsync(Room room, IQuestionRepository questionRepo, CancellationToken ct)
    {
        var level = StartingLevelForDifficulty(room.Difficulty);
        var questions = new List<Question>(room.QuestionCount);
        for (var i = 0; i < room.QuestionCount; i++)
        {
            questions.Add(GenerateProblem(room.Topic, level));
        }

        return Task.FromResult<IReadOnlyList<Question>>(questions);
    }

    public bool RequiresIncrementalGeneration(Room room) => room.Kind == RoomKind.Solitary;

    public Task<(Question Question, int NextAdaptiveState)> PrepareNextQuestionAsync(
        Room room, int currentAdaptiveState, bool? previousAnswerWasCorrect, IQuestionRepository questionRepo, CancellationToken ct)
    {
        var level = previousAnswerWasCorrect switch
        {
            null => StartingLevelForDifficulty(room.Difficulty), // first round of the match
            true => Math.Min(currentAdaptiveState + 1, MaxLevel),
            false => Math.Max(currentAdaptiveState - 1, MinLevel) // wrong or timed out — both count as a miss
        };

        return Task.FromResult((GenerateProblem(room.Topic, level), level));
    }

    public QuestionClientPayload ToClientPayload(MatchQuestion matchQuestion, int index, int totalQuestions, DateTimeOffset endsAtUtc)
    {
        var question = RequireQuestion(matchQuestion);
        return new QuestionClientPayload(
            matchQuestion.MatchId,
            matchQuestion.Id,
            index,
            totalQuestions,
            Key,
            question.Text,
            null,
            endsAtUtc,
            question.Difficulty);
    }

    public QuestionRevealPayload ToRevealPayload(
        MatchQuestion matchQuestion,
        int index,
        DateTimeOffset endsAtUtc,
        IReadOnlyList<ScoreboardEntry> scoreboard)
    {
        var question = RequireQuestion(matchQuestion);
        return new QuestionRevealPayload(
            matchQuestion.MatchId,
            matchQuestion.Id,
            index,
            Key,
            null,
            RequireNumericAnswer(question),
            question.Explanation,
            endsAtUtc,
            scoreboard);
    }

    public QuestionReviewEntry ToReviewEntry(MatchQuestion matchQuestion, int index)
    {
        var question = RequireQuestion(matchQuestion);
        return new QuestionReviewEntry(
            index,
            Key,
            question.Text,
            null,
            null,
            RequireNumericAnswer(question),
            question.Explanation);
    }

    public ScoreResult EvaluateAnswer(MatchQuestion matchQuestion, SubmittedAnswer answer, TimeSpan timeRemaining, TimeSpan timeLimit)
    {
        var question = RequireQuestion(matchQuestion);
        if (answer.NumericValue is null)
        {
            throw new AppException("Invalid answer.", 400);
        }

        var isCorrect = answer.NumericValue.Value == RequireNumericAnswer(question);
        var points = ScoringService.ComputePoints(isCorrect, timeRemaining, timeLimit);
        return new ScoreResult(points, isCorrect);
    }

    private static int StartingLevelForDifficulty(Difficulty difficulty) => difficulty switch
    {
        Difficulty.Easy => 1,
        Difficulty.Medium => 3,
        Difficulty.Hard => 5,
        _ => 3
    };

    private static Question GenerateProblem(RoomTopic topic, int level)
    {
        var random = Random.Shared;
        var numberCount = Math.Clamp(2 + level, MinNumberCount, MaxNumberCount);

        var numbers = new List<int>(numberCount);
        for (var i = 0; i < numberCount; i++)
        {
            numbers.Add(random.Next(1, 100)); // 1-2 digit positive integers
        }

        var sum = numbers.Sum();
        var text = string.Join(",", numbers);
        var expression = string.Join(" + ", numbers);

        return new Question
        {
            Id = Guid.NewGuid(),
            Topic = topic,
            Difficulty = level,
            Type = QuestionType.FlashArithmetic,
            Text = text,
            Options = null,
            CorrectOptionIndex = null,
            CorrectNumericAnswer = sum,
            Explanation = $"{expression} = {sum}",
            Language = "es"
        };
    }

    private static Question RequireQuestion(MatchQuestion matchQuestion) =>
        matchQuestion.Question ?? throw new InvalidOperationException("MatchQuestion.Question must be loaded.");

    private static decimal RequireNumericAnswer(Question question) =>
        question.CorrectNumericAnswer ?? throw new InvalidOperationException("Flash-arithmetic question is missing its answer.");
}
