namespace BrainArena.Application.Matches;

public record PlayerAnswer(Guid UserId, int? SelectedOptionIndex);

/// <summary>
/// A raw answer submission, shape-agnostic across game modes. Only one field is populated,
/// depending on which mode the room is running — each IGameMode validates the field it expects
/// and ignores the other. A closed struct rather than a generic bag: revisit if a third mode
/// needs a genuinely different shape (e.g. free text), at which point collapsing to a single
/// opaque raw value each mode parses itself may be cleaner than adding a third nullable field.
/// </summary>
public readonly record struct SubmittedAnswer(int? OptionIndex, decimal? NumericValue);

public record ScoreResult(int Points, bool IsCorrect);

public record QuestionClientPayload(
    Guid MatchId,
    Guid MatchQuestionId,
    int Index,
    int TotalQuestions,
    string Kind,
    string Text,
    IReadOnlyList<string>? Options,
    DateTimeOffset EndsAtUtc);

public record ScoreboardEntry(Guid UserId, string DisplayName, int Score, bool IsConnected);

public record QuestionRevealPayload(
    Guid MatchId,
    Guid MatchQuestionId,
    int Index,
    string Kind,
    int? CorrectOptionIndex,
    decimal? CorrectNumericAnswer,
    string Explanation,
    DateTimeOffset EndsAtUtc,
    IReadOnlyList<ScoreboardEntry> Scoreboard);

public record RankingEntry(Guid UserId, string DisplayName, int Score, int Rank);

public record QuestionReviewEntry(
    int Index,
    string Kind,
    string Text,
    IReadOnlyList<string>? Options,
    int? CorrectOptionIndex,
    decimal? CorrectNumericAnswer,
    string Explanation);

public record MatchEndedPayload(Guid MatchId, IReadOnlyList<RankingEntry> Ranking, IReadOnlyList<QuestionReviewEntry> Review);

public record MatchResyncPayload(
    Guid MatchId,
    string Phase,
    QuestionClientPayload? CurrentQuestion,
    QuestionRevealPayload? CurrentReveal,
    int YourScore,
    IReadOnlyList<ScoreboardEntry> Scoreboard);

/// <summary>
/// Sync payload for a spectator joining mid-match — a deliberate sibling of
/// <see cref="MatchResyncPayload"/>, not a reuse of it, so it can never structurally carry a
/// personalized score for a non-participant.
/// </summary>
public record MatchSpectatorSyncPayload(
    Guid MatchId,
    string Phase,
    QuestionClientPayload? CurrentQuestion,
    QuestionRevealPayload? CurrentReveal,
    IReadOnlyList<ScoreboardEntry> Scoreboard);

public record MatchResultsDto(
    Guid MatchId,
    Guid RoomId,
    string RoomName,
    IReadOnlyList<RankingEntry> Ranking,
    IReadOnlyList<QuestionReviewEntry> Review);
