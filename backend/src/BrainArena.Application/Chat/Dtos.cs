namespace BrainArena.Application.Chat;

public record ChatMessageDto(
    Guid Id,
    Guid UserId,
    string DisplayName,
    string Text,
    DateTimeOffset SentAt,
    bool IsReported);
