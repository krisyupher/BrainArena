namespace BrainArena.Application.Matches;

public record PlayerAnswer(Guid UserId, int? SelectedOptionIndex);

public record ScoreResult(int Points, bool IsCorrect);

public record QuestionClientPayload(
    Guid MatchId,
    Guid MatchQuestionId,
    int Index,
    int TotalQuestions,
    string Text,
    IReadOnlyList<string> Options,
    DateTimeOffset EndsAtUtc);

public record ScoreboardEntry(Guid UserId, string DisplayName, int Score, bool IsConnected);

public record QuestionRevealPayload(
    Guid MatchId,
    Guid MatchQuestionId,
    int Index,
    int CorrectOptionIndex,
    string Explanation,
    DateTimeOffset EndsAtUtc,
    IReadOnlyList<ScoreboardEntry> Scoreboard);

public record RankingEntry(Guid UserId, string DisplayName, int Score, int Rank);

public record QuestionReviewEntry(
    int Index,
    string Text,
    IReadOnlyList<string> Options,
    int CorrectOptionIndex,
    string Explanation);

public record MatchEndedPayload(Guid MatchId, IReadOnlyList<RankingEntry> Ranking, IReadOnlyList<QuestionReviewEntry> Review);

public record MatchResyncPayload(
    Guid MatchId,
    string Phase,
    QuestionClientPayload? CurrentQuestion,
    QuestionRevealPayload? CurrentReveal,
    int YourScore,
    IReadOnlyList<ScoreboardEntry> Scoreboard);

public record MatchResultsDto(
    Guid MatchId,
    Guid RoomId,
    string RoomName,
    IReadOnlyList<RankingEntry> Ranking,
    IReadOnlyList<QuestionReviewEntry> Review);
