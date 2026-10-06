using BrainArena.Application.Abstractions;
using BrainArena.Domain.Enums;
using BrainArena.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BrainArena.Infrastructure.Repositories;

public class GuestCleanup(BrainArenaDbContext db) : IGuestCleanup
{
    public async Task<int> DeleteGuestsCreatedBeforeAsync(DateTimeOffset cutoff, int batchSize, CancellationToken ct = default)
    {
        // Only guests whose whole footprint is their own rooms; anything entangled with real
        // players' history (shared rooms, tournaments) is left in place rather than rewritten.
        var guestIds = await db.Users
            .Where(u => u.Role == UserRole.Guest && u.CreatedAt < cutoff)
            .Where(u => !db.Tournaments.Any(t => t.CreatorUserId == u.Id))
            .Where(u => !db.TournamentPlayers.Any(tp => tp.UserId == u.Id))
            .Where(u => !db.RoomPlayers.Any(rp => rp.UserId == u.Id && rp.Room!.HostUserId != u.Id))
            .Where(u => !db.RoomPlayers.Any(rp => rp.Room!.HostUserId == u.Id && rp.UserId != u.Id))
            .OrderBy(u => u.CreatedAt)
            .Select(u => u.Id)
            .Take(batchSize)
            .ToListAsync(ct);

        if (guestIds.Count == 0)
        {
            return 0;
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        // Calculation/flash questions are generated per match and referenced nowhere else, but
        // MatchQuestion -> Question is Restrict, so collect them before the rooms (and links) go.
        var generatedQuestionIds = await db.MatchQuestions
            .Where(mq => guestIds.Contains(mq.Match!.Room!.HostUserId) && mq.Question!.Type != QuestionType.MultipleChoice)
            .Select(mq => mq.QuestionId)
            .ToListAsync(ct);

        // Cascades to RoomPlayers, ChatMessages, Matches -> MatchQuestions/MatchPlayers -> MatchAnswers.
        await db.Rooms.Where(r => guestIds.Contains(r.HostUserId)).ExecuteDeleteAsync(ct);
        await db.Questions.Where(q => generatedQuestionIds.Contains(q.Id)).ExecuteDeleteAsync(ct);
        var deleted = await db.Users.Where(u => guestIds.Contains(u.Id)).ExecuteDeleteAsync(ct);

        await transaction.CommitAsync(ct);
        return deleted;
    }
}
