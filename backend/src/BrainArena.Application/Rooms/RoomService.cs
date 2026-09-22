using BrainArena.Application.Abstractions;
using BrainArena.Application.Common;
using BrainArena.Application.Matches;
using BrainArena.Domain.Entities;
using BrainArena.Domain.Enums;

namespace BrainArena.Application.Rooms;

public class RoomService(
    IRoomRepository rooms,
    IUserRepository users,
    IRoomNotifier notifier,
    IMatchOrchestrator matchOrchestrator,
    IGameModeRegistry gameModeRegistry) : IRoomService
{
    public async Task<IReadOnlyList<RoomSummaryDto>> GetOpenRoomsAsync(CancellationToken ct = default)
    {
        var openRooms = await rooms.GetOpenRoomsAsync(ct);
        return openRooms
            .OrderBy(r => r.Status == RoomStatus.Waiting ? 0 : 1)
            .ThenByDescending(r => r.CreatedAt)
            .Select(MapSummary)
            .ToList();
    }

    public async Task<RoomDetailDto> CreateRoomAsync(Guid hostUserId, CreateRoomRequest request, CancellationToken ct = default)
    {
        RoomValidation.Validate(request, gameModeRegistry.ModeKeys);

        var host = await users.GetByIdAsync(hostUserId, ct)
            ?? throw new AppException("Host user not found.", 404);

        var room = new Room
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Topic = request.Topic,
            MaxPlayers = request.MaxPlayers,
            MinPlayersToStart = request.MinPlayersToStart,
            QuestionCount = request.QuestionCount,
            SecondsPerQuestion = request.SecondsPerQuestion,
            IsPrivate = request.IsPrivate,
            GameMode = request.GameMode,
            Status = RoomStatus.Waiting,
            HostUserId = hostUserId,
            CreatedAt = DateTimeOffset.UtcNow
        };

        if (request.IsPrivate)
        {
            room.ShareCode = await GenerateUniqueShareCodeAsync(ct);
        }

        room.Players.Add(new RoomPlayer
        {
            RoomId = room.Id,
            UserId = hostUserId,
            JoinedAt = DateTimeOffset.UtcNow,
            User = host
        });

        await rooms.AddAsync(room, ct);
        await rooms.SaveChangesAsync(ct);
        await notifier.NotifyRoomListChangedAsync(ct);

        return MapDetail(room);
    }

    public async Task<RoomDetailDto> JoinRoomAsync(Guid userId, Guid roomId, CancellationToken ct = default)
    {
        var room = await rooms.GetByIdAsync(roomId, ct)
            ?? throw new AppException("Room not found.", 404);

        if (room.Players.Any(p => p.UserId == userId))
        {
            return MapDetail(room);
        }

        if (room.Status != RoomStatus.Waiting)
            throw new AppException("This room has already started or finished.", 409);

        if (room.Players.Count >= room.MaxPlayers)
            throw new AppException("This room is full.", 409);

        var user = await users.GetByIdAsync(userId, ct)
            ?? throw new AppException("User not found.", 404);

        room.Players.Add(new RoomPlayer
        {
            RoomId = room.Id,
            UserId = userId,
            JoinedAt = DateTimeOffset.UtcNow,
            User = user
        });

        await rooms.SaveChangesAsync(ct);
        await notifier.NotifyRoomListChangedAsync(ct);
        await notifier.NotifyRoomUpdatedAsync(room.Id, ct);
        await matchOrchestrator.TryAutoStartAsync(room.Id, ct);

        return MapDetail(room);
    }

    public async Task LeaveRoomAsync(Guid userId, Guid roomId, CancellationToken ct = default)
    {
        var room = await rooms.GetByIdAsync(roomId, ct)
            ?? throw new AppException("Room not found.", 404);

        if (room.Status != RoomStatus.Waiting)
        {
            // Once a match has started, leaving the waiting room no longer applies — the
            // in-progress match tracks connection state itself instead.
            return;
        }

        var player = room.Players.FirstOrDefault(p => p.UserId == userId);
        if (player is null)
        {
            return;
        }

        room.Players.Remove(player);

        if (room.HostUserId == userId)
        {
            var nextHost = room.Players.OrderBy(p => p.JoinedAt).FirstOrDefault();
            if (nextHost is not null)
            {
                room.HostUserId = nextHost.UserId;
            }
        }

        await rooms.SaveChangesAsync(ct);
        await notifier.NotifyRoomListChangedAsync(ct);
        await notifier.NotifyRoomUpdatedAsync(room.Id, ct);
    }

    public async Task<RoomDetailDto> GetRoomByShareCodeAsync(string shareCode, CancellationToken ct = default)
    {
        var room = await rooms.GetByShareCodeAsync(shareCode.Trim().ToUpperInvariant(), ct)
            ?? throw new AppException("No room found for that code.", 404);

        return MapDetail(room);
    }

    public async Task<RoomDetailDto> GetRoomDetailAsync(Guid roomId, CancellationToken ct = default)
    {
        var room = await rooms.GetByIdAsync(roomId, ct)
            ?? throw new AppException("Room not found.", 404);

        return MapDetail(room);
    }

    public async Task<RoomDetailDto> GetVisibleRoomDetailAsync(Guid roomId, Guid? requestingUserId, CancellationToken ct = default)
    {
        var room = await rooms.GetByIdAsync(roomId, ct)
            ?? throw new AppException("Room not found.", 404);

        var isMember = requestingUserId is not null && room.Players.Any(p => p.UserId == requestingUserId.Value);
        if (room.IsPrivate && !isMember)
        {
            // Same message/status as "doesn't exist" — a private room shouldn't confirm its own
            // existence to a non-member.
            throw new AppException("Room not found.", 404);
        }

        return MapDetail(room);
    }

    private async Task<string> GenerateUniqueShareCodeAsync(CancellationToken ct)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var code = ShareCodeGenerator.Generate();
            if (await rooms.GetByShareCodeAsync(code, ct) is null)
                return code;
        }
        throw new AppException("Could not generate a unique share code, please try again.", 500);
    }

    private static RoomSummaryDto MapSummary(Room room) => new(
        room.Id, room.Name, room.Topic, room.Players.Count, room.MaxPlayers, room.Status, room.IsPrivate);

    private static RoomDetailDto MapDetail(Room room) => new(
        room.Id, room.Name, room.Topic, room.MaxPlayers, room.MinPlayersToStart,
        room.QuestionCount, room.SecondsPerQuestion, room.IsPrivate, room.ShareCode,
        room.Status, room.HostUserId,
        room.Players
            .Select(p => new RoomPlayerDto(p.UserId, p.User?.DisplayName ?? string.Empty))
            .ToList());
}
