namespace BrainArena.Domain.Entities;

public class MatchPlayer
{
    public Guid MatchId { get; set; }
    public Guid UserId { get; set; }
    public int Score { get; set; }
    public int? FinalRank { get; set; }

    public Match? Match { get; set; }
    public User? User { get; set; }
}
