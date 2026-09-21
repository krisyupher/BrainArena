using BrainArena.Application.Common;
using BrainArena.Domain.Entities;
using BrainArena.Domain.Enums;

namespace BrainArena.Application.Matches;

/// <summary>
/// Pure match-start rules, kept separate from orchestration so they can be unit tested without
/// a database or SignalR. A room's own MaxPlayers/MinPlayersToStart settings are always the
/// source of truth (validated at room-creation time by RoomValidation).
/// </summary>
public static class MatchStartRules
{
    /// <summary>The room is full — start automatically regardless of who is host.</summary>
    public static bool CanAutoStart(Room room) =>
        room.Status == RoomStatus.Waiting && room.Players.Count >= room.MaxPlayers;

    /// <summary>Throws an AppException with a client-friendly message if "Start now" isn't allowed yet.</summary>
    public static void EnsureCanStartNow(Room room, Guid requestingUserId)
    {
        if (room.Status != RoomStatus.Waiting)
        {
            throw new AppException("This room has already started or finished.", 409);
        }

        if (room.HostUserId != requestingUserId)
        {
            throw new AppException("Only the host can start the match.", 403);
        }

        if (room.Players.Count < room.MinPlayersToStart)
        {
            throw new AppException($"Need at least {room.MinPlayersToStart} players to start.", 409);
        }
    }
}
