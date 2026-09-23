using BrainArena.Domain.Enums;

namespace BrainArena.Domain.Entities;

public class Room
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public RoomTopic Topic { get; set; }
    public int MaxPlayers { get; set; }
    public int MinPlayersToStart { get; set; }
    public int QuestionCount { get; set; }
    public int SecondsPerQuestion { get; set; }
    public bool IsPrivate { get; set; }
    public string? ShareCode { get; set; }
    public RoomStatus Status { get; set; } = RoomStatus.Waiting;
    public RoomKind Kind { get; set; } = RoomKind.Multiplayer;
    public Guid HostUserId { get; set; }
    public string GameMode { get; set; } = "multiple-choice";
    public DateTimeOffset CreatedAt { get; set; }

    public User? HostUser { get; set; }
    public ICollection<RoomPlayer> Players { get; set; } = new List<RoomPlayer>();
}
