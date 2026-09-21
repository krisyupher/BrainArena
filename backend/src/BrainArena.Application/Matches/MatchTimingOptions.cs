namespace BrainArena.Application.Matches;

/// <summary>
/// The countdown-before-questions and reveal-after-each-question durations. Defaults match the
/// brief exactly (5s / 5s); only overridden in the integration test host, so a full match doesn't
/// have to wait through two full 5-second pauses per question on top of the (brief-mandated,
/// non-configurable) 10-60s per-question answer window.
/// </summary>
public class MatchTimingOptions
{
    public const string SectionName = "MatchTiming";

    public int CountdownSeconds { get; set; } = 5;
    public int RevealSeconds { get; set; } = 5;
}
