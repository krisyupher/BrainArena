using BrainArena.Application.Common;
using BrainArena.Application.Matches;
using BrainArena.Domain.Enums;
using BrainArena.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BrainArena.Infrastructure.Matches;

public class MatchResultsService(BrainArenaDbContext db, IGameModeRegistry gameModeRegistry) : IMatchResultsService
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

        // Same mode-shaping the live orchestrator uses (MatchOrchestrator.FinalizeMatchAsync) — the
        // match is over either way, so this is just the persisted-data equivalent of that path.
        var gameMode = gameModeRegistry.Resolve(match.Room?.GameMode ?? MultipleChoiceGameMode.Key);
        var review = match.Questions
            .OrderBy(q => q.OrderIndex)
            .Select(q => gameMode.ToReviewEntry(q, q.OrderIndex))
            .ToList();

        return new MatchResultsDto(match.Id, match.RoomId, match.Room?.Name ?? string.Empty, ranking, review);
    }
}
