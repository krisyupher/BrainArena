using BrainArena.Application.Abstractions;
using BrainArena.Domain.Entities;
using BrainArena.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BrainArena.Infrastructure.Repositories;

public class ChatRepository(BrainArenaDbContext db) : IChatRepository
{
    public Task<List<ChatMessage>> GetByRoomIdAsync(Guid roomId, CancellationToken ct = default) =>
        db.ChatMessages
            .Include(m => m.User)
            .Where(m => m.RoomId == roomId)
            .OrderBy(m => m.SentAt)
            .ToListAsync(ct);

    public Task<ChatMessage?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.ChatMessages.FirstOrDefaultAsync(m => m.Id == id, ct);

    public async Task AddAsync(ChatMessage message, CancellationToken ct = default) =>
        await db.ChatMessages.AddAsync(message, ct);

    public Task SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
