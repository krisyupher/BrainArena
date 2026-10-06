using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using BrainArena.Application.Matches;
using BrainArena.Application.Rooms;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;

namespace BrainArena.IntegrationTests;

/// <summary>
/// End to end through the just-in-time generation engine and the server-paced flash: an anonymous
/// caller gets a guest session from the create call, then plays a Solitary flash-arithmetic match
/// where every number arrives on its own FlashNumber event and answering opens only on
/// AnswerWindowOpened. Proves: the sequence never ships inside QuestionStarted, an answer submitted
/// mid-flash is refused, TotalQuestions stays constant while rounds are generated one at a time, and
/// Level moves up after a correct round and down after a miss.
/// </summary>
public class FlashArithmeticSolitaryMatchFlowTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    private const int QuestionCount = 5;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public async Task AnonymousGuest_PlaysAServerPacedFlashMatch_LevelAdaptsAndAnsweringOnlyOpensAfterTheFlash()
    {
        var client = factory.CreateClient();

        // No Authorization header at all — the anonymous-Solitary path.
        var createResponse = await client.PostAsJsonAsync("/api/rooms", new
        {
            name = "Anonymous Flash Practice",
            topic = "Math",
            maxPlayers = 1,
            minPlayersToStart = 1,
            questionCount = QuestionCount,
            secondsPerQuestion = 10,
            isPrivate = false,
            gameMode = FlashArithmeticGameMode.Key,
            kind = "Solitary",
            difficulty = "Easy"
        }, JsonOptions);
        createResponse.EnsureSuccessStatusCode();

        var created = (await createResponse.Content.ReadFromJsonAsync<CreateRoomResult>(JsonOptions))!;
        Assert.NotNull(created.GuestAuth);
        Assert.Equal("Guest", created.GuestAuth!.Role);
        Assert.Equal("InProgress", created.Room.Status.ToString());
        var roomId = created.Room.Id;
        var guestToken = created.GuestAuth.Token;

        var started = new ConcurrentDictionary<Guid, QuestionClientPayload>();
        var numbers = new ConcurrentDictionary<Guid, ConcurrentDictionary<int, int>>();
        var counts = new ConcurrentDictionary<Guid, int>();
        var opened = new ConcurrentDictionary<Guid, bool>();
        var toAnswer = new ConcurrentQueue<Guid>();
        var revealedAnswers = new ConcurrentDictionary<int, decimal>();
        var matchEnded = new TaskCompletionSource<MatchEndedPayload>();

        await using var conn = BuildHubConnection(guestToken);
        conn.On<QuestionClientPayload>("QuestionStarted", q => started[q.MatchQuestionId] = q);
        conn.On<FlashNumberPayload>("FlashNumber", n =>
        {
            numbers.GetOrAdd(n.MatchQuestionId, _ => new ConcurrentDictionary<int, int>())[n.Position] = n.Value;
            counts[n.MatchQuestionId] = n.Count;
        });
        conn.On<AnswerWindowOpenedPayload>("AnswerWindowOpened", w =>
        {
            opened[w.MatchQuestionId] = true;
            toAnswer.Enqueue(w.MatchQuestionId);
        });
        conn.On<QuestionRevealPayload>("QuestionRevealed", r => revealedAnswers[r.Index] = r.CorrectNumericAnswer!.Value);
        conn.On<MatchEndedPayload>("MatchEnded", m => matchEnded.TrySetResult(m));
        // The match auto-started inside the create call, so the first state may arrive as a resync.
        conn.On<MatchResyncPayload>("MatchResync", resync =>
        {
            if (resync.CurrentQuestion is { } q)
            {
                started[q.MatchQuestionId] = q;
                if (resync.Phase == "Question")
                {
                    opened[q.MatchQuestionId] = true;
                    toAnswer.Enqueue(q.MatchQuestionId);
                }
            }
        });

        await conn.StartAsync();
        await conn.InvokeAsync("JoinRoomGroup", roomId);

        // Alternating so both directions of the adaptive Level get exercised.
        bool[] answerCorrectly = [true, false, true, false, true];
        var submitted = new Dictionary<int, decimal>();
        bool? earlyAnswerRefused = null;
        var deadline = DateTime.UtcNow.AddSeconds(150);

        while (!matchEnded.Task.IsCompleted && DateTime.UtcNow < deadline)
        {
            // Mid-flash (at least one number still to come), answering must be refused.
            if (earlyAnswerRefused is null)
            {
                var flashing = numbers.FirstOrDefault(kv =>
                    !opened.ContainsKey(kv.Key) && counts.TryGetValue(kv.Key, out var count) &&
                    !kv.Value.IsEmpty && kv.Value.Keys.Max() < count - 1);
                if (flashing.Key != Guid.Empty)
                {
                    try
                    {
                        await conn.InvokeAsync("SubmitAnswer", roomId, flashing.Key, null, 1m);
                        earlyAnswerRefused = false;
                    }
                    catch (HubException)
                    {
                        earlyAnswerRefused = true;
                    }
                }
            }

            while (toAnswer.TryDequeue(out var matchQuestionId))
            {
                var question = started[matchQuestionId];
                // Joined too late to see every number of this round: leave it unanswered (a miss).
                if (!counts.TryGetValue(matchQuestionId, out var count) ||
                    !numbers.TryGetValue(matchQuestionId, out var seen) || seen.Count != count)
                {
                    continue;
                }

                var sum = seen.Values.Sum();
                decimal answer = answerCorrectly[question.Index] ? sum : sum + 1;
                await conn.InvokeAsync("SubmitAnswer", roomId, matchQuestionId, null, answer);
                submitted[question.Index] = answer;
            }

            await Task.Delay(50);
        }

        Assert.True(matchEnded.Task.IsCompletedSuccessfully, "The solo flash-arithmetic match did not finish within the expected time.");
        Assert.True(earlyAnswerRefused, "An answer submitted while numbers were still flashing was not refused.");

        var rounds = started.Values.OrderBy(q => q.Index).ToList();
        Assert.Equal(QuestionCount, rounds.Count);
        Assert.All(rounds, q => Assert.Equal(QuestionCount, q.TotalQuestions));
        Assert.All(rounds, q => Assert.Equal(string.Empty, q.Text));

        // Level is Easy's baseline in round 0, then follows the previous round's actual outcome.
        Assert.Equal(1, rounds[0].Level);
        var rises = 0;
        var drops = 0;
        for (var i = 1; i < rounds.Count; i++)
        {
            var previousCorrect = submitted.TryGetValue(i - 1, out var given) && given == revealedAnswers[i - 1];
            var expected = Math.Clamp(rounds[i - 1].Level!.Value + (previousCorrect ? 1 : -1), 1, 20);
            Assert.Equal(expected, rounds[i].Level);
            if (rounds[i].Level > rounds[i - 1].Level) rises++;
            if (rounds[i].Level < rounds[i - 1].Level) drops++;
        }
        Assert.True(rises > 0 && drops > 0, $"Expected the Level to both rise and drop (rises={rises}, drops={drops}).");

        // A guest session can still read its own practice results.
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", guestToken);
        var resultsResponse = await client.GetAsync($"/api/rooms/{roomId}/results");
        resultsResponse.EnsureSuccessStatusCode();
        var results = (await resultsResponse.Content.ReadFromJsonAsync<MatchResultsDto>(JsonOptions))!;
        Assert.Equal(created.GuestAuth.UserId, Assert.Single(results.Ranking).UserId);
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
