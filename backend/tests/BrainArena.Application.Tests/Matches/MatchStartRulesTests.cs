using BrainArena.Application.Common;
using BrainArena.Application.Matches;
using BrainArena.Domain.Entities;
using BrainArena.Domain.Enums;

namespace BrainArena.Application.Tests.Matches;

public class MatchStartRulesTests
{
    private static Room BuildRoom(int maxPlayers, int minPlayersToStart, int playerCount, RoomStatus status = RoomStatus.Waiting)
    {
        var hostId = Guid.NewGuid();
        var room = new Room
        {
            Id = Guid.NewGuid(),
            Name = "Test Room",
            Topic = RoomTopic.Math,
            MaxPlayers = maxPlayers,
            MinPlayersToStart = minPlayersToStart,
            QuestionCount = 10,
            SecondsPerQuestion = 20,
            Status = status,
            HostUserId = hostId,
            CreatedAt = DateTimeOffset.UtcNow
        };

        for (var i = 0; i < playerCount; i++)
        {
            room.Players.Add(new RoomPlayer
            {
                RoomId = room.Id,
                UserId = i == 0 ? hostId : Guid.NewGuid(),
                JoinedAt = DateTimeOffset.UtcNow
            });
        }

        return room;
    }

    [Fact]
    public void CanAutoStart_TrueWhenRoomIsFullAndWaiting()
    {
        var room = BuildRoom(maxPlayers: 4, minPlayersToStart: 2, playerCount: 4);

        Assert.True(MatchStartRules.CanAutoStart(room));
    }

    [Fact]
    public void CanAutoStart_FalseWhenRoomIsNotFull()
    {
        var room = BuildRoom(maxPlayers: 4, minPlayersToStart: 2, playerCount: 3);

        Assert.False(MatchStartRules.CanAutoStart(room));
    }

    [Fact]
    public void CanAutoStart_FalseWhenRoomAlreadyInProgress()
    {
        var room = BuildRoom(maxPlayers: 4, minPlayersToStart: 2, playerCount: 4, status: RoomStatus.InProgress);

        Assert.False(MatchStartRules.CanAutoStart(room));
    }

    [Fact]
    public void EnsureCanStartNow_AllowsHostWithEnoughPlayers()
    {
        var room = BuildRoom(maxPlayers: 6, minPlayersToStart: 2, playerCount: 2);

        var exception = Record.Exception(() => MatchStartRules.EnsureCanStartNow(room, room.HostUserId));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureCanStartNow_RejectsNonHost()
    {
        var room = BuildRoom(maxPlayers: 6, minPlayersToStart: 2, playerCount: 2);
        var nonHost = room.Players.First(p => p.UserId != room.HostUserId).UserId;

        var exception = Assert.Throws<AppException>(() => MatchStartRules.EnsureCanStartNow(room, nonHost));

        Assert.Equal(403, exception.StatusCode);
    }

    [Fact]
    public void EnsureCanStartNow_RejectsTooFewPlayers()
    {
        var room = BuildRoom(maxPlayers: 6, minPlayersToStart: 3, playerCount: 2);

        var exception = Assert.Throws<AppException>(() => MatchStartRules.EnsureCanStartNow(room, room.HostUserId));

        Assert.Equal(409, exception.StatusCode);
    }

    [Fact]
    public void EnsureCanStartNow_RejectsWhenRoomAlreadyStarted()
    {
        var room = BuildRoom(maxPlayers: 6, minPlayersToStart: 2, playerCount: 2, status: RoomStatus.InProgress);

        var exception = Assert.Throws<AppException>(() => MatchStartRules.EnsureCanStartNow(room, room.HostUserId));

        Assert.Equal(409, exception.StatusCode);
    }
}
