namespace BrainArena.Domain.Entities;

public class TournamentRound
{
    public Guid Id { get; set; }
    public Guid TournamentId { get; set; }
    public int RoundNumber { get; set; }

    /// <summary>True when every remaining player fits in this round's single room — its own
    /// match ranking crowns the champion, no further advancement.</summary>
    public bool IsFinal { get; set; }

    public Tournament? Tournament { get; set; }
    public ICollection<TournamentRoundRoom> RoundRooms { get; set; } = new List<TournamentRoundRoom>();
}
