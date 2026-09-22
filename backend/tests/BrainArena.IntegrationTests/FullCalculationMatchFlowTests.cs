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
/// Phase 6's version of FullMatchFlowTests.cs — same full server-authoritative loop, but for a
/// calculation-mode room: procedurally generated (never admin-authored) arithmetic problems, a
/// numeric answer submission instead of an option index, and payloads carrying Kind ==
/// "calculation" with no Options/CorrectOptionIndex. Also exercises the cascade-insert of the
/// freshly-generated, not-yet-persisted Question rows through the real Postgres database.
/// </summary>
public class FullCalculationMatchFlowTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public async Task TwoPlayers_JoinACalculationRoomAndFinishAMatch()
    {
        var client = factory.CreateClient();

        var host = await RegisterAsync(client, "calchost");
        var second = await RegisterAsync(client, "calcsecond");

        var createResponse = await PostAsync(client, host.Token, "/api/rooms", new
        {
            name = "Calculation Room",
            topic = "Math",
            maxPlayers = 2,
            minPlayersToStart = 2,
            questionCount = 5,
            secondsPerQuestion = 10,
            isPrivate = false,
            gameMode = CalculationGameMode.Key
        });
        createResponse.EnsureSuccessStatusCode();
        var room = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var roomId = room.GetProperty("id").GetGuid();

        await using var hostConn = BuildHubConnection(host.Token);
        await using var secondConn = BuildHubConnection(second.Token);

        QuestionClientPayload? latestQuestion = null;
        var matchEndedTcs = new TaskCompletionSource<MatchEndedPayload>();

        void WireEvents(HubConnection conn)
        {
            conn.On<QuestionClientPayload>("QuestionStarted", q => latestQuestion = q);
            conn.On<MatchEndedPayload>("MatchEnded", m => matchEndedTcs.TrySetResult(m));
        }

        WireEvents(hostConn);
        WireEvents(secondConn);

        await hostConn.StartAsync();
        await secondConn.StartAsync();

        await hostConn.InvokeAsync("JoinRoomGroup", roomId);
        await JoinRoomAsync(client, second.Token, roomId); // fills the room -> auto-starts
        await secondConn.InvokeAsync("JoinRoomGroup", roomId);

        var answeredIndexes = new HashSet<int>();
        var deadline = DateTime.UtcNow.AddSeconds(90);

        while (!matchEndedTcs.Task.IsCompleted && DateTime.UtcNow < deadline)
        {
            if (latestQuestion is { } q && answeredIndexes.Add(q.Index))
            {
                Assert.Equal(CalculationGameMode.Key, q.Kind);
                Assert.Null(q.Options);

                var correctAnswer = ComputeExpected(q.Text);
                // Host answers correctly, second player deliberately wrong — mirrors
                // FullMatchFlowTests' "one correct enough, one not" pattern.
                await hostConn.InvokeAsync("SubmitAnswer", roomId, q.MatchQuestionId, null, correctAnswer);
                await secondConn.InvokeAsync("SubmitAnswer", roomId, q.MatchQuestionId, null, correctAnswer + 1);
            }

            await Task.Delay(200);
        }

        Assert.True(matchEndedTcs.Task.IsCompletedSuccessfully, "The match did not finish within the expected time.");
        var ended = await matchEndedTcs.Task;

        Assert.Equal(2, ended.Ranking.Count);
        Assert.Equal(5, ended.Review.Count);
        Assert.All(ended.Review, r =>
        {
            Assert.Equal(CalculationGameMode.Key, r.Kind);
            Assert.Null(r.Options);
            Assert.Null(r.CorrectOptionIndex);
            Assert.NotNull(r.CorrectNumericAnswer);
        });
        Assert.True(ended.Ranking.Single(r => r.UserId == host.UserId).Score > 0);
        Assert.Equal(0, ended.Ranking.Single(r => r.UserId == second.UserId).Score);

        // Persisted results (REST) must match what the live event reported, confirming the
        // procedurally generated Question rows round-tripped through EF Core correctly.
        var resultsResponse = await GetAsync(client, host.Token, $"/api/rooms/{roomId}/results");
        resultsResponse.EnsureSuccessStatusCode();
        var results = (await resultsResponse.Content.ReadFromJsonAsync<MatchResultsDto>(JsonOptions))!;
        Assert.Equal(5, results.Review.Count);
        Assert.All(results.Review, r => Assert.Equal(CalculationGameMode.Key, r.Kind));
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
