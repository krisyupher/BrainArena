using BrainArena.Application.Common;
using BrainArena.Application.Matches;
using BrainArena.Application.Rooms;
using BrainArena.Application.Tests.Fakes;
using BrainArena.Domain.Enums;

namespace BrainArena.Application.Tests.Rooms;

public class RoomServiceTests
{
    private static CreateRoomRequest Request(int maxPlayers = 3, int minPlayersToStart = 2, bool isPrivate = false, RoomKind kind = RoomKind.Multiplayer) => new(
        Name: "Geo Trivia",
        Topic: RoomTopic.Geography,
        MaxPlayers: maxPlayers,
        MinPlayersToStart: minPlayersToStart,
        QuestionCount: 10,
        SecondsPerQuestion: 20,
        IsPrivate: isPrivate,
        Kind: kind);

    private static (RoomService Service, FakeUserRepository Users, FakeRoomRepository Rooms, FakeRoomNotifier Notifier, FakeMatchOrchestrator MatchOrchestrator) BuildService()
    {
        var users = new FakeUserRepository();
        var rooms = new FakeRoomRepository();
        var notifier = new FakeRoomNotifier();
        var matchOrchestrator = new FakeMatchOrchestrator();
        var gameModeRegistry = new GameModeRegistry([new MultipleChoiceGameMode(), new CalculationGameMode()]);
        return (new RoomService(rooms, users, notifier, matchOrchestrator, gameModeRegistry), users, rooms, notifier, matchOrchestrator);
    }

    [Fact]
    public async Task CreateRoomAsync_AddsTheHostAsTheFirstPlayer()
    {
        var (service, users, _, notifier, _) = BuildService();
        var host = users.Seed("Host Hernandez");

        var room = await service.CreateRoomAsync(host.Id, Request());

        var player = Assert.Single(room.Players);
        Assert.Equal(host.Id, player.UserId);
        Assert.Equal(host.Id, room.HostUserId);
        Assert.Equal(1, notifier.NotificationCount);
    }

    [Fact]
    public async Task CreateRoomAsync_PrivateRoomGetsAShareCode()
    {
        var (service, users, _, _, _) = BuildService();
        var host = users.Seed();

        var room = await service.CreateRoomAsync(host.Id, Request(isPrivate: true));

        Assert.False(string.IsNullOrWhiteSpace(room.ShareCode));
    }

    [Fact]
    public async Task JoinRoomAsync_AddsANewPlayerToAWaitingRoom()
    {
        var (service, users, _, _, _) = BuildService();
        var host = users.Seed();
        var joiner = users.Seed();
        var room = await service.CreateRoomAsync(host.Id, Request());

        var updated = await service.JoinRoomAsync(joiner.Id, room.Id);

        Assert.Equal(2, updated.Players.Count);
        Assert.Contains(updated.Players, p => p.UserId == joiner.Id);
    }

    [Fact]
    public async Task JoinRoomAsync_IsIdempotentForAPlayerAlreadyInTheRoom()
    {
        var (service, users, _, _, _) = BuildService();
        var host = users.Seed();
        var room = await service.CreateRoomAsync(host.Id, Request());

        var result = await service.JoinRoomAsync(host.Id, room.Id);

        Assert.Single(result.Players);
    }

    [Fact]
    public async Task JoinRoomAsync_ThrowsWhenTheRoomIsFull()
    {
        var (service, users, _, _, _) = BuildService();
        var host = users.Seed();
        var room = await service.CreateRoomAsync(host.Id, Request(maxPlayers: 2));
        var secondPlayer = users.Seed();
        await service.JoinRoomAsync(secondPlayer.Id, room.Id);

        var thirdPlayer = users.Seed();
        var exception = await Assert.ThrowsAsync<AppException>(
            () => service.JoinRoomAsync(thirdPlayer.Id, room.Id));

        Assert.Equal(409, exception.StatusCode);
    }

    [Fact]
    public async Task GetOpenRoomsAsync_OrdersWaitingRoomsBeforeInProgressRooms()
    {
        var (service, users, roomsRepo, _, _) = BuildService();
        var host = users.Seed();
        var waitingRoom = await service.CreateRoomAsync(host.Id, Request());
        var inProgressRoomHost = users.Seed();
        var inProgressRoom = await service.CreateRoomAsync(inProgressRoomHost.Id, Request());
        (await roomsRepo.GetByIdAsync(inProgressRoom.Id))!.Status = RoomStatus.InProgress;

        var summaries = await service.GetOpenRoomsAsync();

        Assert.Equal(waitingRoom.Id, summaries[0].Id);
        Assert.Equal(inProgressRoom.Id, summaries[1].Id);
    }

