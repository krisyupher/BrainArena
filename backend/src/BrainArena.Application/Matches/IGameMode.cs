using BrainArena.Domain.Entities;

namespace BrainArena.Application.Matches;

/// <summary>
/// A pluggable quiz mode. Only <see cref="MultipleChoiceGameMode"/> exists today, but new modes
/// (memory, math drills, ...) can be added later without touching room/match orchestration —
/// the orchestrator only ever talks to a room's mode through this interface.
/// </summary>
public interface IGameMode
{
    string ModeKey { get; }

    /// <summary>Never includes the correct answer — safe to send before the question closes.</summary>
    QuestionClientPayload ToClientPayload(MatchQuestion matchQuestion, int index, int totalQuestions, DateTimeOffset endsAtUtc);

    /// <summary>Only called once a question has closed — safe to include the correct answer.</summary>
    QuestionRevealPayload ToRevealPayload(
        MatchQuestion matchQuestion,
        int index,
        DateTimeOffset endsAtUtc,
        IReadOnlyList<ScoreboardEntry> scoreboard);

    /// <summary>Server-authoritative scoring: timeRemaining/timeLimit come from the server's own clock, never the client.</summary>
    ScoreResult EvaluateAnswer(MatchQuestion matchQuestion, int? selectedOptionIndex, TimeSpan timeRemaining, TimeSpan timeLimit);
}

public interface IGameModeRegistry
{
    IGameMode Resolve(string modeKey);
}
