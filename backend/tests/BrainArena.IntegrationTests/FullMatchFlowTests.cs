using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using BrainArena.Application.Auth;
using BrainArena.Application.Matches;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;

namespace BrainArena.IntegrationTests;

/// <summary>
/// The brief's canonical scenario: 3 players join a room and finish a match — exercising the
/// full server-authoritative loop (auto-start on the room filling, countdown, per-question
/// server-side timing/scoring including a "never answered" case, reveal broadcasts with a live
/// scoreboard, and the final results) over real HTTP + SignalR against the running Api host.
/// </summary>
public class FullMatchFlowTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public async Task ThreePlayers_JoinARoomAndFinishAMatch()
    {
        var client = factory.CreateClient();

        var host = await RegisterAsync(client, "flowhost");
        var second = await RegisterAsync(client, "flowsecond");
        var third = await RegisterAsync(client, "flowthird");

        var createResponse = await PostAsync(client, host.Token, "/api/rooms", new
        {
            name = "Full Flow Room",
            topic = "Math",
            maxPlayers = 3,
            minPlayersToStart = 2,
            questionCount = 5,
            secondsPerQuestion = 10,
            isPrivate = false
        });
        createResponse.EnsureSuccessStatusCode();
        var room = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var roomId = room.GetProperty("id").GetGuid();

        await using var hostConn = BuildHubConnection(host.Token);
        await using var secondConn = BuildHubConnection(second.Token);
        await using var thirdConn = BuildHubConnection(third.Token);

        QuestionClientPayload? latestQuestion = null;
        var matchEndedTcs = new TaskCompletionSource<MatchEndedPayload>();

        void WireEvents(HubConnection conn)
        {
            conn.On<QuestionClientPayload>("QuestionStarted", q => latestQuestion = q);
            conn.On<MatchEndedPayload>("MatchEnded", m => matchEndedTcs.TrySetResult(m));
        }

        WireEvents(hostConn);
        WireEvents(secondConn);
        WireEvents(thirdConn);

        await hostConn.StartAsync();
        await secondConn.StartAsync();
        await thirdConn.StartAsync();

        // Host is already a room member from creation; join its SignalR group directly.
        await hostConn.InvokeAsync("JoinRoomGroup", roomId);

        await JoinRoomAsync(client, second.Token, roomId);
        await secondConn.InvokeAsync("JoinRoomGroup", roomId);

        await JoinRoomAsync(client, third.Token, roomId); // fills the room 3/3 -> auto-starts the match
        await thirdConn.InvokeAsync("JoinRoomGroup", roomId);

        var answeredIndexes = new HashSet<int>();
        var deadline = DateTime.UtcNow.AddSeconds(90);

        while (!matchEndedTcs.Task.IsCompleted && DateTime.UtcNow < deadline)
        {
            if (latestQuestion is { } q && answeredIndexes.Add(q.Index))
            {
                // Host and second player answer (one "correctly enough", one not); third never answers.
                await hostConn.InvokeAsync("SubmitAnswer", roomId, q.MatchQuestionId, 0, null);
                await secondConn.InvokeAsync("SubmitAnswer", roomId, q.MatchQuestionId, 1, null);
            }

            await Task.Delay(200);
        }

        Assert.True(matchEndedTcs.Task.IsCompletedSuccessfully, "The match did not finish within the expected time.");
        var ended = await matchEndedTcs.Task;

        Assert.Equal(3, ended.Ranking.Count);
        Assert.Equal(5, ended.Review.Count);
        Assert.Equal(0, ended.Ranking.Single(r => r.UserId == third.UserId).Score);
        Assert.True(ended.Ranking.Single(r => r.UserId == host.UserId).Score > 0 ||
                    ended.Ranking.Single(r => r.UserId == second.UserId).Score > 0);

        // Persisted results (REST) must match what the live event reported.
        var resultsResponse = await GetAsync(client, host.Token, $"/api/rooms/{roomId}/results");
        resultsResponse.EnsureSuccessStatusCode();
        var results = (await resultsResponse.Content.ReadFromJsonAsync<MatchResultsDto>(JsonOptions))!;
        Assert.Equal(ended.Ranking.OrderBy(r => r.Rank).Select(r => r.UserId),
            results.Ranking.OrderBy(r => r.Rank).Select(r => r.UserId));

        // A finished room should no longer show up in the open lobby list.
        var lobbyResponse = await GetAsync(client, host.Token, "/api/rooms");
        var openRooms = await lobbyResponse.Content.ReadFromJsonAsync<List<JsonElement>>(JsonOptions);
        Assert.DoesNotContain(openRooms!, r => r.GetProperty("id").GetGuid() == roomId);
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

    private static Task<HttpResponseMessage> GetAsync(HttpClient client, string token, string url)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client.GetAsync(url);
    }
}
