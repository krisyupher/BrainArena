using BrainArena.Api.Extensions;
using BrainArena.Application.Abstractions;
using BrainArena.Application.Chat;
using BrainArena.Application.Common;
using BrainArena.Application.Matches;
using BrainArena.Application.Reactions;
using BrainArena.Application.Rooms;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace BrainArena.Api.Hubs;

// No class-level [Authorize]: SignalR gates the connection handshake itself at the class level, so
// anonymous spectators couldn't connect at all if it stayed here. Every method that touches player
// state carries [Authorize] individually instead — see the reflection test in
// BrainArena.IntegrationTests that guards against a new participant-only method forgetting it.
public class RoomHub(
    IRoomService roomService,
    IMatchOrchestrator matchOrchestrator,
    IChatService chatService,
    RoomConnectionTracker tracker) : Hub
{
    private const string LobbyGroup = "lobby";
    private const string TournamentLobbyGroup = "tournament-lobby";

    public static string RoomGroupName(Guid roomId) => $"room:{roomId}";
    public static string TournamentGroupName(Guid tournamentId) => $"tournament:{tournamentId}";

    public override async Task OnConnectedAsync()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, LobbyGroup);
        await Groups.AddToGroupAsync(Context.ConnectionId, TournamentLobbyGroup);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, LobbyGroup);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, TournamentLobbyGroup);

        if (tracker.TryUntrack(Context.ConnectionId, out var info))
        {
            if (matchOrchestrator.HasActiveMatch(info.RoomId))
            {
                matchOrchestrator.MarkDisconnected(info.RoomId, info.UserId);
            }
            else
            {
                await roomService.LeaveRoomAsync(info.UserId, info.RoomId);
            }
        }

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Called once when a client lands on a room's waiting-room or match-play page. Joins the
    /// room's SignalR group and, if a match is already running, pushes a resync payload back —
    /// this is also how a reconnecting player picks the match back up.
    /// </summary>
    [Authorize]
    public async Task JoinRoomGroup(Guid roomId)
    {
        var userId = Context.User!.GetUserId();

        try
        {
            var room = await roomService.GetRoomDetailAsync(roomId);
            if (room.Players.All(p => p.UserId != userId))
            {
                throw new HubException("You are not a member of this room.");
            }
        }
        catch (AppException ex)
        {
            throw new HubException(ex.Message);
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, RoomGroupName(roomId));
        tracker.Track(Context.ConnectionId, roomId, userId);

        if (matchOrchestrator.HasActiveMatch(roomId))
        {
            var resync = matchOrchestrator.Join(roomId, userId);
            if (resync is not null)
            {
                await Clients.Caller.SendAsync("MatchResync", resync);
            }
        }
    }

    /// <summary>
    /// Anonymous-safe alternative to <see cref="JoinRoomGroup"/> for visitors watching without
    /// playing. No room-membership requirement, no <c>MatchResync</c> (that payload carries a
    /// personalized score and is participant-only) — instead a non-personalized
    /// <c>MatchSpectatorSync</c> — and no <see cref="RoomConnectionTracker"/> entry, since a
    /// spectator has no player-domain disconnect side effect to run; SignalR removes group
    /// membership automatically when the connection drops.
    /// </summary>
    public async Task JoinAsSpectator(Guid roomId)
    {
        var userId = Context.User?.GetUserIdOrNull();

        try
        {
            await roomService.GetVisibleRoomDetailAsync(roomId, userId);
        }
        catch (AppException ex)
        {
            throw new HubException(ex.Message);
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, RoomGroupName(roomId));

        if (matchOrchestrator.HasActiveMatch(roomId))
        {
            var snapshot = matchOrchestrator.Snapshot(roomId);
            if (snapshot is not null)
            {
                await Clients.Caller.SendAsync("MatchSpectatorSync", snapshot);
            }
        }
    }

    /// <summary>Explicit leave, called when a player navigates away from the waiting room (a no-op once a match has started).</summary>
    [Authorize]
    public async Task LeaveRoomGroup(Guid roomId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, RoomGroupName(roomId));
        tracker.TryUntrack(Context.ConnectionId, out _);
        await roomService.LeaveRoomAsync(Context.User!.GetUserId(), roomId);
    }

    /// <summary>
    /// Symmetric counterpart to <see cref="JoinAsSpectator"/> — the app-wide hub connection stays
    /// alive across navigation, so a spectator leaving a room's page must explicitly drop the
    /// group or stay subscribed to its broadcasts for the rest of the session. No player-domain
    /// side effect to run (spectators never had one), so this is just a group removal.
    /// </summary>
    public Task LeaveSpectatorGroup(Guid roomId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, RoomGroupName(roomId));

    [Authorize]
    public async Task StartNow(Guid roomId)
    {
        try
        {
            await matchOrchestrator.StartNowAsync(roomId, Context.User!.GetUserId());
        }
        catch (AppException ex)
        {
            throw new HubException(ex.Message);
        }
    }

    /// <summary>
    /// Accepts either answer shape — exactly one of the two should be non-null, depending on the
    /// room's game mode. The mode itself validates which (and rejects a malformed/missing one);
    /// this method stays mode-agnostic.
    /// </summary>
    [Authorize]
    public async Task SubmitAnswer(Guid roomId, Guid matchQuestionId, int? selectedOptionIndex, decimal? numericAnswer)
    {
        try
        {
            await matchOrchestrator.SubmitAnswerAsync(
                roomId, Context.User!.GetUserId(), matchQuestionId, new SubmittedAnswer(selectedOptionIndex, numericAnswer));
        }
        catch (AppException ex)
        {
            throw new HubException(ex.Message);
        }

        await Clients.Caller.SendAsync("AnswerAccepted", matchQuestionId);
    }

    /// <summary>
    /// Mounted on every room-lifecycle page (waiting room, match-play, results) for any signed-in
    /// caller, participant or spectator — chat is no longer disabled during questions (Phase 7).
    /// </summary>
    [Authorize]
    public async Task SendChatMessage(Guid roomId, string text)
    {
        ChatMessageDto message;
        try
        {
            message = await chatService.SendMessageAsync(roomId, Context.User!.GetUserId(), text);
        }
        catch (AppException ex)
        {
            throw new HubException(ex.Message);
        }

        await Clients.Group(RoomGroupName(roomId)).SendAsync("ChatMessageReceived", message);
    }

    [Authorize]
    public async Task ReportChatMessage(Guid messageId)
    {
        try
        {
            await chatService.ReportMessageAsync(messageId);
        }
        catch (AppException ex)
        {
            throw new HubException(ex.Message);
        }

        await Clients.Caller.SendAsync("ChatMessageReported", messageId);
    }

    /// <summary>
    /// A small fixed-emoji reaction, targeted at a competitor tile — ephemeral (never persisted),
    /// broadcast to the whole room group so everyone sees the same animated burst. Same "any
    /// signed-in user, player or spectator" gating as chat; no room-membership check since it's
    /// low-stakes and cosmetic only.
    /// </summary>
    [Authorize]
    public async Task SendReaction(Guid roomId, Guid targetUserId, string emoji)
    {
        try
        {
            ReactionValidation.Validate(emoji);
        }
        catch (AppException ex)
        {
            throw new HubException(ex.Message);
        }

        var payload = new ReactionSentPayload(Context.User!.GetUserId(), targetUserId, emoji);
        await Clients.Group(RoomGroupName(roomId)).SendAsync("ReactionSent", payload);
    }

    /// <summary>
    /// Anonymous-friendly, like room spectating — anyone can watch a tournament's progress live.
    /// Joining/starting a tournament still requires sign-in (TournamentsController/TournamentService).
    /// </summary>
    public Task JoinTournamentGroup(Guid tournamentId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, TournamentGroupName(tournamentId));

    public Task LeaveTournamentGroup(Guid tournamentId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, TournamentGroupName(tournamentId));
}
