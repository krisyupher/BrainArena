using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using BrainArena.Application.Auth;
using Microsoft.AspNetCore.Mvc;

namespace BrainArena.IntegrationTests;

/// <summary>Real (tiny) limits — the base factory relaxes them so ordinary suites never trip them.</summary>
public class RateLimitedIntegrationTestFactory : IntegrationTestFactory
{
    protected override IReadOnlyDictionary<string, string?> ConfigOverrides => new Dictionary<string, string?>
    {
        ["RateLimiting:Auth:TokenLimit"] = "3",
        ["RateLimiting:Auth:TokensPerMinute"] = "1",
        ["RateLimiting:GuestCreation:TokenLimit"] = "2",
        ["RateLimiting:GuestCreation:TokensPerMinute"] = "1"
    };
}

internal static class RateLimitTestHelpers
{
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public static Task<HttpResponseMessage> Register(HttpClient client, string namePrefix) =>
        client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(
            Email: $"{namePrefix}-{Guid.NewGuid():N}@example.com",
            Password: "P@ssw0rd123",
            DisplayName: namePrefix));

    public static object RoomRequest(string kind, int questionCount = 5) => new
    {
        name = "Rate Limit Room",
        topic = "Math",
        maxPlayers = 2,
        minPlayersToStart = 2,
        questionCount,
        secondsPerQuestion = 10,
        isPrivate = false,
        gameMode = "calculation",
        kind
    };

    public static async Task AssertTooManyRequests(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        Assert.False(string.IsNullOrWhiteSpace(problem?.Title));
    }
}

public class AuthRateLimitingTests(RateLimitedIntegrationTestFactory factory) : IClassFixture<RateLimitedIntegrationTestFactory>
{
    [Fact]
    public async Task LoginAndRegister_ShareOnePerIpBudget_AndReturn429ProblemDetailsOnceItsSpent()
    {
        var client = factory.CreateClient();

        for (var i = 0; i < 3; i++)
        {
            (await RateLimitTestHelpers.Register(client, $"burst{i}")).EnsureSuccessStatusCode();
        }

        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("nobody@example.com", "whatever"));

        await RateLimitTestHelpers.AssertTooManyRequests(login);
    }
}

public class RoomCreationRateLimitingTests(RateLimitedIntegrationTestFactory factory) : IClassFixture<RateLimitedIntegrationTestFactory>
{
    [Fact]
    public async Task AnonymousCreation_IsLimitedPerIp_WithoutAffectingSignedInUsers()
    {
        var client = factory.CreateClient();

        // The limiter runs before validation, so invalid requests still spend tokens — this drains
        // the guest bucket without minting guest accounts or starting practice matches.
        for (var i = 0; i < 2; i++)
        {
            var invalid = await client.PostAsJsonAsync("/api/rooms", RateLimitTestHelpers.RoomRequest("Solitary", questionCount: 1));
            Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        }

        var throttled = await client.PostAsJsonAsync("/api/rooms", RateLimitTestHelpers.RoomRequest("Solitary"));
        await RateLimitTestHelpers.AssertTooManyRequests(throttled);

        // A signed-in user is partitioned by user id, not by the (shared) IP.
        var registration = await RateLimitTestHelpers.Register(client, "ratelimithost");
        registration.EnsureSuccessStatusCode();
        var auth = (await registration.Content.ReadFromJsonAsync<AuthResponse>(RateLimitTestHelpers.JsonOptions))!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);

        var created = await client.PostAsJsonAsync("/api/rooms", RateLimitTestHelpers.RoomRequest("Multiplayer"));

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
    }
}
