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
/// A solitary (practice) room is exactly 1/1 and auto-starts synchronously inside the
/// POST /api/rooms request itself — there's no second player to trigger auto-start via join, and
/// no waiting-room phase at all. This confirms: the REST response already reports
/// Status == InProgress (the RoomService re-fetch after TryAutoStartAsync), and that joining the
/// SignalR group afterwards correctly resyncs into a match that's already running — exactly the
/// same "already active when I join the group" path MatchPlay relies on, just reached without ever
/// visiting the waiting room. Uses CalculationGameMode so the single player can submit a
/// guaranteed-correct answer (deterministic score, matching FullCalculationMatchFlowTests' trick).
/// </summary>
public class SolitaryMatchFlowTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public async Task CreatingASolitaryRoom_AutoStartsImmediatelyAndFinishesWithOneRankedPlayer()
    {
        var client = factory.CreateClient();
        var player = await RegisterAsync(client, "solo");

        var createResponse = await PostAsync(client, player.Token, "/api/rooms", new
        {
            name = "Solo Practice",
            topic = "Math",
            maxPlayers = 6, // deliberately "wrong" — RoomService must force this to 1 for Solitary
            minPlayersToStart = 4,
            questionCount = 5,
            secondsPerQuestion = 10,
            isPrivate = true, // deliberately "wrong" too — must be forced to false
            gameMode = CalculationGameMode.Key,
            kind = "Solitary"
        });
        createResponse.EnsureSuccessStatusCode();
        var room = await createResponse.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("InProgress", room.GetProperty("status").GetString());
        Assert.Equal(1, room.GetProperty("maxPlayers").GetInt32());
        Assert.Equal(1, room.GetProperty("minPlayersToStart").GetInt32());
        Assert.False(room.GetProperty("isPrivate").GetBoolean());
        Assert.Equal("Solitary", room.GetProperty("kind").GetString());
        var roomId = room.GetProperty("id").GetGuid();

        await using var conn = BuildHubConnection(player.Token);

        QuestionClientPayload? latestQuestion = null;
        var matchEndedTcs = new TaskCompletionSource<MatchEndedPayload>();

        conn.On<QuestionClientPayload>("QuestionStarted", q => latestQuestion = q);
        conn.On<MatchEndedPayload>("MatchEnded", m => matchEndedTcs.TrySetResult(m));
        // The match auto-started synchronously inside the REST call above, before this SignalR
        // connection existed at all — MatchResync (not QuestionStarted) is what actually delivers
        // the first live question here, exactly the race JoinRoomGroup's resync path exists for.
        conn.On<MatchResyncPayload>("MatchResync", resync =>
        {
            if (resync.Phase == "Question" && resync.CurrentQuestion is not null)
            {
                latestQuestion = resync.CurrentQuestion;
            }
        });

        await conn.StartAsync();
        await conn.InvokeAsync("JoinRoomGroup", roomId);

        var answeredIndexes = new HashSet<int>();
        var deadline = DateTime.UtcNow.AddSeconds(60);

        while (!matchEndedTcs.Task.IsCompleted && DateTime.UtcNow < deadline)
        {
            if (latestQuestion is { } q && answeredIndexes.Add(q.Index))
            {
                var correctAnswer = ComputeExpected(q.Text);
                await conn.InvokeAsync("SubmitAnswer", roomId, q.MatchQuestionId, null, correctAnswer);
            }

            await Task.Delay(200);
        }

        Assert.True(matchEndedTcs.Task.IsCompletedSuccessfully, "The solo match did not finish within the expected time.");
        var ended = await matchEndedTcs.Task;

        var onlyEntry = Assert.Single(ended.Ranking);
        Assert.Equal(player.UserId, onlyEntry.UserId);
        Assert.Equal(1, onlyEntry.Rank);
        Assert.True(onlyEntry.Score > 0);

        var resultsResponse = await GetAsync(client, player.Token, $"/api/rooms/{roomId}/results");
        resultsResponse.EnsureSuccessStatusCode();
        var results = (await resultsResponse.Content.ReadFromJsonAsync<MatchResultsDto>(JsonOptions))!;
        Assert.Single(results.Ranking);
    }

    private static decimal ComputeExpected(string questionText)
    {
        var parts = questionText.Replace(" = ?", string.Empty).Split(' ');
        var a = decimal.Parse(parts[0]);
        var b = decimal.Parse(parts[2]);
        return parts[1] switch
        {
            "+" => a + b,
            "-" => a - b,
            "×" => a * b,
            _ => throw new InvalidOperationException($"Unrecognized operator in '{questionText}'.")
        };
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

    private static Task<HttpResponseMessage> PostAsync<TBody>(HttpClient client, string token, string url, TBody body)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client.PostAsJsonAsync(url, body, JsonOptions);
    }

    private static Task<HttpResponseMessage> GetAsync(HttpClient client, string token, string url)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client.GetAsync(url);
    }
}
