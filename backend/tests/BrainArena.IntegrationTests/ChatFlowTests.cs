using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using BrainArena.Application.Auth;
using BrainArena.Application.Chat;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;

namespace BrainArena.IntegrationTests;

/// <summary>
/// Exercises live chat (Phase 4) over the real SignalR pipeline: send/receive, the server-side
/// 1-message-per-2-seconds rate limit, the profanity filter, and reporting a message. Chat is no
/// longer disabled while a match is in progress (Phase 7 product decision — fairness only
/// restricts players' answers) — that shape is covered by ChatServiceTests, no need for a full
/// ~1-minute match here too.
/// </summary>
public class ChatFlowTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public async Task PlayersInAWaitingRoom_CanChatLive_WithRateLimitingAndProfanityFiltering()
    {
        var client = factory.CreateClient();

        var host = await RegisterAsync(client, "chathost");
        var second = await RegisterAsync(client, "chatsecond");

        var createResponse = await PostAsync(client, host.Token, "/api/rooms", new
        {
            name = "Chat Test Room",
            topic = "Geography",
            maxPlayers = 3,
            minPlayersToStart = 2,
            questionCount = 5,
            secondsPerQuestion = 10,
            isPrivate = false
        });
        createResponse.EnsureSuccessStatusCode();
        var room = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var roomId = room.GetProperty("id").GetGuid();

        var joinResponse = await PostAsync(client, second.Token, $"/api/rooms/{roomId}/join");
        joinResponse.EnsureSuccessStatusCode(); // room now 2/3 — stays Waiting, doesn't auto-start

        await using var hostConn = BuildHubConnection(host.Token);
        await using var secondConn = BuildHubConnection(second.Token);

        var received = new List<ChatMessageDto>();
        var reportAckTcs = new TaskCompletionSource<Guid>();
        secondConn.On<ChatMessageDto>("ChatMessageReceived", m => received.Add(m));
        // The ack for ReportChatMessage goes to whoever invoked it (secondConn below), not the sender.
        secondConn.On<Guid>("ChatMessageReported", id => reportAckTcs.TrySetResult(id));

        await hostConn.StartAsync();
        await secondConn.StartAsync();
        await hostConn.InvokeAsync("JoinRoomGroup", roomId);
        await secondConn.InvokeAsync("JoinRoomGroup", roomId);

        // Happy path: host sends a clean message, second player receives it live.
        await hostConn.InvokeAsync("SendChatMessage", roomId, "Good luck everyone!");
        await WaitUntilAsync(() => received.Count >= 1);
        Assert.Equal("Good luck everyone!", received[0].Text);
        var firstMessageId = received[0].Id;

        // Profanity filter: blocked words are censored, not rejected. Sent by the *other* player
        // so this doesn't collide with the rate-limit check below (rate limiting is per user).
        await secondConn.InvokeAsync("SendChatMessage", roomId, "you are an idiot");
        await WaitUntilAsync(() => received.Count >= 2);
        Assert.DoesNotContain("idiot", received[1].Text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("*****", received[1].Text); // "idiot" (5 letters) censored to 5 asterisks

        // Rate limit: a second message from the same user (host) within 2 seconds is rejected server-side.
        var rateLimitException = await Assert.ThrowsAsync<HubException>(
            () => hostConn.InvokeAsync("SendChatMessage", roomId, "spam"));
        Assert.Contains("too fast", rateLimitException.Message, StringComparison.OrdinalIgnoreCase);

        // Report: the caller gets an ack; the flag shows up in persisted history.
        await secondConn.InvokeAsync("ReportChatMessage", firstMessageId);
        var reportedId = await reportAckTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(firstMessageId, reportedId);

        var historyResponse = await GetAsync(client, host.Token, $"/api/rooms/{roomId}/chat");
        historyResponse.EnsureSuccessStatusCode();
        var history = (await historyResponse.Content.ReadFromJsonAsync<List<ChatMessageDto>>(JsonOptions))!;
        Assert.Equal(2, history.Count);
        Assert.True(history.Single(m => m.Id == firstMessageId).IsReported);
    }

    private static async Task WaitUntilAsync(Func<bool> condition, int timeoutMs = 5000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(50);
        }
        Assert.True(condition(), "Condition was not met within the timeout.");
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
