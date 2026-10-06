using BrainArena.Application.Abstractions;
using BrainArena.Application.Auth;
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
    IGameModeRegistry gameModeRegistry,
    IPasswordHasher passwordHasher,
    IJwtTokenService jwtTokenService) : IRoomService
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

    public async Task<CreateRoomResult> CreateRoomAsync(Guid? hostUserId, CreateRoomRequest request, CancellationToken ct = default)
    {
        RoomValidation.Validate(request, gameModeRegistry.ModeKeys);

        AuthResponse? guestAuth = null;
        User host;

        if (hostUserId is null)
        {
            // Anonymous caller — only allowed for a Solitary practice room (Multiplayer/Tournament
            // still require a real sign-in). Auto-create a throwaway guest account and mint it a
            // real JWT so the caller leaves this request as a completely ordinary authenticated
            // user from every other part of the app's perspective.
            if (request.Kind != RoomKind.Solitary)
            {
                throw new AppException("Sign in to create this room.", 401);
            }

            var guest = new User
            {
                Id = Guid.NewGuid(),
                Email = $"guest-{Guid.NewGuid():N}@guest.brainarena.local",
                DisplayName = "Guest",
                PasswordHash = string.Empty,
                Role = UserRole.Player,
                CreatedAt = DateTimeOffset.UtcNow
            };
            guest.PasswordHash = passwordHasher.Hash(guest, Guid.NewGuid().ToString("N"));

            await users.AddAsync(guest, ct);

            host = guest;
            hostUserId = guest.Id;
            guestAuth = new AuthResponse(jwtTokenService.GenerateToken(guest), guest.Id, guest.DisplayName, guest.Role.ToString());
        }
        else
        {
            host = await users.GetByIdAsync(hostUserId.Value, ct)
                ?? throw new AppException("Host user not found.", 404);
        }

        // A solitary practice room is always exactly 1/1 and never shareable — force these
        // server-side regardless of what the client sent, so a crafted payload can't create a
        // many-player "solitary" room.
        var isSolitary = request.Kind == RoomKind.Solitary;
        var maxPlayers = isSolitary ? 1 : request.MaxPlayers;
        var minPlayersToStart = isSolitary ? 1 : request.MinPlayersToStart;
        var isPrivate = isSolitary ? false : request.IsPrivate;

        var room = new Room
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Topic = request.Topic,
            MaxPlayers = maxPlayers,
            MinPlayersToStart = minPlayersToStart,
            QuestionCount = request.QuestionCount,
            SecondsPerQuestion = request.SecondsPerQuestion,
            IsPrivate = isPrivate,
            GameMode = request.GameMode,
            Status = RoomStatus.Waiting,
            Kind = request.Kind,
            Difficulty = request.Difficulty,
            HostUserId = hostUserId.Value,
            CreatedAt = DateTimeOffset.UtcNow
        };

        if (isPrivate)
        {
            room.ShareCode = await GenerateUniqueShareCodeAsync(ct);
        }

        room.Players.Add(new RoomPlayer
        {
            RoomId = room.Id,
            UserId = hostUserId.Value,
            JoinedAt = DateTimeOffset.UtcNow,
            User = host
        });

        await rooms.AddAsync(room, ct);
        await rooms.SaveChangesAsync(ct);
        await notifier.NotifyRoomListChangedAsync(ct);

        if (isSolitary)
        {
            // The room is already "full" (1/1) — auto-start immediately rather than waiting for a
            // second player who can never arrive. TryAutoStartAsync runs inside its own DbContext
            // scope (MatchOrchestrator is a Singleton) and, if the match actually starts, commits
            // Status = InProgress there. A plain GetByIdAsync re-fetch here would use the SAME
            // DbContext that already has this exact `room` tracked (from AddAsync/SaveChangesAsync
            // above) — EF Core's identity map would hand back that same stale instance instead of
            // reading the committed row, silently no-op'ing the "re-fetch." Use the no-tracking
            // status projection instead, and apply just that one field to the local `room` we
            // already have (safe — nothing else can touch a 1-player solitary room in this window).
            await matchOrchestrator.TryAutoStartAsync(room.Id, ct);
            room.Status = await rooms.GetStatusNoTrackingAsync(room.Id, ct) ?? room.Status;
        }

        return new CreateRoomResult(MapDetail(room), guestAuth);
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
        room.Id, room.Name, room.Topic, room.GameMode, room.QuestionCount, room.SecondsPerQuestion,
        room.Players.Count, room.MaxPlayers, room.Status, room.IsPrivate, room.Kind, room.Difficulty);

    private static RoomDetailDto MapDetail(Room room) => new(
        room.Id, room.Name, room.Topic, room.MaxPlayers, room.MinPlayersToStart,
        room.QuestionCount, room.SecondsPerQuestion, room.IsPrivate, room.ShareCode,
        room.Status, room.HostUserId, room.Kind, room.Difficulty,
        room.Players
            .Select(p => new RoomPlayerDto(p.UserId, p.User?.DisplayName ?? string.Empty))
            .ToList());
}
