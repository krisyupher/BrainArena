using BrainArena.Domain.Enums;

namespace BrainArena.Application.Questions;

public record QuestionDto(
    Guid Id,
    RoomTopic Topic,
    int Difficulty,
    string Text,
    IReadOnlyList<string> Options,
    int CorrectOptionIndex,
    string Explanation,
    string Language);

public record QuestionUpsertRequest(
    RoomTopic Topic,
    int Difficulty,
    string Text,
    IReadOnlyList<string> Options,
    int CorrectOptionIndex,
    string Explanation,
    string Language);

public record QuestionImportResult(int ImportedCount, IReadOnlyList<string> Errors);
