using BrainArena.Domain.Enums;

namespace BrainArena.Application.Tournaments;

public record CreateTournamentRequest(
    string Name,
    RoomTopic Topic,
    string GameMode,
    int QuestionCount,
    int SecondsPerQuestion,
    int TournamentSize,
    int RoomSize,
    int AdvancesPerRoom,
    int MinPlayersToStart);

public record TournamentSummaryDto(
    Guid Id,
    string Name,
    RoomTopic Topic,
    string GameMode,
    int PlayerCount,
    int TournamentSize,
    TournamentStatus Status);

public record TournamentPlayerDto(
    Guid UserId,
    string DisplayName,
    TournamentPlayerStatus Status,
    int? EliminatedAtRound);

public record TournamentRoundRoomPlayerDto(Guid UserId, string DisplayName);

public record TournamentRoundRoomDto(
    Guid RoomId,
    RoomStatus RoomStatus,
    IReadOnlyList<TournamentRoundRoomPlayerDto> Players);

public record TournamentRoundDto(int RoundNumber, bool IsFinal, IReadOnlyList<TournamentRoundRoomDto> Rooms);

public record TournamentDetailDto(
    Guid Id,
    string Name,
    RoomTopic Topic,
    string GameMode,
    int QuestionCount,
    int SecondsPerQuestion,
    int TournamentSize,
    int RoomSize,
    int AdvancesPerRoom,
    int MinPlayersToStart,
    TournamentStatus Status,
    Guid CreatorUserId,
    int CurrentRoundNumber,
    Guid? ChampionUserId,
    IReadOnlyList<TournamentPlayerDto> Players,
    IReadOnlyList<TournamentRoundDto> Rounds);
