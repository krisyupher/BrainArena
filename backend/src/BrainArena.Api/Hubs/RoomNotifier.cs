using BrainArena.Application.Abstractions;
using Microsoft.AspNetCore.SignalR;

namespace BrainArena.Api.Hubs;

/// <summary>
/// Tells lobby clients that the room list changed; clients re-fetch GET /api/rooms.
/// Deliberately payload-free so this doesn't have to depend on IRoomService (which itself
/// depends on IRoomNotifier — a direct dependency the other way would be circular).
/// </summary>
public class RoomNotifier(IHubContext<RoomHub> hub) : IRoomNotifier
{
    public Task NotifyRoomListChangedAsync(CancellationToken ct = default) =>
        hub.Clients.Group("lobby").SendAsync("RoomListChanged", cancellationToken: ct);

    public Task NotifyRoomUpdatedAsync(Guid roomId, CancellationToken ct = default) =>
        hub.Clients.Group(RoomHub.RoomGroupName(roomId)).SendAsync("RoomUpdated", cancellationToken: ct);
}
