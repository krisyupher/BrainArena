using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using BrainArena.Application.Matches;
using BrainArena.Application.Rooms;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;

namespace BrainArena.IntegrationTests;

/// <summary>
/// Exercises the just-in-time question generation engine end to end: an anonymous (no sign-in)
/// caller creates a Solitary flash-arithmetic room, gets back an auto-created guest account's
/// token in the same response, and plays a full match where every round after the first is
/// generated live based on whether the previous round was answered correctly. This is the test
/// that proves two things the plan called out as easy to silently regress: (1) TotalQuestions in
/// every QuestionStarted payload stays constant across the whole match even though
/// MatchRuntimeState.Questions.Count starts at 1 and grows round by round, and (2) Level actually
/// rises on a correct answer and drops on a wrong one, alternating in lockstep with a deliberately
/// alternating correct/wrong answer pattern.
/// </summary>
public class FlashArithmeticSolitaryMatchFlowTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public async Task AnonymousCaller_PlaysAFullSolitaryFlashArithmeticMatch_LevelAdaptsAndTotalQuestionsStaysConstant()
    {
        var client = factory.CreateClient();

        // No Authorization header at all — this is the anonymous-solitary path.
        var createResponse = await client.PostAsJsonAsync("/api/rooms", new
        {
            name = "Anonymous Flash Practice",
            topic = "Math",
            maxPlayers = 1,
            minPlayersToStart = 1,
            questionCount = 5,
            secondsPerQuestion = 10,
            isPrivate = false,
            gameMode = FlashArithmeticGameMode.Key,
            kind = "Solitary",
            difficulty = "Medium"
        }, JsonOptions);
        createResponse.EnsureSuccessStatusCode();

        var created = (await createResponse.Content.ReadFromJsonAsync<CreateRoomResult>(JsonOptions))!;
        Assert.NotNull(created.GuestAuth);
        var guestToken = created.GuestAuth!.Token;
        Assert.False(string.IsNullOrWhiteSpace(guestToken));
        Assert.Equal("InProgress", created.Room.Status.ToString());
        var roomId = created.Room.Id;

        await using var conn = BuildHubConnection(guestToken);

        var startedByIndex = new Dictionary<int, QuestionClientPayload>();
        var matchEndedTcs = new TaskCompletionSource<MatchEndedPayload>();

        conn.On<QuestionClientPayload>("QuestionStarted", q => startedByIndex[q.Index] = q);
        conn.On<MatchEndedPayload>("MatchEnded", m => matchEndedTcs.TrySetResult(m));
        // The match auto-started synchronously inside the REST call above — MatchResync (not
        // QuestionStarted) delivers the first live question here, same as SolitaryMatchFlowTests.
        conn.On<MatchResyncPayload>("MatchResync", resync =>
        {
            if (resync.Phase == "Question" && resync.CurrentQuestion is not null)
            {
                startedByIndex[resync.CurrentQuestion.Index] = resync.CurrentQuestion;
            }
        });

        await conn.StartAsync();
        await conn.InvokeAsync("JoinRoomGroup", roomId);

        // Deliberately alternating so the test proves both directions: round 0 answered correctly,
        // round 1 wrong, round 2 correctly, round 3 wrong, round 4 correctly (the last round's
        // answer never gets observed by a next round, since the match ends after it).
        bool[] answerCorrectly = [true, false, true, false, true];
        var answeredIndexes = new HashSet<int>();
        var deadline = DateTime.UtcNow.AddSeconds(90);

        while (!matchEndedTcs.Task.IsCompleted && DateTime.UtcNow < deadline)
        {
            foreach (var (index, q) in startedByIndex)
            {
                if (answeredIndexes.Add(index))
                {
                    var sum = q.Text.Split(',').Select(int.Parse).Sum();
                    var submitted = answerCorrectly[index] ? sum : sum + 1;
                    await conn.InvokeAsync("SubmitAnswer", roomId, q.MatchQuestionId, null, (decimal)submitted);
                }
            }

            await Task.Delay(200);
        }

        Assert.True(matchEndedTcs.Task.IsCompletedSuccessfully, "The solo flash-arithmetic match did not finish within the expected time.");
        await matchEndedTcs.Task;

        Assert.Equal(5, startedByIndex.Count);
        // The bug this test guards against: TotalQuestions must report the match's true target
        // count from round 0 onward, never grow live as MatchRuntimeState.Questions fills in.
        Assert.All(startedByIndex.Values, q => Assert.Equal(5, q.TotalQuestions));

        var levelByIndex = startedByIndex.ToDictionary(kv => kv.Key, kv => kv.Value.Level);
        Assert.Equal(3, levelByIndex[0]); // Medium's starting Level
        Assert.Equal(4, levelByIndex[1]); // round 0 was correct -> Level rose
        Assert.Equal(3, levelByIndex[2]); // round 1 was wrong -> Level dropped
        Assert.Equal(4, levelByIndex[3]); // round 2 was correct -> Level rose
        Assert.Equal(3, levelByIndex[4]); // round 3 was wrong -> Level dropped
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
