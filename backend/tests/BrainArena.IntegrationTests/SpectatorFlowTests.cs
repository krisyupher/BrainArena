using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using BrainArena.Application.Auth;
using BrainArena.Application.Matches;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;

namespace BrainArena.IntegrationTests;

/// <summary>
/// Phase 5: anonymous visitors can browse public rooms and watch a live match without an account,
/// but private rooms stay hidden and only actual participants can submit answers or start a match.
/// </summary>
public class SpectatorFlowTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public async Task AnonymousVisitor_CanBrowseAndReadChatForAPublicRoom_ButNotForAPrivateRoom()
    {
        var client = factory.CreateClient();
        var host = await RegisterAsync(client, "spechost");

        var publicRoom = await CreateRoomAsync(client, host.Token, "Public Spectator Room", isPrivate: false);
        var privateRoom = await CreateRoomAsync(client, host.Token, "Private Spectator Room", isPrivate: true);

        var anonymous = factory.CreateClient(); // no Authorization header at all

        var lobby = await anonymous.GetAsync("/api/rooms");
        Assert.Equal(HttpStatusCode.OK, lobby.StatusCode);
        var openRooms = await lobby.Content.ReadFromJsonAsync<List<JsonElement>>(JsonOptions);
        Assert.Contains(openRooms!, r => r.GetProperty("id").GetGuid() == publicRoom);
        Assert.DoesNotContain(openRooms!, r => r.GetProperty("id").GetGuid() == privateRoom);

        var publicDetail = await anonymous.GetAsync($"/api/rooms/{publicRoom}");
        Assert.Equal(HttpStatusCode.OK, publicDetail.StatusCode);

        var publicChat = await anonymous.GetAsync($"/api/rooms/{publicRoom}/chat");
        Assert.Equal(HttpStatusCode.OK, publicChat.StatusCode);

        var privateDetail = await anonymous.GetAsync($"/api/rooms/{privateRoom}");
        Assert.Equal(HttpStatusCode.NotFound, privateDetail.StatusCode);

        var privateChat = await anonymous.GetAsync($"/api/rooms/{privateRoom}/chat");
        Assert.Equal(HttpStatusCode.NotFound, privateChat.StatusCode);
    }

    [Fact]
    public async Task AnonymousSpectator_JoiningMidMatch_IsSyncedAndSeesLiveBroadcasts_ButCannotAnswer()
    {
        var client = factory.CreateClient();

        var host = await RegisterAsync(client, "specflowhost");
        var second = await RegisterAsync(client, "specflowsecond");

        var createResponse = await PostAsync(client, host.Token, "/api/rooms", new
        {
            name = "Spectator Flow Room",
            topic = "Math",
            maxPlayers = 2,
            minPlayersToStart = 2,
            questionCount = 5,
            secondsPerQuestion = 10,
            isPrivate = false
        });
        createResponse.EnsureSuccessStatusCode();
        var room = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var roomId = room.GetProperty("id").GetGuid();

        await using var hostConn = BuildHubConnection(host.Token);
        var latestQuestionTcs = new TaskCompletionSource<QuestionClientPayload>();
        var matchEndedTcs = new TaskCompletionSource<MatchEndedPayload>();
        hostConn.On<QuestionClientPayload>("QuestionStarted", q => latestQuestionTcs.TrySetResult(q));
        hostConn.On<MatchEndedPayload>("MatchEnded", m => matchEndedTcs.TrySetResult(m));
        await hostConn.StartAsync();
        await hostConn.InvokeAsync("JoinRoomGroup", roomId);

        await JoinRoomAsync(client, second.Token, roomId); // fills the room -> auto-starts

        // Wait for the first live question before the spectator connects, so JoinAsSpectator's
        // mid-match resync path (MatchSpectatorSync) is what actually gets exercised here.
        var firstQuestion = await latestQuestionTcs.Task.WaitAsync(TimeSpan.FromSeconds(15));

        await using var spectatorConn = BuildAnonymousHubConnection();
        var spectatorQuestions = new List<QuestionClientPayload>();
        var spectatorMatchEndedTcs = new TaskCompletionSource<MatchEndedPayload>();
        var syncTcs = new TaskCompletionSource<MatchSpectatorSyncPayload>();
        spectatorConn.On<QuestionClientPayload>("QuestionStarted", q => spectatorQuestions.Add(q));
        spectatorConn.On<MatchEndedPayload>("MatchEnded", m => spectatorMatchEndedTcs.TrySetResult(m));
        spectatorConn.On<MatchSpectatorSyncPayload>("MatchSpectatorSync", s => syncTcs.TrySetResult(s));
        await spectatorConn.StartAsync();
        await spectatorConn.InvokeAsync("JoinAsSpectator", roomId);

        var sync = await syncTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("Question", sync.Phase);
        Assert.NotNull(sync.CurrentQuestion);
        Assert.Null(sync.CurrentReveal);
        Assert.Equal(firstQuestion.MatchQuestionId, sync.CurrentQuestion!.MatchQuestionId);

        // A spectator has no player row for this match — [Authorize] lets the call through
        // (they're not required to be a room member for auth purposes) but SubmitAnswerAsync
        // itself rejects a non-participant. Either way it must not be accepted as an answer.
        await Assert.ThrowsAsync<HubException>(
            () => spectatorConn.InvokeAsync("SubmitAnswer", roomId, sync.CurrentQuestion!.MatchQuestionId, 0, null));

        var deadline = DateTime.UtcNow.AddSeconds(90);
        while (!matchEndedTcs.Task.IsCompleted && DateTime.UtcNow < deadline)
        {
            await Task.Delay(200);
        }
        Assert.True(matchEndedTcs.Task.IsCompletedSuccessfully, "The match did not finish within the expected time.");

        var spectatorEnded = await spectatorMatchEndedTcs.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(5, spectatorEnded.Review.Count);
        Assert.True(spectatorQuestions.Count >= 1, "Spectator should have received at least one live QuestionStarted broadcast.");
    }

    [Fact]
    public async Task AnonymousSpectator_CannotJoinAPrivateRoom()
    {
        var client = factory.CreateClient();
        var host = await RegisterAsync(client, "privspechost");
        var privateRoomId = await CreateRoomAsync(client, host.Token, "Private Room For Hub Test", isPrivate: true);

        await using var spectatorConn = BuildAnonymousHubConnection();
        await spectatorConn.StartAsync();

        await Assert.ThrowsAsync<HubException>(() => spectatorConn.InvokeAsync("JoinAsSpectator", privateRoomId));
    }

    private async Task<Guid> CreateRoomAsync(HttpClient client, string token, string name, bool isPrivate)
    {
        var response = await PostAsync(client, token, "/api/rooms", new
        {
            name,
            topic = "Geography",
            maxPlayers = 3,
            minPlayersToStart = 2,
            questionCount = 5,
            secondsPerQuestion = 10,
            isPrivate
        });
        response.EnsureSuccessStatusCode();
        var room = await response.Content.ReadFromJsonAsync<JsonElement>();
        return room.GetProperty("id").GetGuid();
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

    private HubConnection BuildAnonymousHubConnection()
    {
        var uri = new Uri(factory.Server.BaseAddress, "hubs/room");
        return new HubConnectionBuilder()
            .WithUrl(uri, options =>
            {
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
                // No AccessTokenProvider — this connection never sends an access_token at all.
            })
            .Build();
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

    private static async Task JoinRoomAsync(HttpClient client, string token, Guid roomId)
    {
        var response = await PostAsync(client, token, $"/api/rooms/{roomId}/join");
        response.EnsureSuccessStatusCode();
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
}
