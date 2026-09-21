namespace BrainArena.Application.Abstractions;

/// <summary>
/// Lets the Application layer announce lobby changes without depending on SignalR directly
/// (the Api project implements this on top of IHubContext&lt;RoomHub&gt;).
/// </summary>
public interface IRoomNotifier
{
    Task NotifyRoomListChangedAsync(CancellationToken ct = default);

    /// <summary>Signals clients in a specific room's group to refetch that room's detail (player list, host, status).</summary>
    Task NotifyRoomUpdatedAsync(Guid roomId, CancellationToken ct = default);
}
