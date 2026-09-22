using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using BrainArena.Application.Auth;
using BrainArena.Application.Matches;
using BrainArena.Domain.Enums;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;

namespace BrainArena.IntegrationTests;

/// <summary>
/// Phase 9: a top-K-advance mini-tournament. 4 players, RoomSize=2, AdvancesPerRoom=1 — round 1 is
/// two 1v1-shaped rooms (top-1 advances from each, i.e. a winner), round 2 is the final (the two
/// round-1 winners, single room, its own ranking crowns the champion). Uses calculation mode so
/// the "winner" in every room can submit a *guaranteed-correct* answer (derived from the question
/// text, same trick as FullCalculationMatchFlowTests) rather than guessing a multiple-choice
/// index, which would make the test's designated winner nondeterministic.
/// </summary>
public class TournamentFlowTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public async Task FourPlayers_PlayATwoRoundTournamentToACrownedChampion()
    {
        var client = factory.CreateClient();

        var players = new List<AuthResponse>();
        for (var i = 0; i < 4; i++)
        {
            players.Add(await RegisterAsync(client, $"tourney{i}"));
        }

        var createResponse = await PostAsync(client, players[0].Token, "/api/tournaments", new
        {
            name = "Mini Cup",
            topic = "Math",
            gameMode = CalculationGameMode.Key,
            questionCount = 5,
            secondsPerQuestion = 10,
            tournamentSize = 4,
            roomSize = 2,
            advancesPerRoom = 1,
            minPlayersToStart = 4
        });
        createResponse.EnsureSuccessStatusCode();
        var tournamentId = (await createResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        // The 4th join fills the tournament (TournamentSize=4) and auto-starts round 1.
        for (var i = 1; i < 4; i++)
        {
            var joinResponse = await PostAsync(client, players[i].Token, $"/api/tournaments/{tournamentId}/join");
            joinResponse.EnsureSuccessStatusCode();
        }

        // One persistent hub connection per player. Each answers correctly-and-immediately
        // whenever `winnerOf[userId]` is true for their *current* room, and never answers
        // otherwise — deterministic win/loss regardless of question content.
        var winnerOf = new Dictionary<Guid, bool>();
        var currentRoomOf = new Dictionary<Guid, Guid>();
        var connections = new Dictionary<Guid, HubConnection>();

        foreach (var p in players)
        {
            var userId = p.UserId;
            var conn = BuildHubConnection(p.Token);

            async Task AnswerIfWinner(Guid matchQuestionId, string text)
            {
                if (!winnerOf.GetValueOrDefault(userId) || !currentRoomOf.TryGetValue(userId, out var roomId))
                {
                    return;
                }
                var expected = ComputeExpected(text);
                try
                {
                    await conn.InvokeAsync("SubmitAnswer", roomId, matchQuestionId, null, expected);
                }
                catch
                {
                    // Best-effort: a late/duplicate submission after the round already moved on is fine to ignore.
                }
            }

            conn.On<QuestionClientPayload>("QuestionStarted", q => AnswerIfWinner(q.MatchQuestionId, q.Text));
            // JoinRoomGroup can land after the match already started (fast test timing) — the
            // resync this test cares about is "there's a live question right now," handled the
            // same way QuestionStarted would be.
            conn.On<MatchResyncPayload>("MatchResync", resync =>
            {
                if (resync.Phase == "Question" && resync.CurrentQuestion is not null)
                {
                    return AnswerIfWinner(resync.CurrentQuestion.MatchQuestionId, resync.CurrentQuestion.Text);
                }
                return Task.CompletedTask;
            });

            connections[userId] = conn;
            await conn.StartAsync();
        }

        var round1 = await WaitForRoundAsync(client, players[0].Token, tournamentId, roundNumber: 1, TimeSpan.FromSeconds(20));
        var round1Rooms = round1.GetProperty("rooms").EnumerateArray().ToList();
        Assert.Equal(2, round1Rooms.Count);

        var round1Winners = new List<Guid>();
        foreach (var room in round1Rooms)
        {
            round1Winners.Add(await AssignRolesAndJoinAsync(room, connections, winnerOf, currentRoomOf));
        }

        await WaitForAllRoomsFinishedAsync(client, players[0].Token, tournamentId, roundNumber: 1, TimeSpan.FromSeconds(60));

        var round2 = await WaitForRoundAsync(client, players[0].Token, tournamentId, roundNumber: 2, TimeSpan.FromSeconds(20));
        Assert.True(round2.GetProperty("isFinal").GetBoolean());
        var round2Rooms = round2.GetProperty("rooms").EnumerateArray().ToList();
        var finalRoom = Assert.Single(round2Rooms);
        var finalRoomPlayers = finalRoom.GetProperty("players").EnumerateArray()
            .Select(p => p.GetProperty("userId").GetGuid()).ToHashSet();
        Assert.Equal(round1Winners.ToHashSet(), finalRoomPlayers); // exactly the two round-1 winners advanced

        var champion = await AssignRolesAndJoinAsync(finalRoom, connections, winnerOf, currentRoomOf);

        var finalDetail = await WaitForConditionAsync(
            client, players[0].Token, tournamentId,
            d => d.GetProperty("status").GetString() == nameof(TournamentStatus.Finished),
            TimeSpan.FromSeconds(60));

        Assert.Equal(champion, finalDetail.GetProperty("championUserId").GetGuid());
        var championEntry = finalDetail.GetProperty("players").EnumerateArray()
            .Single(p => p.GetProperty("userId").GetGuid() == champion);
        Assert.Equal(nameof(TournamentPlayerStatus.Champion), championEntry.GetProperty("status").GetString());

        foreach (var conn in connections.Values)
        {
            await conn.DisposeAsync();
        }
    }

    /// <summary>Picks the first player in the room as the winner, wires both players' current-room state, and joins both hub connections to the room's group.</summary>
    private static async Task<Guid> AssignRolesAndJoinAsync(
        JsonElement room,
        Dictionary<Guid, HubConnection> connections,
        Dictionary<Guid, bool> winnerOf,
        Dictionary<Guid, Guid> currentRoomOf)
    {
        var roomId = room.GetProperty("roomId").GetGuid();
        var roomPlayers = room.GetProperty("players").EnumerateArray().Select(p => p.GetProperty("userId").GetGuid()).ToList();
        var winnerId = roomPlayers[0];

        foreach (var userId in roomPlayers)
        {
            winnerOf[userId] = userId == winnerId;
            currentRoomOf[userId] = roomId;
            await connections[userId].InvokeAsync("JoinRoomGroup", roomId);
        }

        return winnerId;
    }

    private async Task<JsonElement> WaitForRoundAsync(HttpClient client, string token, Guid tournamentId, int roundNumber, TimeSpan timeout)
    {
        var detail = await WaitForConditionAsync(
            client, token, tournamentId,
            d => d.GetProperty("rounds").EnumerateArray().Any(r => r.GetProperty("roundNumber").GetInt32() == roundNumber),
            timeout);

        return detail.GetProperty("rounds").EnumerateArray().Single(r => r.GetProperty("roundNumber").GetInt32() == roundNumber);
    }

    private async Task WaitForAllRoomsFinishedAsync(HttpClient client, string token, Guid tournamentId, int roundNumber, TimeSpan timeout)
    {
        await WaitForConditionAsync(
            client, token, tournamentId,
            d =>
            {
                var round = d.GetProperty("rounds").EnumerateArray().SingleOrDefault(r => r.GetProperty("roundNumber").GetInt32() == roundNumber);
                if (round.ValueKind == JsonValueKind.Undefined)
                {
                    return false;
                }
                return round.GetProperty("rooms").EnumerateArray()
                    .All(rr => rr.GetProperty("roomStatus").GetString() == nameof(RoomStatus.Finished));
            },
            timeout);
    }

    private async Task<JsonElement> WaitForConditionAsync(
        HttpClient client, string token, Guid tournamentId, Func<JsonElement, bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow.Add(timeout);
        while (DateTime.UtcNow < deadline)
        {
            var response = await GetAsync(client, token, $"/api/tournaments/{tournamentId}");
            response.EnsureSuccessStatusCode();
            var detail = await response.Content.ReadFromJsonAsync<JsonElement>();
            if (condition(detail))
            {
                return detail;
            }
            await Task.Delay(300);
        }

        throw new TimeoutException("Condition was not met within the timeout.");
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
