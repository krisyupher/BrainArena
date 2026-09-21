using BrainArena.Domain.Entities;

namespace BrainArena.Application.Abstractions;

public interface IRoomRepository
{
    /// <summary>Public rooms that are still Waiting or InProgress, with players loaded, newest-active first.</summary>
    Task<List<Room>> GetOpenRoomsAsync(CancellationToken ct = default);

    Task<Room?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Room?> GetByShareCodeAsync(string shareCode, CancellationToken ct = default);
    Task AddAsync(Room room, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
