using System.Collections.Concurrent;
using BrainArena.Application.Matches;
using BrainArena.Domain.Entities;

namespace BrainArena.Api.Matches;

internal enum MatchPhase
{
    Countdown,
    Question,
    Reveal,
    Finished
}

/// <summary>All the live, in-memory state for one active match, keyed by RoomId in the orchestrator.</summary>
internal class MatchRuntimeState
{
    public required Guid MatchId { get; init; }
    public required Guid RoomId { get; init; }
    public required IGameMode GameMode { get; init; }
    public required int SecondsPerQuestion { get; init; }
    public required IReadOnlyList<MatchQuestionRuntime> Questions { get; init; }
    public required Dictionary<Guid, PlayerRuntime> Players { get; init; }

    public MatchPhase Phase { get; set; } = MatchPhase.Countdown;
    public int CurrentIndex { get; set; } = -1;
    public DateTimeOffset PhaseEndsAtUtc { get; set; }

    public MatchQuestionRuntime CurrentQuestion => Questions[CurrentIndex];

    public IReadOnlyList<ScoreboardEntry> BuildScoreboard() =>
        Players.Values
            .OrderByDescending(p => p.Score)
            .Select(p => new ScoreboardEntry(p.UserId, p.DisplayName, p.Score, p.IsConnected))
            .ToList();
}

internal class MatchQuestionRuntime
{
    public required MatchQuestion MatchQuestion { get; init; }

    /// <summary>userId -> the answer they submitted for this question.</summary>
    public ConcurrentDictionary<Guid, PlayerAnswerRuntime> Answers { get; } = new();
}

internal record PlayerAnswerRuntime(int? SelectedOptionIndex, int PointsAwarded, bool IsCorrect);

internal class PlayerRuntime
{
    public required Guid UserId { get; init; }
    public required string DisplayName { get; init; }
    public int Score { get; set; }
    public bool IsConnected { get; set; } = true;
    public DateTimeOffset? DisconnectedAt { get; set; }
}
