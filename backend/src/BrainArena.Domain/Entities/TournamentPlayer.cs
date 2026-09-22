using BrainArena.Domain.Enums;

namespace BrainArena.Domain.Entities;

public class TournamentPlayer
{
    public Guid TournamentId { get; set; }
    public Guid UserId { get; set; }
    public DateTimeOffset JoinedAt { get; set; }
    public TournamentPlayerStatus Status { get; set; } = TournamentPlayerStatus.Active;

    /// <summary>Set when Status becomes Eliminated — which round they were knocked out in.</summary>
    public int? EliminatedAtRound { get; set; }

    public Tournament? Tournament { get; set; }
    public User? User { get; set; }
}
