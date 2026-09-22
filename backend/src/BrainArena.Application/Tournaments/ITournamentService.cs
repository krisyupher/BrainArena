namespace BrainArena.Application.Tournaments;

public interface ITournamentService
{
    Task<IReadOnlyList<TournamentSummaryDto>> GetOpenTournamentsAsync(CancellationToken ct = default);
    Task<TournamentDetailDto> CreateTournamentAsync(Guid creatorUserId, CreateTournamentRequest request, CancellationToken ct = default);
    Task<TournamentDetailDto> GetTournamentDetailAsync(Guid tournamentId, CancellationToken ct = default);
    Task<TournamentDetailDto> JoinTournamentAsync(Guid userId, Guid tournamentId, CancellationToken ct = default);
    Task LeaveTournamentAsync(Guid userId, Guid tournamentId, CancellationToken ct = default);

    /// <summary>Throws AppException if the requester isn't the creator or MinPlayersToStart isn't met yet.</summary>
    Task StartTournamentAsync(Guid tournamentId, Guid requestingUserId, CancellationToken ct = default);

    /// <summary>
    /// Called by MatchOrchestrator after any room's match finishes. No-ops if the room isn't part
    /// of an active tournament round; otherwise advances the bracket once every room in the
    /// current round has finished.
    /// </summary>
    Task HandleRoomMatchFinishedAsync(Guid roomId, CancellationToken ct = default);
}
