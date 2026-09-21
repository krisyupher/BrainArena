using BrainArena.Api.Extensions;
using BrainArena.Application.Abstractions;
using BrainArena.Application.Chat;
using BrainArena.Application.Common;
using BrainArena.Application.Rooms;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace BrainArena.Api.Hubs;

[Authorize]
public class RoomHub(
    IRoomService roomService,
    IMatchOrchestrator matchOrchestrator,
    IChatService chatService,
    RoomConnectionTracker tracker) : Hub
{
    private const string LobbyGroup = "lobby";

    public static string RoomGroupName(Guid roomId) => $"room:{roomId}";

    public override async Task OnConnectedAsync()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, LobbyGroup);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, LobbyGroup);

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

    /// <summary>Explicit leave, called when a player navigates away from the waiting room (a no-op once a match has started).</summary>
    public async Task LeaveRoomGroup(Guid roomId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, RoomGroupName(roomId));
        tracker.TryUntrack(Context.ConnectionId, out _);
        await roomService.LeaveRoomAsync(Context.User!.GetUserId(), roomId);
    }

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

    public async Task SubmitAnswer(Guid roomId, Guid matchQuestionId, int selectedOptionIndex)
    {
        try
        {
            await matchOrchestrator.SubmitAnswerAsync(roomId, Context.User!.GetUserId(), matchQuestionId, selectedOptionIndex);
        }
        catch (AppException ex)
        {
            throw new HubException(ex.Message);
        }

        await Clients.Caller.SendAsync("AnswerAccepted", matchQuestionId);
    }

    /// <summary>Waiting room and results screen only — ChatService itself rejects this while a match is in progress.</summary>
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
}
