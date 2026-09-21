namespace BrainArena.Domain.Entities;

public class MatchQuestion
{
    public Guid Id { get; set; }
    public Guid MatchId { get; set; }
    public Guid QuestionId { get; set; }
    public int OrderIndex { get; set; }

    public Match? Match { get; set; }
    public Question? Question { get; set; }
    public ICollection<MatchAnswer> Answers { get; set; } = new List<MatchAnswer>();
}
