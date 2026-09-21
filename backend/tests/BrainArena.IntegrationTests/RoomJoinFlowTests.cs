using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using BrainArena.Application.Auth;
using BrainArena.Application.Rooms;
using BrainArena.Domain.Enums;

namespace BrainArena.IntegrationTests;

/// <summary>
/// Simulates three players registering, one creating a room and the other two joining it —
/// the Phase 1 slice of the brief's "3 players join a room and finish a match" scenario.
/// The match-play/finish portion is added once Phase 2 (match flow) exists.
/// </summary>
public class RoomJoinFlowTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    // Must mirror the server's JsonStringEnumConverter (configured in Program.cs) — the
    // System.Net.Http.Json extension methods don't pick up ASP.NET Core's MVC JSON options.
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public async Task ThreePlayers_CanRegisterCreateAndFillARoom()
    {
        var client = factory.CreateClient();

        var host = await RegisterAsync(client, "host");
        var second = await RegisterAsync(client, "second");
        var third = await RegisterAsync(client, "third");

        var createResponse = await PostAsync(client, host.Token, "/api/rooms", new CreateRoomRequest(
            Name: "Trivia Night",
            Topic: RoomTopic.Geography,
            MaxPlayers: 3,
            MinPlayersToStart: 2,
            QuestionCount: 10,
            SecondsPerQuestion: 20,
            IsPrivate: false));

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var room = (await createResponse.Content.ReadFromJsonAsync<RoomDetailDto>(JsonOptions))!;
        Assert.Single(room.Players);

        await JoinAsync(client, second.Token, room.Id);
        var finalRoom = await JoinAsync(client, third.Token, room.Id);

        Assert.Equal(3, finalRoom.Players.Count);
        Assert.Equal(RoomStatus.Waiting, finalRoom.Status);
        Assert.Contains(finalRoom.Players, p => p.UserId == host.UserId);
        Assert.Contains(finalRoom.Players, p => p.UserId == second.UserId);
        Assert.Contains(finalRoom.Players, p => p.UserId == third.UserId);

        var lobbyResponse = await GetAsync(client, host.Token, "/api/rooms");
        var openRooms = (await lobbyResponse.Content.ReadFromJsonAsync<List<RoomSummaryDto>>(JsonOptions))!;
        var listedRoom = Assert.Single(openRooms, r => r.Id == room.Id);
        Assert.Equal(3, listedRoom.PlayerCount);
    }

    [Fact]
    public async Task JoinRoomAsync_RejectsAFourthPlayerOnceTheRoomIsFull()
    {
        var client = factory.CreateClient();

        var host = await RegisterAsync(client, "fullhost");
        var second = await RegisterAsync(client, "fullsecond");
        var third = await RegisterAsync(client, "fullthird");
        var fourth = await RegisterAsync(client, "fullfourth");

        var createResponse = await PostAsync(client, host.Token, "/api/rooms", new CreateRoomRequest(
            Name: "Full House",
            Topic: RoomTopic.Chemistry,
            MaxPlayers: 3,
            MinPlayersToStart: 2,
            QuestionCount: 5,
            SecondsPerQuestion: 15,
            IsPrivate: false));
        var room = (await createResponse.Content.ReadFromJsonAsync<RoomDetailDto>(JsonOptions))!;

        await JoinAsync(client, second.Token, room.Id);
        await JoinAsync(client, third.Token, room.Id);

        var rejected = await PostAsync(client, fourth.Token, $"/api/rooms/{room.Id}/join");

        Assert.Equal(HttpStatusCode.Conflict, rejected.StatusCode);
    }

    private static async Task<AuthResponse> RegisterAsync(HttpClient client, string namePrefix)
    {
        var email = $"{namePrefix}-{Guid.NewGuid():N}@example.com";
        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(
            Email: email,
            Password: "P@ssw0rd123",
            DisplayName: namePrefix));

        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions))!;
    }

    private static async Task<RoomDetailDto> JoinAsync(HttpClient client, string token, Guid roomId)
    {
        var response = await PostAsync(client, token, $"/api/rooms/{roomId}/join");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<RoomDetailDto>(JsonOptions))!;
    }

    private static Task<HttpResponseMessage> PostAsync<TBody>(HttpClient client, string token, string url, TBody body)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client.PostAsJsonAsync(url, body, JsonOptions);
    }

    private static Task<HttpResponseMessage> PostAsync(HttpClient client, string token, string url)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client.PostAsync(url, null);
    }

    private static Task<HttpResponseMessage> GetAsync(HttpClient client, string token, string url)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client.GetAsync(url);
    }
}
