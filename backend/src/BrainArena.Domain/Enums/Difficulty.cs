namespace BrainArena.Domain.Enums;

/// <summary>
/// A room/tournament-level difficulty tier (currently only meaningful for FlashArithmeticGameMode,
/// ignored by other modes the same way Topic is ignored by calculation-mode rooms). Not to be
/// confused with Question.Difficulty (an int, "this round's numeric Level") — different concept,
/// same word, deliberately kept as two separate types.
/// </summary>
public enum Difficulty
{
    Easy = 0,
    Medium = 1,
    Hard = 2
}
