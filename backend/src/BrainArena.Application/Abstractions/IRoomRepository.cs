using BrainArena.Domain.Entities;
using BrainArena.Domain.Enums;

namespace BrainArena.Application.Abstractions;

public interface IRoomRepository
{
    /// <summary>Public rooms that are still Waiting or InProgress, with players loaded, newest-active first.</summary>
    Task<List<Room>> GetOpenRoomsAsync(CancellationToken ct = default);

    Task<Room?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Room?> GetByShareCodeAsync(string shareCode, CancellationToken ct = default);
    Task AddAsync(Room room, CancellationToken ct = default);

    /// <summary>
    /// Scalar, non-tracking status projection. Needed specifically for re-checking a room's status
    /// on the SAME DbContext that just created it (e.g. after a solitary room's synchronous
    /// auto-start) — a second GetByIdAsync call on that DbContext would hit EF Core's identity map
    /// and return the *same already-tracked instance* without refreshing it from the database,
    /// exactly the class of stale-read bug documented in CLAUDE.md's Mini-tournaments section. A
    /// scalar projection never enters the change tracker, so it can't fall into that trap.
    /// </summary>
    Task<RoomStatus?> GetStatusNoTrackingAsync(Guid id, CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);
}
