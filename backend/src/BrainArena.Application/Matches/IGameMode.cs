using BrainArena.Application.Abstractions;
using BrainArena.Domain.Entities;

namespace BrainArena.Application.Matches;

/// <summary>
/// A pluggable quiz mode. <see cref="MultipleChoiceGameMode"/> and <see cref="CalculationGameMode"/>
/// exist today; more can be added later without touching room/match orchestration — the
/// orchestrator only ever talks to a room's mode through this interface.
/// </summary>
public interface IGameMode
{
    string ModeKey { get; }

    /// <summary>
    /// Produces this room's match-scoped questions at match start. Multiple-choice pulls from the
    /// admin-curated bank via <paramref name="questionRepo"/>; other modes may generate content on
    /// the fly instead — MatchOrchestrator stays unaware of which. questionRepo is passed in
    /// (rather than constructor-injected) because IGameMode implementations are registered as
    /// singletons but the repository is scoped.
    /// </summary>
    Task<IReadOnlyList<Question>> PrepareQuestionsAsync(Room room, IQuestionRepository questionRepo, CancellationToken ct);

    /// <summary>Never includes the correct answer — safe to send before the question closes.</summary>
    QuestionClientPayload ToClientPayload(MatchQuestion matchQuestion, int index, int totalQuestions, DateTimeOffset endsAtUtc);

    /// <summary>Only called once a question has closed — safe to include the correct answer.</summary>
    QuestionRevealPayload ToRevealPayload(
        MatchQuestion matchQuestion,
        int index,
        DateTimeOffset endsAtUtc,
        IReadOnlyList<ScoreboardEntry> scoreboard);

    /// <summary>Post-match review entry — the match is over, so this is always safe to include the correct answer.</summary>
    QuestionReviewEntry ToReviewEntry(MatchQuestion matchQuestion, int index);

    /// <summary>
    /// Server-authoritative scoring: timeRemaining/timeLimit come from the server's own clock,
    /// never the client. Also responsible for validating the raw answer's shape (e.g. an option
    /// index actually within range) — that check belongs to the mode that defines the shape, not
    /// to the orchestrator.
    /// </summary>
    ScoreResult EvaluateAnswer(MatchQuestion matchQuestion, SubmittedAnswer answer, TimeSpan timeRemaining, TimeSpan timeLimit);

    /// <summary>
    /// True for modes whose round content must be decided one round at a time instead of all
    /// upfront — e.g. FlashArithmeticGameMode's Solitary live-adaptive difficulty, where round N+1
    /// depends on whether round N was answered correctly. Default interface method so existing
    /// modes need zero changes: false means MatchOrchestrator keeps using PrepareQuestionsAsync's
    /// upfront list exactly as before.
    /// </summary>
    bool RequiresIncrementalGeneration(Room room) => false;

    /// <summary>
    /// Only called when RequiresIncrementalGeneration is true. currentAdaptiveState/
    /// previousAnswerWasCorrect are opaque to MatchOrchestrator — only the mode itself assigns
    /// meaning to them (for FlashArithmeticGameMode, the adaptive state is the current Level).
    /// previousAnswerWasCorrect == null signals the very first round of the match, in which case
    /// the mode should reset to its own baseline regardless of currentAdaptiveState.
    /// </summary>
    Task<(Question Question, int NextAdaptiveState)> PrepareNextQuestionAsync(
        Room room, int currentAdaptiveState, bool? previousAnswerWasCorrect, IQuestionRepository questionRepo, CancellationToken ct)
        => throw new NotSupportedException($"{ModeKey} does not support incremental generation.");
}

public interface IGameModeRegistry
{
    IGameMode Resolve(string modeKey);
    IReadOnlyCollection<string> ModeKeys { get; }
}
