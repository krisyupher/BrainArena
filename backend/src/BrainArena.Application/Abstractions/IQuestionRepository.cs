using BrainArena.Domain.Entities;
using BrainArena.Domain.Enums;

namespace BrainArena.Application.Abstractions;

public interface IQuestionRepository
{
    Task<List<Question>> GetRandomByTopicAsync(RoomTopic topic, int count, CancellationToken ct = default);
    Task<int> CountAsync(CancellationToken ct = default);
    Task<List<Question>> GetAllAsync(RoomTopic? topic, CancellationToken ct = default);
    Task<Question?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(Question question, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
