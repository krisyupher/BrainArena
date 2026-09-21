using BrainArena.Application.Matches;

namespace BrainArena.Application.Abstractions;

/// <summary>
/// Drives the live match state machine (countdown -> question -> reveal -> ... -> results) and
/// owns all in-memory match state. The interface lives in Application so RoomService and the
/// SignalR hub can depend on it without either one owning SignalR/timer details directly; the
/// concrete implementation lives in the Api project since it needs IHubContext&lt;RoomHub&gt;.
/// </summary>
public interface IMatchOrchestrator
{
    bool HasActiveMatch(Guid roomId);

    /// <summary>No-ops if the room isn't full or a match is already running.</summary>
    Task TryAutoStartAsync(Guid roomId, CancellationToken ct = default);

    /// <summary>Throws AppException if the requester isn't the host or the room can't start yet.</summary>
    Task StartNowAsync(Guid roomId, Guid requestingUserId, CancellationToken ct = default);

    /// <summary>Throws HubException-friendly errors via AppException for invalid submissions.</summary>
    Task SubmitAnswerAsync(Guid roomId, Guid userId, Guid matchQuestionId, int selectedOptionIndex);

    /// <summary>Null if there's no active match for this room (caller should treat it as a waiting-room join instead).</summary>
    MatchResyncPayload? Join(Guid roomId, Guid userId);

    void MarkDisconnected(Guid roomId, Guid userId);
}
