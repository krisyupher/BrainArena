using System.Collections.Concurrent;

namespace BrainArena.Api.Hubs;

/// <summary>
/// Maps a live SignalR connection to the single room it's currently "in" (waiting room or
/// match), so OnDisconnectedAsync — which only gets a connection id — knows what to clean up.
/// A client is only ever considered to be in one room at a time in this app.
/// </summary>
public class RoomConnectionTracker
{
    private readonly ConcurrentDictionary<string, (Guid RoomId, Guid UserId)> _connections = new();

    public void Track(string connectionId, Guid roomId, Guid userId) =>
        _connections[connectionId] = (roomId, userId);

    public bool TryUntrack(string connectionId, out (Guid RoomId, Guid UserId) info) =>
        _connections.TryRemove(connectionId, out info);
}
