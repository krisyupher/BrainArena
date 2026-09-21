using BrainArena.Application.Abstractions;
using BrainArena.Application.Common;
using BrainArena.Domain.Entities;
using BrainArena.Domain.Enums;

namespace BrainArena.Application.Questions;

public class QuestionService(IQuestionRepository questions) : IQuestionService
{
    public async Task<IReadOnlyList<QuestionDto>> GetAllAsync(RoomTopic? topic, CancellationToken ct = default)
    {
        var all = await questions.GetAllAsync(topic, ct);
        return all.Select(MapDto).ToList();
    }

    public async Task<QuestionDto> CreateAsync(QuestionUpsertRequest request, CancellationToken ct = default)
    {
        QuestionValidation.Validate(request);

        var question = ToEntity(Guid.NewGuid(), request);
        await questions.AddAsync(question, ct);
        await questions.SaveChangesAsync(ct);

        return MapDto(question);
    }

    public async Task<QuestionDto> UpdateAsync(Guid id, QuestionUpsertRequest request, CancellationToken ct = default)
    {
        QuestionValidation.Validate(request);

        var question = await questions.GetByIdAsync(id, ct)
            ?? throw new AppException("Question not found.", 404);

        question.Topic = request.Topic;
        question.Difficulty = request.Difficulty;
        question.Text = request.Text.Trim();
        question.Options = request.Options.Select(o => o.Trim()).ToArray();
        question.CorrectOptionIndex = request.CorrectOptionIndex;
        question.Explanation = request.Explanation.Trim();
        question.Language = request.Language;

        await questions.SaveChangesAsync(ct);

        return MapDto(question);
    }

    public async Task<QuestionImportResult> ImportAsync(IReadOnlyList<QuestionUpsertRequest> requests, CancellationToken ct = default)
    {
        var errors = new List<string>();
        var toAdd = new List<Question>();

        for (var i = 0; i < requests.Count; i++)
        {
            try
            {
                QuestionValidation.Validate(requests[i]);
                toAdd.Add(ToEntity(Guid.NewGuid(), requests[i]));
            }
            catch (AppException ex)
            {
                errors.Add($"Item {i + 1}: {ex.Message}");
            }
        }

        foreach (var question in toAdd)
        {
            await questions.AddAsync(question, ct);
        }

        if (toAdd.Count > 0)
        {
            await questions.SaveChangesAsync(ct);
        }

        return new QuestionImportResult(toAdd.Count, errors);
    }

    private static Question ToEntity(Guid id, QuestionUpsertRequest request) => new()
    {
        Id = id,
        Topic = request.Topic,
        Difficulty = request.Difficulty,
        Text = request.Text.Trim(),
        Options = request.Options.Select(o => o.Trim()).ToArray(),
        CorrectOptionIndex = request.CorrectOptionIndex,
        Explanation = request.Explanation.Trim(),
        Language = request.Language
    };

    private static QuestionDto MapDto(Question q) =>
        new(q.Id, q.Topic, q.Difficulty, q.Text, q.Options, q.CorrectOptionIndex, q.Explanation, q.Language);
}
