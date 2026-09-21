using BrainArena.Domain.Enums;

namespace BrainArena.Application.Rooms;

public record CreateRoomRequest(
    string Name,
    RoomTopic Topic,
    int MaxPlayers,
    int MinPlayersToStart,
    int QuestionCount,
    int SecondsPerQuestion,
    bool IsPrivate);

public record RoomSummaryDto(
    Guid Id,
    string Name,
    RoomTopic Topic,
    int PlayerCount,
    int MaxPlayers,
    RoomStatus Status,
    bool IsPrivate);

public record RoomPlayerDto(Guid UserId, string DisplayName);

public record RoomDetailDto(
    Guid Id,
    string Name,
    RoomTopic Topic,
    int MaxPlayers,
    int MinPlayersToStart,
    int QuestionCount,
    int SecondsPerQuestion,
    bool IsPrivate,
    string? ShareCode,
    RoomStatus Status,
    Guid HostUserId,
    IReadOnlyList<RoomPlayerDto> Players);
