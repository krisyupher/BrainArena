namespace BrainArena.Application.Chat;

public interface IChatService
{
    Task<ChatMessageDto> SendMessageAsync(Guid roomId, Guid userId, string text, CancellationToken ct = default);
    Task<IReadOnlyList<ChatMessageDto>> GetHistoryAsync(Guid roomId, CancellationToken ct = default);
    Task ReportMessageAsync(Guid messageId, CancellationToken ct = default);
}
