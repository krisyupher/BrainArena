using BrainArena.Application.Abstractions;
using BrainArena.Application.Common;
using BrainArena.Domain.Entities;
using BrainArena.Domain.Enums;

namespace BrainArena.Application.Matches;

/// <summary>
/// Players race to submit the correct numeric answer to a procedurally generated arithmetic
/// problem — no admin-authored bank involved. Each problem is a fresh, not-yet-persisted
/// <see cref="Question"/>; MatchOrchestrator's existing bulk-insert cascades it into the database
/// the same way it already does for bank-sourced multiple-choice questions, so this mode needed no
/// orchestration changes beyond sourcing questions through <see cref="IGameMode"/> instead of the
/// question repository directly.
/// </summary>
public class CalculationGameMode : IGameMode
{
    public const string Key = "calculation";

    private const int MinOperand = 2;
    private const int MaxOperand = 50;
    private const int MinFactor = 2;
    private const int MaxFactor = 12;

    public string ModeKey => Key;

    public Task<IReadOnlyList<Question>> PrepareQuestionsAsync(Room room, IQuestionRepository questionRepo, CancellationToken ct)
    {
        var questions = new List<Question>(room.QuestionCount);
        for (var i = 0; i < room.QuestionCount; i++)
        {
            questions.Add(GenerateProblem(room.Topic));
        }

        return Task.FromResult<IReadOnlyList<Question>>(questions);
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
            endsAtUtc);
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

    private static Question GenerateProblem(RoomTopic topic)
    {
        var random = Random.Shared;
        var op = random.Next(3);

        int a, b;
        decimal answer;
        string symbol;

        switch (op)
        {
            case 0:
                a = random.Next(MinOperand, MaxOperand + 1);
                b = random.Next(MinOperand, MaxOperand + 1);
                answer = a + b;
                symbol = "+";
                break;
            case 1:
                a = random.Next(MinOperand, MaxOperand + 1);
                b = random.Next(MinOperand, a + 1); // keeps the result non-negative
                answer = a - b;
                symbol = "-";
                break;
            default:
                a = random.Next(MinFactor, MaxFactor + 1);
                b = random.Next(MinFactor, MaxFactor + 1);
                answer = a * b;
                symbol = "×";
                break;
        }

        var expression = $"{a} {symbol} {b}";
        return new Question
        {
            Id = Guid.NewGuid(),
            Topic = topic,
            Difficulty = 1,
            Type = QuestionType.Calculation,
            Text = $"{expression} = ?",
            Options = null,
            CorrectOptionIndex = null,
            CorrectNumericAnswer = answer,
            Explanation = $"{expression} = {answer}",
            Language = "es"
        };
    }

    private static Question RequireQuestion(MatchQuestion matchQuestion) =>
        matchQuestion.Question ?? throw new InvalidOperationException("MatchQuestion.Question must be loaded.");

    private static decimal RequireNumericAnswer(Question question) =>
        question.CorrectNumericAnswer ?? throw new InvalidOperationException("Calculation question is missing its answer.");
}
