using BrainArena.Application.Abstractions;
using BrainArena.Domain.Entities;
using BrainArena.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BrainArena.Infrastructure.Repositories;

public class UserRepository(BrainArenaDbContext db) : IUserRepository
{
    public Task<User?> GetByEmailAsync(string email, CancellationToken ct = default) =>
        db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);

    public Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);

    public async Task AddAsync(User user, CancellationToken ct = default) =>
        await db.Users.AddAsync(user, ct);

    public Task SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
