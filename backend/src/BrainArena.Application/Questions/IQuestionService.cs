using BrainArena.Domain.Enums;

namespace BrainArena.Application.Questions;

public interface IQuestionService
{
    Task<IReadOnlyList<QuestionDto>> GetAllAsync(RoomTopic? topic, CancellationToken ct = default);
    Task<QuestionDto> CreateAsync(QuestionUpsertRequest request, CancellationToken ct = default);
    Task<QuestionDto> UpdateAsync(Guid id, QuestionUpsertRequest request, CancellationToken ct = default);
    Task<QuestionImportResult> ImportAsync(IReadOnlyList<QuestionUpsertRequest> requests, CancellationToken ct = default);
}
