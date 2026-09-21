using BrainArena.Application.Abstractions;
using BrainArena.Domain.Entities;
using BrainArena.Domain.Enums;
using BrainArena.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BrainArena.Infrastructure.Repositories;

public class QuestionRepository(BrainArenaDbContext db) : IQuestionRepository
{
    public Task<List<Question>> GetRandomByTopicAsync(RoomTopic topic, int count, CancellationToken ct = default) =>
        db.Questions
            .Where(q => q.Topic == topic)
            .OrderBy(_ => EF.Functions.Random())
            .Take(count)
            .ToListAsync(ct);

    public Task<int> CountAsync(CancellationToken ct = default) => db.Questions.CountAsync(ct);

    public Task<List<Question>> GetAllAsync(RoomTopic? topic, CancellationToken ct = default) =>
        db.Questions
            .Where(q => topic == null || q.Topic == topic)
            .OrderBy(q => q.Topic).ThenBy(q => q.Text)
            .ToListAsync(ct);

    public Task<Question?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.Questions.FirstOrDefaultAsync(q => q.Id == id, ct);

    public async Task AddAsync(Question question, CancellationToken ct = default) =>
        await db.Questions.AddAsync(question, ct);

    public Task SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
