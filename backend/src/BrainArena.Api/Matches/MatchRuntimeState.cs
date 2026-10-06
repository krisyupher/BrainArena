using System.Collections.Concurrent;
using BrainArena.Application.Matches;
using BrainArena.Domain.Entities;
using BrainArena.Domain.Enums;

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
    public required List<MatchQuestionRuntime> Questions { get; init; }
    public required Dictionary<Guid, PlayerRuntime> Players { get; init; }

    /// <summary>
    /// Cached off Room at match start rather than re-queried per round — everything an
    /// incrementally-generating mode needs is immutable for the match's lifetime, so caching avoids
    /// both an extra DB round-trip per round and the EF Core stale-read trap this project has
    /// already hit twice (see CLAUDE.md's Mini-tournaments and "Rooms have a Kind" sections).
    /// </summary>
    public required RoomTopic Topic { get; init; }
    public required Difficulty Difficulty { get; init; }

    /// <summary>
    /// The match's true round count — for an incrementally-generating mode, Questions.Count starts
    /// at 1 and grows as rounds are generated, so payloads must report this instead (see
    /// RunMatchLoopAsync/Join/Snapshot) or a client would see "Question 1 of 1" grow live.
    /// </summary>
    public required int TargetQuestionCount { get; init; }

    /// <summary>Opaque to the orchestrator — only the mode itself assigns meaning (Level, for FlashArithmeticGameMode).</summary>
    public int AdaptiveState { get; set; }

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

internal record PlayerAnswerRuntime(SubmittedAnswer Answer, int PointsAwarded, bool IsCorrect);

internal class PlayerRuntime
{
    public required Guid UserId { get; init; }
    public required string DisplayName { get; init; }
    public int Score { get; set; }
    public bool IsConnected { get; set; } = true;
    public DateTimeOffset? DisconnectedAt { get; set; }
}
