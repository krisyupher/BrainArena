using BrainArena.Domain.Enums;

namespace BrainArena.Domain.Entities;

public class Question
{
    public Guid Id { get; set; }
    public RoomTopic Topic { get; set; }
    public int Difficulty { get; set; } = 1;
    public QuestionType Type { get; set; } = QuestionType.MultipleChoice;
    public required string Text { get; set; }

    // Multiple-choice only.
    public string[]? Options { get; set; }
    public int? CorrectOptionIndex { get; set; }

    // Calculation only.
    public decimal? CorrectNumericAnswer { get; set; }

    public required string Explanation { get; set; }
    public string Language { get; set; } = "es";
}
