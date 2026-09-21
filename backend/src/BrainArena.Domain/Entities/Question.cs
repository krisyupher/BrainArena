using BrainArena.Domain.Enums;

namespace BrainArena.Domain.Entities;

public class Question
{
    public Guid Id { get; set; }
    public RoomTopic Topic { get; set; }
    public int Difficulty { get; set; } = 1;
    public required string Text { get; set; }
    public required string[] Options { get; set; }
    public int CorrectOptionIndex { get; set; }
    public required string Explanation { get; set; }
    public string Language { get; set; } = "es";
}
