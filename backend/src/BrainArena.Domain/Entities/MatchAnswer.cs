namespace BrainArena.Domain.Entities;

public class MatchAnswer
{
    public Guid MatchQuestionId { get; set; }
    public Guid UserId { get; set; }
    public int? SelectedOptionIndex { get; set; }
    public int PointsAwarded { get; set; }
    public DateTimeOffset AnsweredAt { get; set; }

    public MatchQuestion? MatchQuestion { get; set; }
    public User? User { get; set; }
}
