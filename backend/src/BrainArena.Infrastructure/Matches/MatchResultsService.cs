using BrainArena.Application.Common;
using BrainArena.Application.Matches;
using BrainArena.Domain.Enums;
using BrainArena.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BrainArena.Infrastructure.Matches;

public class MatchResultsService(BrainArenaDbContext db) : IMatchResultsService
{
    public async Task<MatchResultsDto> GetResultsByRoomAsync(Guid roomId, CancellationToken ct = default)
    {
        var match = await db.Matches
            .Include(m => m.Room)
            .Include(m => m.Players).ThenInclude(p => p.User)
            .Include(m => m.Questions.OrderBy(q => q.OrderIndex)).ThenInclude(q => q.Question)
            .Where(m => m.RoomId == roomId)
            .OrderByDescending(m => m.CreatedAt)
            .FirstOrDefaultAsync(ct)
            ?? throw new AppException("No match found for this room.", 404);

        if (match.Status != MatchStatus.Finished)
        {
            throw new AppException("This match hasn't finished yet.", 409);
        }

        var ranking = match.Players
            .OrderBy(p => p.FinalRank ?? int.MaxValue)
            .Select(p => new RankingEntry(p.UserId, p.User?.DisplayName ?? string.Empty, p.Score, p.FinalRank ?? 0))
            .ToList();

        var review = match.Questions
            .OrderBy(q => q.OrderIndex)
            .Select(q => new QuestionReviewEntry(
                q.OrderIndex,
                q.Question!.Text,
                q.Question.Options,
                q.Question.CorrectOptionIndex,
                q.Question.Explanation))
            .ToList();

        return new MatchResultsDto(match.Id, match.RoomId, match.Room?.Name ?? string.Empty, ranking, review);
    }
}
