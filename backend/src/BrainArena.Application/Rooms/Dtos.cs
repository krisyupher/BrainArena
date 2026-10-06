using BrainArena.Application.Auth;
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
    RoomKind Kind = RoomKind.Multiplayer,
    Difficulty Difficulty = Difficulty.Medium);

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
    RoomKind Kind,
    Difficulty Difficulty);

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
    Difficulty Difficulty,
    IReadOnlyList<RoomPlayerDto> Players);

/// <summary>
/// GuestAuth is non-null only when CreateRoomAsync auto-created a throwaway guest account for an
/// anonymous Solitary caller — the frontend applies it as the caller's new session and reconnects
/// the hub before navigating. Null for every other (already-authenticated) create.
/// </summary>
public record CreateRoomResult(RoomDetailDto Room, AuthResponse? GuestAuth);
