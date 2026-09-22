using BrainArena.Application.Chat;
using BrainArena.Application.Common;
using BrainArena.Application.Tests.Fakes;
using BrainArena.Domain.Entities;
using BrainArena.Domain.Enums;

namespace BrainArena.Application.Tests.Chat;

public class ChatServiceTests
{
    private static (ChatService Service, FakeChatRepository Chat, FakeRoomRepository Rooms, FakeUserRepository Users) BuildService(
        BrainArena.Application.Abstractions.IChatRateLimiter? rateLimiter = null)
    {
        var chat = new FakeChatRepository();
        var rooms = new FakeRoomRepository();
        var users = new FakeUserRepository();
        var service = new ChatService(chat, rooms, users, rateLimiter ?? new AlwaysAllowRateLimiter());
        return (service, chat, rooms, users);
    }

    private static Room BuildRoom(RoomStatus status, params Guid[] playerIds) => BuildRoom(status, isPrivate: false, playerIds);

    private static Room BuildRoom(RoomStatus status, bool isPrivate, params Guid[] playerIds)
    {
        var room = new Room
        {
            Id = Guid.NewGuid(),
            Name = "Test Room",
            Topic = RoomTopic.Math,
            MaxPlayers = 6,
            MinPlayersToStart = 2,
            QuestionCount = 10,
            SecondsPerQuestion = 20,
            Status = status,
            IsPrivate = isPrivate,
            HostUserId = playerIds[0],
            CreatedAt = DateTimeOffset.UtcNow
        };

        foreach (var id in playerIds)
        {
            room.Players.Add(new RoomPlayer { RoomId = room.Id, UserId = id, JoinedAt = DateTimeOffset.UtcNow });
        }

        return room;
    }

    [Fact]
    public async Task SendMessageAsync_SucceedsForAMemberOfAWaitingRoom()
    {
        var (service, _, rooms, users) = BuildService();
        var player = users.Seed("Alex");
        var room = BuildRoom(RoomStatus.Waiting, player.Id);
        await rooms.AddAsync(room);

        var message = await service.SendMessageAsync(room.Id, player.Id, "Hello everyone!");

        Assert.Equal("Hello everyone!", message.Text);
        Assert.Equal("Alex", message.DisplayName);
    }

    [Fact]
    public async Task SendMessageAsync_SucceedsForAMemberOfAFinishedRoom()
    {
        var (service, _, rooms, users) = BuildService();
        var player = users.Seed();
        var room = BuildRoom(RoomStatus.Finished, player.Id);
        await rooms.AddAsync(room);

        var exception = await Record.ExceptionAsync(() => service.SendMessageAsync(room.Id, player.Id, "GG!"));

        Assert.Null(exception);
    }

    [Fact]
    public async Task SendMessageAsync_SucceedsWhileAMatchIsInProgress()
    {
        // Product decision: chat is no longer disabled during questions — fairness only
        // restricts *players'* answers, never chat, for anyone signed in.
        var (service, _, rooms, users) = BuildService();
        var player = users.Seed();
        var room = BuildRoom(RoomStatus.InProgress, player.Id);
        await rooms.AddAsync(room);

        var exception = await Record.ExceptionAsync(() => service.SendMessageAsync(room.Id, player.Id, "go team!"));

        Assert.Null(exception);
    }

    [Fact]
    public async Task SendMessageAsync_SucceedsForANonMemberOfAPublicRoom()
    {
        // Spectators (signed in, but never joined as a player) can chat in any public room.
        var (service, _, rooms, users) = BuildService();
        var player = users.Seed();
        var spectator = users.Seed();
        var room = BuildRoom(RoomStatus.Waiting, player.Id);
        await rooms.AddAsync(room);

        var exception = await Record.ExceptionAsync(() => service.SendMessageAsync(room.Id, spectator.Id, "good luck!"));

        Assert.Null(exception);
    }

    [Fact]
    public async Task SendMessageAsync_ThrowsForANonMemberOfAPrivateRoom()
    {
        var (service, _, rooms, users) = BuildService();
        var player = users.Seed();
        var outsider = users.Seed();
        var room = BuildRoom(RoomStatus.Waiting, isPrivate: true, player.Id);
        await rooms.AddAsync(room);

        var exception = await Assert.ThrowsAsync<AppException>(
            () => service.SendMessageAsync(room.Id, outsider.Id, "hi"));

        Assert.Equal(404, exception.StatusCode);
    }

    [Fact]
    public async Task SendMessageAsync_ThrowsWhenRateLimited()
    {
        var (service, _, rooms, users) = BuildService(new ChatRateLimiter());
        var player = users.Seed();
        var room = BuildRoom(RoomStatus.Waiting, player.Id);
        await rooms.AddAsync(room);

        await service.SendMessageAsync(room.Id, player.Id, "first message");

        var exception = await Assert.ThrowsAsync<AppException>(
            () => service.SendMessageAsync(room.Id, player.Id, "second message immediately after"));

        Assert.Equal(429, exception.StatusCode);
    }

    [Fact]
    public async Task SendMessageAsync_CensorsProfanityBeforeStoring()
    {
        var (service, _, rooms, users) = BuildService();
        var player = users.Seed();
        var room = BuildRoom(RoomStatus.Waiting, player.Id);
        await rooms.AddAsync(room);

        var message = await service.SendMessageAsync(room.Id, player.Id, "you are an idiot");

        Assert.DoesNotContain("idiot", message.Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetHistoryAsync_ReturnsMessagesForTheRoomInOrder()
    {
        var (service, _, rooms, users) = BuildService();
        var player = users.Seed();
        var room = BuildRoom(RoomStatus.Waiting, player.Id);
        await rooms.AddAsync(room);

        await service.SendMessageAsync(room.Id, player.Id, "first");
        await service.SendMessageAsync(room.Id, player.Id, "second"); // not rate-limited: AlwaysAllowRateLimiter

        var history = await service.GetHistoryAsync(room.Id);

        Assert.Equal(["first", "second"], history.Select(m => m.Text));
    }

    [Fact]
    public async Task ReportMessageAsync_MarksTheMessageAsReported()
    {
        var (service, _, rooms, users) = BuildService();
        var player = users.Seed();
        var room = BuildRoom(RoomStatus.Waiting, player.Id);
        await rooms.AddAsync(room);
        var message = await service.SendMessageAsync(room.Id, player.Id, "borderline message");

        await service.ReportMessageAsync(message.Id);

        var history = await service.GetHistoryAsync(room.Id);
        Assert.True(history.Single().IsReported);
    }

    [Fact]
    public async Task ReportMessageAsync_ThrowsForAnUnknownMessage()
    {
        var (service, _, _, _) = BuildService();

        var exception = await Assert.ThrowsAsync<AppException>(
            () => service.ReportMessageAsync(Guid.NewGuid()));

        Assert.Equal(404, exception.StatusCode);
    }
}