    [Fact]
    public async Task GetRoomByShareCodeAsync_ThrowsForAnUnknownCode()
    {
        var (service, _, _, _, _) = BuildService();

        var exception = await Assert.ThrowsAsync<AppException>(
            () => service.GetRoomByShareCodeAsync("ZZZZZZ"));

        Assert.Equal(404, exception.StatusCode);
    }

    [Fact]
    public async Task LeaveRoomAsync_NonHostLeaving_RemovesThemWithoutChangingHost()
    {
        var (service, users, _, _, _) = BuildService();
        var host = users.Seed();
        var other = users.Seed();
        var room = await service.CreateRoomAsync(host.Id, Request());
        await service.JoinRoomAsync(other.Id, room.Id);

        await service.LeaveRoomAsync(other.Id, room.Id);

        var updated = await service.GetRoomDetailAsync(room.Id);
        Assert.Single(updated.Players);
        Assert.Equal(host.Id, updated.HostUserId);
    }

    [Fact]
    public async Task LeaveRoomAsync_HostLeaving_PassesHostToTheEarliestRemainingPlayer()
    {
        var (service, users, _, _, _) = BuildService();
        var host = users.Seed();
        var second = users.Seed();
        var third = users.Seed();
        var room = await service.CreateRoomAsync(host.Id, Request(maxPlayers: 3));
        await service.JoinRoomAsync(second.Id, room.Id);
        await service.JoinRoomAsync(third.Id, room.Id);

        await service.LeaveRoomAsync(host.Id, room.Id);

        var updated = await service.GetRoomDetailAsync(room.Id);
        Assert.Equal(2, updated.Players.Count);
        Assert.Equal(second.Id, updated.HostUserId);
    }

    [Fact]
    public async Task LeaveRoomAsync_OnceTheRoomHasStarted_DoesNothing()
    {
        var (service, users, roomsRepo, _, _) = BuildService();
        var host = users.Seed();
        var second = users.Seed();
        var room = await service.CreateRoomAsync(host.Id, Request());
        await service.JoinRoomAsync(second.Id, room.Id);
        (await roomsRepo.GetByIdAsync(room.Id))!.Status = RoomStatus.InProgress;

        await service.LeaveRoomAsync(second.Id, room.Id);

        var updated = await service.GetRoomDetailAsync(room.Id);
        Assert.Equal(2, updated.Players.Count);
    }

    [Fact]
    public async Task CreateRoomAsync_ForASolitaryRoom_ForcesOnePlayerAndNeverPrivate()
    {
        var (service, users, _, _, _) = BuildService();
        var host = users.Seed();

        // A crafted payload trying to smuggle a many-player/private "solitary" room through.
        var room = await service.CreateRoomAsync(
            host.Id, Request(maxPlayers: 8, minPlayersToStart: 4, isPrivate: true, kind: RoomKind.Solitary));

        Assert.Equal(1, room.MaxPlayers);
        Assert.Equal(1, room.MinPlayersToStart);
        Assert.False(room.IsPrivate);
        Assert.Null(room.ShareCode);
        Assert.Equal(RoomKind.Solitary, room.Kind);
    }

    [Fact]
    public async Task CreateRoomAsync_ForASolitaryRoom_AttemptsAutoStart()
    {
        var (service, users, _, _, matchOrchestrator) = BuildService();
        var host = users.Seed();

        var room = await service.CreateRoomAsync(host.Id, Request(kind: RoomKind.Solitary));

        Assert.Equal(1, matchOrchestrator.AutoStartAttempts);
        Assert.Equal(room.Id, matchOrchestrator.LastAutoStartRoomId);
    }

    [Fact]
    public async Task CreateRoomAsync_ForAMultiplayerRoom_NeverAttemptsAutoStart()
    {
        var (service, users, _, _, matchOrchestrator) = BuildService();
        var host = users.Seed();

        await service.CreateRoomAsync(host.Id, Request());

        Assert.Equal(0, matchOrchestrator.AutoStartAttempts);
    }
}
