using BrainArena.Domain.Enums;

namespace BrainArena.Domain.Entities;

public class Match
{
    public Guid Id { get; set; }
    public Guid RoomId { get; set; }
    public MatchStatus Status { get; set; } = MatchStatus.Countdown;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? EndedAt { get; set; }

    public Room? Room { get; set; }
    public ICollection<MatchQuestion> Questions { get; set; } = new List<MatchQuestion>();
    public ICollection<MatchPlayer> Players { get; set; } = new List<MatchPlayer>();
}
