namespace BrainArena.Application.Matches;

public interface IMatchResultsService
{
    /// <summary>Reads the final ranking and per-question review from persisted data — works even
    /// after the live orchestrator state for this match has been cleaned up or the server restarted.
    /// Keyed by room rather than match id since a room has at most one match in this phase, and the
    /// frontend only ever knows a room id (it never needs to learn the match id).</summary>
    Task<MatchResultsDto> GetResultsByRoomAsync(Guid roomId, CancellationToken ct = default);
}
