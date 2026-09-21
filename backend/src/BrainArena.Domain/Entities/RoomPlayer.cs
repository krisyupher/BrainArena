namespace BrainArena.Domain.Entities;

public class RoomPlayer
{
    public Guid RoomId { get; set; }
    public Guid UserId { get; set; }
    public DateTimeOffset JoinedAt { get; set; }

    public Room? Room { get; set; }
    public User? User { get; set; }
}
