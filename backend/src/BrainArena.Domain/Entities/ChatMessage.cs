namespace BrainArena.Domain.Entities;

public class ChatMessage
{
    public Guid Id { get; set; }
    public Guid RoomId { get; set; }
    public Guid UserId { get; set; }
    public required string Text { get; set; }
    public DateTimeOffset SentAt { get; set; }
    public bool IsReported { get; set; }

    public Room? Room { get; set; }
    public User? User { get; set; }
}
