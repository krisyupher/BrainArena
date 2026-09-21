using BrainArena.Domain.Entities;

namespace BrainArena.Application.Matches;

public class MultipleChoiceGameMode : IGameMode
{
    public const string Key = "multiple-choice";

    public string ModeKey => Key;

    public QuestionClientPayload ToClientPayload(MatchQuestion matchQuestion, int index, int totalQuestions, DateTimeOffset endsAtUtc)
    {
        var question = RequireQuestion(matchQuestion);
        return new QuestionClientPayload(
            matchQuestion.MatchId,
            matchQuestion.Id,
            index,
            totalQuestions,
            question.Text,
            question.Options,
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
            question.CorrectOptionIndex,
            question.Explanation,
            endsAtUtc,
            scoreboard);
    }

    public ScoreResult EvaluateAnswer(MatchQuestion matchQuestion, int? selectedOptionIndex, TimeSpan timeRemaining, TimeSpan timeLimit)
    {
        var question = RequireQuestion(matchQuestion);
        var isCorrect = selectedOptionIndex.HasValue && selectedOptionIndex.Value == question.CorrectOptionIndex;
        var points = ScoringService.ComputePoints(isCorrect, timeRemaining, timeLimit);
        return new ScoreResult(points, isCorrect);
    }

    private static Question RequireQuestion(MatchQuestion matchQuestion) =>
        matchQuestion.Question ?? throw new InvalidOperationException("MatchQuestion.Question must be loaded.");
}

public class GameModeRegistry(IEnumerable<IGameMode> modes) : IGameModeRegistry
{
    private readonly Dictionary<string, IGameMode> _modes = modes.ToDictionary(m => m.ModeKey);

    public IGameMode Resolve(string modeKey) =>
        _modes.TryGetValue(modeKey, out var mode)
            ? mode
            : throw new InvalidOperationException($"Unknown game mode '{modeKey}'.");
}
