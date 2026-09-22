using BrainArena.Application.Abstractions;
using BrainArena.Application.Common;
using BrainArena.Domain.Entities;

namespace BrainArena.Application.Matches;

public class MultipleChoiceGameMode : IGameMode
{
    public const string Key = "multiple-choice";

    public string ModeKey => Key;

    public async Task<IReadOnlyList<Question>> PrepareQuestionsAsync(Room room, IQuestionRepository questionRepo, CancellationToken ct) =>
        await questionRepo.GetRandomByTopicAsync(room.Topic, room.QuestionCount, ct);

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
            RequireOptions(question),
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
            question.CorrectOptionIndex,
            null,
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
            RequireOptions(question),
            question.CorrectOptionIndex,
            null,
            question.Explanation);
    }

    public ScoreResult EvaluateAnswer(MatchQuestion matchQuestion, SubmittedAnswer answer, TimeSpan timeRemaining, TimeSpan timeLimit)
    {
        var question = RequireQuestion(matchQuestion);
        var options = RequireOptions(question);
        if (answer.OptionIndex is null || answer.OptionIndex < 0 || answer.OptionIndex >= options.Count)
        {
            throw new AppException("Invalid option.", 400);
        }

        var isCorrect = answer.OptionIndex.Value == question.CorrectOptionIndex;
        var points = ScoringService.ComputePoints(isCorrect, timeRemaining, timeLimit);
        return new ScoreResult(points, isCorrect);
    }

    private static Question RequireQuestion(MatchQuestion matchQuestion) =>
        matchQuestion.Question ?? throw new InvalidOperationException("MatchQuestion.Question must be loaded.");

    private static IReadOnlyList<string> RequireOptions(Question question) =>
        question.Options ?? throw new InvalidOperationException("Multiple-choice question is missing its options.");
}

public class GameModeRegistry(IEnumerable<IGameMode> modes) : IGameModeRegistry
{
    private readonly Dictionary<string, IGameMode> _modes = modes.ToDictionary(m => m.ModeKey);

    public IReadOnlyCollection<string> ModeKeys => _modes.Keys;

    public IGameMode Resolve(string modeKey) =>
        _modes.TryGetValue(modeKey, out var mode)
            ? mode
            : throw new InvalidOperationException($"Unknown game mode '{modeKey}'.");
}
