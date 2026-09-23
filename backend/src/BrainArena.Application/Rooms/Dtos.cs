using BrainArena.Application.Matches;
using BrainArena.Domain.Enums;

namespace BrainArena.Application.Rooms;

public record CreateRoomRequest(
    string Name,
    RoomTopic Topic,
    int MaxPlayers,
    int MinPlayersToStart,
    int QuestionCount,
    int SecondsPerQuestion,
    bool IsPrivate,
    string GameMode = MultipleChoiceGameMode.Key,
    RoomKind Kind = RoomKind.Multiplayer);

public record RoomSummaryDto(
    Guid Id,
    string Name,
    RoomTopic Topic,
    string GameMode,
    int QuestionCount,
    int SecondsPerQuestion,
    int PlayerCount,
    int MaxPlayers,
    RoomStatus Status,
    bool IsPrivate,
    RoomKind Kind);

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
    RoomKind Kind,
    IReadOnlyList<RoomPlayerDto> Players);
