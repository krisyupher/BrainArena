using System.Reflection;
using BrainArena.Api.Auth;
using BrainArena.Api.Hubs;
using Microsoft.AspNetCore.Authorization;

namespace BrainArena.IntegrationTests;

/// <summary>
/// RoomHub deliberately has no class-level [Authorize] (Phase 5, spectator mode) — SignalR gates
/// the connection handshake itself at the class level, so an anonymous spectator couldn't connect
/// at all if it stayed there. Every method that requires a signed-in caller (join-as-player, start,
/// submit an answer, chat send/report/react — not all of these require room *membership* anymore
/// since Phase 7, just authentication) carries [Authorize] individually instead. This is a "fail
/// open by default" design at the hub level, so this reflection test exists purely to catch a
/// future method that forgets the attribute, rather than letting it silently become
/// anonymous-callable.
/// </summary>
public class RoomHubAuthorizationTests
{
    private static readonly string[] AuthenticatedOnlyMethodNames =
    [
        nameof(RoomHub.JoinRoomGroup),
        nameof(RoomHub.LeaveRoomGroup),
        nameof(RoomHub.StartNow),
        nameof(RoomHub.SubmitAnswer),
        nameof(RoomHub.SendChatMessage),
        nameof(RoomHub.ReportChatMessage),
        nameof(RoomHub.SendReaction)
    ];

    [Fact]
    public void RoomHub_HasNoClassLevelAuthorize()
    {
        Assert.Empty(typeof(RoomHub).GetCustomAttributes<AuthorizeAttribute>());
    }

    [Theory]
    [MemberData(nameof(AuthenticatedOnlyMethods))]
    public void AuthenticatedOnlyMethod_CarriesAuthorize(string methodName)
    {
        var method = typeof(RoomHub).GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance)
            ?? throw new InvalidOperationException($"RoomHub no longer has a method named '{methodName}'.");

        Assert.NotEmpty(method.GetCustomAttributes<AuthorizeAttribute>());
    }

    public static IEnumerable<object[]> AuthenticatedOnlyMethods() =>
        AuthenticatedOnlyMethodNames.Select(name => new object[] { name });

    /// <summary>Guest sessions are Solitary-practice-only — these must exclude them, not just require sign-in.</summary>
    [Theory]
    [InlineData(nameof(RoomHub.StartNow))]
    [InlineData(nameof(RoomHub.SendChatMessage))]
    [InlineData(nameof(RoomHub.ReportChatMessage))]
    [InlineData(nameof(RoomHub.SendReaction))]
    public void RegisteredOnlyMethod_RequiresTheRegisteredUserPolicy(string methodName)
    {
        var method = typeof(RoomHub).GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance)
            ?? throw new InvalidOperationException($"RoomHub no longer has a method named '{methodName}'.");

        Assert.Contains(method.GetCustomAttributes<AuthorizeAttribute>(), a => a.Policy == AuthPolicies.RegisteredUser);
    }

    [Theory]
    [InlineData(nameof(RoomHub.JoinAsSpectator))]
    [InlineData(nameof(RoomHub.LeaveSpectatorGroup))]
    public void SpectatorMethod_DoesNotCarryAuthorize(string methodName)
    {
        var method = typeof(RoomHub).GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance)
            ?? throw new InvalidOperationException($"RoomHub no longer has a method named '{methodName}'.");

        Assert.Empty(method.GetCustomAttributes<AuthorizeAttribute>());
    }
}
