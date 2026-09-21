using BrainArena.Application.Abstractions;
using BrainArena.Application.Common;
using BrainArena.Domain.Entities;
using BrainArena.Domain.Enums;

namespace BrainArena.Application.Chat;

public class ChatService(
    IChatRepository chat,
    IRoomRepository rooms,
    IUserRepository users,
    IChatRateLimiter rateLimiter) : IChatService
{
    public async Task<ChatMessageDto> SendMessageAsync(Guid roomId, Guid userId, string text, CancellationToken ct = default)
    {
        ChatMessageValidation.Validate(text);

        var room = await rooms.GetByIdAsync(roomId, ct)
            ?? throw new AppException("Room not found.", 404);

        if (room.Status == RoomStatus.InProgress)
        {
            throw new AppException("Chat is disabled while a match is in progress.", 409);
        }

        if (room.Players.All(p => p.UserId != userId))
        {
            throw new AppException("You're not part of this room.", 403);
        }

        if (!rateLimiter.TryConsume(userId))
        {
            throw new AppException("You're sending messages too fast. Please wait a moment.", 429);
        }

        var user = await users.GetByIdAsync(userId, ct)
            ?? throw new AppException("User not found.", 404);

        var message = new ChatMessage
        {
            Id = Guid.NewGuid(),
            RoomId = roomId,
            UserId = userId,
            Text = ProfanityFilter.Sanitize(text.Trim()),
            SentAt = DateTimeOffset.UtcNow,
            IsReported = false
        };

        await chat.AddAsync(message, ct);
        await chat.SaveChangesAsync(ct);

        return new ChatMessageDto(message.Id, userId, user.DisplayName, message.Text, message.SentAt, false);
    }

    public async Task<IReadOnlyList<ChatMessageDto>> GetHistoryAsync(Guid roomId, CancellationToken ct = default)
    {
        var messages = await chat.GetByRoomIdAsync(roomId, ct);
        return messages
            .Select(m => new ChatMessageDto(m.Id, m.UserId, m.User?.DisplayName ?? string.Empty, m.Text, m.SentAt, m.IsReported))
            .ToList();
    }

    public async Task ReportMessageAsync(Guid messageId, CancellationToken ct = default)
    {
        var message = await chat.GetByIdAsync(messageId, ct)
            ?? throw new AppException("Message not found.", 404);

        message.IsReported = true;
        await chat.SaveChangesAsync(ct);
    }
}
