using BrainArena.Application.Abstractions;
using BrainArena.Domain.Entities;
using BrainArena.Domain.Enums;
using BrainArena.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BrainArena.Infrastructure.Repositories;

public class RoomRepository(BrainArenaDbContext db) : IRoomRepository
{
    public async Task<List<Room>> GetOpenRoomsAsync(CancellationToken ct = default) =>
        await db.Rooms
            .Include(r => r.Players).ThenInclude(p => p.User)
            .Where(r => !r.IsPrivate && (r.Status == RoomStatus.Waiting || r.Status == RoomStatus.InProgress))
            .ToListAsync(ct);

    public Task<Room?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.Rooms
            .Include(r => r.Players).ThenInclude(p => p.User)
            .FirstOrDefaultAsync(r => r.Id == id, ct);

    public Task<Room?> GetByShareCodeAsync(string shareCode, CancellationToken ct = default) =>
        db.Rooms
            .Include(r => r.Players).ThenInclude(p => p.User)
            .FirstOrDefaultAsync(r => r.ShareCode == shareCode, ct);

    public async Task AddAsync(Room room, CancellationToken ct = default) =>
        await db.Rooms.AddAsync(room, ct);

    public Task<RoomStatus?> GetStatusNoTrackingAsync(Guid id, CancellationToken ct = default) =>
        db.Rooms.AsNoTracking().Where(r => r.Id == id).Select(r => (RoomStatus?)r.Status).FirstOrDefaultAsync(ct);

    public Task SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
