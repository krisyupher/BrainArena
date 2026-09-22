using BrainArena.Application.Abstractions;
using BrainArena.Domain.Entities;
using BrainArena.Domain.Enums;
using BrainArena.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BrainArena.Infrastructure.Repositories;

public class TournamentRepository(BrainArenaDbContext db) : ITournamentRepository
{
    public async Task<List<Tournament>> GetOpenTournamentsAsync(CancellationToken ct = default) =>
        await db.Tournaments
            .Include(t => t.Players)
            .Where(t => t.Status == TournamentStatus.Waiting || t.Status == TournamentStatus.InProgress)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(ct);

    public Task<Tournament?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.Tournaments
            .Include(t => t.CreatorUser)
            .Include(t => t.Players).ThenInclude(p => p.User)
            .Include(t => t.Rounds).ThenInclude(r => r.RoundRooms).ThenInclude(rr => rr.Room!).ThenInclude(room => room.Players).ThenInclude(rp => rp.User)
            .AsSplitQuery()
            .FirstOrDefaultAsync(t => t.Id == id, ct);

    public Task<TournamentRoundRoom?> GetRoundRoomByRoomIdAsync(Guid roomId, CancellationToken ct = default) =>
        db.TournamentRoundRooms
            .Include(rr => rr.TournamentRound!.Tournament!.Players)
            .Include(rr => rr.TournamentRound!.RoundRooms).ThenInclude(rr2 => rr2.Room)
            .AsSplitQuery()
            .FirstOrDefaultAsync(rr => rr.RoomId == roomId, ct);

    public Task<Guid?> GetTournamentIdForRoomAsync(Guid roomId, CancellationToken ct = default) =>
        db.TournamentRoundRooms
            .Where(rr => rr.RoomId == roomId)
            .Select(rr => (Guid?)rr.TournamentRound!.TournamentId)
            .FirstOrDefaultAsync(ct);

    public async Task<List<(Guid UserId, int Score, int Rank)>> GetRoomRankingAsync(Guid roomId, CancellationToken ct = default)
    {
        var players = await db.MatchPlayers
            .Where(mp => mp.Match!.RoomId == roomId)
            .OrderBy(mp => mp.FinalRank ?? int.MaxValue)
            .Select(mp => new { mp.UserId, mp.Score, mp.FinalRank })
            .ToListAsync(ct);

        return players
            .Select((p, index) => (p.UserId, p.Score, p.FinalRank ?? index + 1))
            .ToList();
    }

    public async Task AddAsync(Tournament tournament, CancellationToken ct = default) =>
        await db.Tournaments.AddAsync(tournament, ct);

    public async Task AddRoundAsync(TournamentRound round, CancellationToken ct = default) =>
        await db.TournamentRounds.AddAsync(round, ct);

    public async Task AddRoundRoomAsync(TournamentRoundRoom roundRoom, CancellationToken ct = default) =>
        await db.TournamentRoundRooms.AddAsync(roundRoom, ct);

    public Task SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
