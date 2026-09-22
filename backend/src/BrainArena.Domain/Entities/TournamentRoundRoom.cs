namespace BrainArena.Domain.Entities;

/// <summary>
/// Links one of a tournament round's rooms to the actual Room that plays it — all real gameplay
/// (countdown, questions, scoring) happens through the existing Room/Match/MatchOrchestrator
/// machinery unmodified; this is purely bookkeeping for "which room belongs to which tournament
/// round" so MatchOrchestrator can notify tournament progress when the room's match finishes.
/// </summary>
public class TournamentRoundRoom
{
    public Guid Id { get; set; }
    public Guid TournamentRoundId { get; set; }
    public Guid RoomId { get; set; }

    public TournamentRound? TournamentRound { get; set; }
    public Room? Room { get; set; }
}
