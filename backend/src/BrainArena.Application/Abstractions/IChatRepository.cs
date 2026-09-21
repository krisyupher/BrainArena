using BrainArena.Domain.Entities;

namespace BrainArena.Application.Abstractions;

public interface IChatRepository
{
    Task<List<ChatMessage>> GetByRoomIdAsync(Guid roomId, CancellationToken ct = default);
    Task<ChatMessage?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(ChatMessage message, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
