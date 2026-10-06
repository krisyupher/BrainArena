using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using BrainArena.Application.Abstractions;
using BrainArena.Application.Auth;
using BrainArena.Application.Rooms;
using BrainArena.Domain.Entities;
using BrainArena.Domain.Enums;
using BrainArena.Infrastructure.Data;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BrainArena.IntegrationTests;

/// <summary>
/// Guest sessions (minted for anonymous Solitary practice) are practice-only: everything shared
/// with real players is refused. That containment is also what lets IGuestCleanup delete a guest
/// wholesale once its token has expired — verified against the real Postgres FK graph here.
/// </summary>
public class GuestAccountTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public async Task GuestSession_IsRefusedEverythingSharedWithRealPlayers()
    {
        var hostClient = factory.CreateClient();
        var host = await RegisterAsync(hostClient, "guesttesthost");
        hostClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", host.Token);
        var createResponse = await hostClient.PostAsJsonAsync("/api/rooms", MultiplayerRoomRequest(), JsonOptions);
        createResponse.EnsureSuccessStatusCode();
        var roomId = (await createResponse.Content.ReadFromJsonAsync<CreateRoomResult>(JsonOptions))!.Room.Id;

        var (_, guestToken) = await SeedGuestAsync(DateTimeOffset.UtcNow);
        var guestClient = factory.CreateClient();
        guestClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", guestToken);

        var createMultiplayer = await guestClient.PostAsJsonAsync("/api/rooms", MultiplayerRoomRequest(), JsonOptions);
        Assert.Equal(HttpStatusCode.Forbidden, createMultiplayer.StatusCode);

        var join = await guestClient.PostAsync($"/api/rooms/{roomId}/join", null);
        Assert.Equal(HttpStatusCode.Forbidden, join.StatusCode);

        var createTournament = await guestClient.PostAsJsonAsync("/api/tournaments", new
        {
            name = "Guest Tournament",
            topic = "Math",
            gameMode = "calculation",
            questionCount = 5,
            secondsPerQuestion = 10,
            tournamentSize = 4,
            roomSize = 2,
            advancesPerRoom = 1,
            minPlayersToStart = 4
        }, JsonOptions);
        Assert.Equal(HttpStatusCode.Forbidden, createTournament.StatusCode);

        await using var conn = BuildHubConnection(guestToken);
        await conn.StartAsync();
        await Assert.ThrowsAsync<HubException>(() => conn.InvokeAsync("SendChatMessage", roomId, "hello from a guest"));
    }

    [Fact]
    public async Task Cleanup_DeletesExpiredSelfContainedGuests_AndLeavesEverythingElseAlone()
    {
        var longAgo = DateTimeOffset.UtcNow.AddDays(-30);

        var (expiredGuest, _) = await SeedGuestAsync(longAgo);
        var (expiredRoom, generatedQuestion) = await SeedFinishedPracticeRoomAsync(expiredGuest);

        var (recentGuest, _) = await SeedGuestAsync(DateTimeOffset.UtcNow);
        var (recentRoom, _) = await SeedFinishedPracticeRoomAsync(recentGuest);

        var registered = await SeedUserAsync(UserRole.Player, longAgo);
        var (registeredRoom, _) = await SeedFinishedPracticeRoomAsync(registered);

        // An expired guest who played in a real player's room: deleting it would rewrite that history.
        var (entangledGuest, _) = await SeedGuestAsync(longAgo);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BrainArenaDbContext>();
            db.RoomPlayers.Add(new RoomPlayer { RoomId = registeredRoom, UserId = entangledGuest, JoinedAt = longAgo });
            await db.SaveChangesAsync();
        }

        int deleted;
        using (var scope = factory.Services.CreateScope())
        {
            var cleanup = scope.ServiceProvider.GetRequiredService<IGuestCleanup>();
            deleted = await cleanup.DeleteGuestsCreatedBeforeAsync(DateTimeOffset.UtcNow.AddDays(-8), batchSize: 500);
        }

        Assert.Equal(1, deleted);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BrainArenaDbContext>();

            Assert.False(await db.Users.AnyAsync(u => u.Id == expiredGuest));
            Assert.False(await db.Rooms.AnyAsync(r => r.Id == expiredRoom));
            Assert.False(await db.Matches.AnyAsync(m => m.RoomId == expiredRoom));
            Assert.False(await db.ChatMessages.AnyAsync(m => m.RoomId == expiredRoom));
            Assert.False(await db.Questions.AnyAsync(q => q.Id == generatedQuestion));

            Assert.True(await db.Users.AnyAsync(u => u.Id == recentGuest));
            Assert.True(await db.Rooms.AnyAsync(r => r.Id == recentRoom));
            Assert.True(await db.Users.AnyAsync(u => u.Id == entangledGuest));
            Assert.True(await db.Users.AnyAsync(u => u.Id == registered));
            Assert.True(await db.Rooms.AnyAsync(r => r.Id == registeredRoom));
        }
    }

    private async Task<(Guid UserId, string Token)> SeedGuestAsync(DateTimeOffset createdAt)
    {
        var userId = await SeedUserAsync(UserRole.Guest, createdAt);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BrainArenaDbContext>();
        var user = await db.Users.SingleAsync(u => u.Id == userId);
        var token = scope.ServiceProvider.GetRequiredService<IJwtTokenService>().GenerateToken(user);
        return (userId, token);
    }

    private async Task<Guid> SeedUserAsync(UserRole role, DateTimeOffset createdAt)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BrainArenaDbContext>();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = role == UserRole.Guest
                ? $"guest-{Guid.NewGuid():N}@guest.brainarena.local"
                : $"user-{Guid.NewGuid():N}@example.com",
            DisplayName = role == UserRole.Guest ? "Guest" : "Real Player",
            PasswordHash = "unused",
            Role = role,
            CreatedAt = createdAt
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    /// <summary>A finished practice room with the full row graph a real match leaves behind.</summary>
    private async Task<(Guid RoomId, Guid GeneratedQuestionId)> SeedFinishedPracticeRoomAsync(Guid hostUserId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BrainArenaDbContext>();

        var room = new Room
        {
            Id = Guid.NewGuid(),
            Name = "Practice: Flash",
            Topic = RoomTopic.Math,
            MaxPlayers = 1,
            MinPlayersToStart = 1,
            QuestionCount = 5,
            SecondsPerQuestion = 10,
            GameMode = "flash-arithmetic",
            Kind = RoomKind.Solitary,
            Status = RoomStatus.Finished,
            HostUserId = hostUserId,
            CreatedAt = DateTimeOffset.UtcNow
        };
        room.Players.Add(new RoomPlayer { RoomId = room.Id, UserId = hostUserId, JoinedAt = DateTimeOffset.UtcNow });

        var question = new Question
        {
            Id = Guid.NewGuid(),
            Topic = RoomTopic.Math,
            Type = QuestionType.FlashArithmetic,
            Text = "4,5",
            CorrectNumericAnswer = 9,
            Explanation = "4 + 5 = 9"
        };
        var match = new Match { Id = Guid.NewGuid(), RoomId = room.Id, Status = MatchStatus.Finished, CreatedAt = DateTimeOffset.UtcNow };
        var matchQuestion = new MatchQuestion { Id = Guid.NewGuid(), MatchId = match.Id, Question = question, OrderIndex = 0 };

        db.Rooms.Add(room);
        db.Matches.Add(match);
        db.MatchQuestions.Add(matchQuestion);
        db.MatchPlayers.Add(new MatchPlayer { MatchId = match.Id, UserId = hostUserId, Score = 150, FinalRank = 1 });
        db.MatchAnswers.Add(new MatchAnswer
        {
            MatchQuestionId = matchQuestion.Id,
            UserId = hostUserId,
            NumericAnswer = 9,
            PointsAwarded = 150,
            AnsweredAt = DateTimeOffset.UtcNow
        });
        db.ChatMessages.Add(new ChatMessage { Id = Guid.NewGuid(), RoomId = room.Id, UserId = hostUserId, Text = "practice", SentAt = DateTimeOffset.UtcNow });

        await db.SaveChangesAsync();
        return (room.Id, question.Id);
    }

    private static object MultiplayerRoomRequest() => new
    {
        name = "Guest Test Room",
        topic = "Math",
        maxPlayers = 3,
        minPlayersToStart = 2,
        questionCount = 5,
        secondsPerQuestion = 10,
        isPrivate = false,
        gameMode = "calculation",
        kind = "Multiplayer"
    };

    private static async Task<AuthResponse> RegisterAsync(HttpClient client, string namePrefix)
    {
        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(
            Email: $"{namePrefix}-{Guid.NewGuid():N}@example.com",
            Password: "P@ssw0rd123",
            DisplayName: namePrefix));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions))!;
    }

    private HubConnection BuildHubConnection(string token)
    {
        var uri = new Uri(factory.Server.BaseAddress, "hubs/room");
        return new HubConnectionBuilder()
            .WithUrl(uri, options =>
            {
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
                options.AccessTokenProvider = () => Task.FromResult<string?>(token);
            })
            .Build();
    }
}
