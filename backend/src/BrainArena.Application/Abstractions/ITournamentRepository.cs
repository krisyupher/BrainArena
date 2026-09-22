using BrainArena.Domain.Entities;
using BrainArena.Domain.Enums;

namespace BrainArena.Application.Abstractions;

public interface ITournamentRepository
{
    /// <summary>Tournaments still accepting players or currently running, newest first.</summary>
    Task<List<Tournament>> GetOpenTournamentsAsync(CancellationToken ct = default);

    /// <summary>Full detail: players, every round, every round's rooms and their players.</summary>
    Task<Tournament?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Reverse lookup from a Room back to the tournament round it belongs to, if any.</summary>
    Task<TournamentRoundRoom?> GetRoundRoomByRoomIdAsync(Guid roomId, CancellationToken ct = default);

    /// <summary>
    /// Cheap "is this a tournament room, and if so which tournament" projection — used only to
    /// decide whether to acquire the per-tournament advancement lock at all, *before* acquiring it.
    /// Deliberately a scalar projection rather than GetRoundRoomByRoomIdAsync: a second call to that
    /// method on the same DbContext would return the *same tracked Tournament instance* from the
    /// first call's identity map without refreshing it from the database — the exact bug that made
    /// a concurrent caller's staleness check see a pre-advancement CurrentRoundNumber even after the
    /// other call had already committed the advance. A projection to Guid? never enters the change
    /// tracker, so it can't poison the real (tracked) fetch taken after the lock is held.
    /// </summary>
    Task<Guid?> GetTournamentIdForRoomAsync(Guid roomId, CancellationToken ct = default);

    /// <summary>A finished room's final standings (userId, score, rank), ordered by rank.</summary>
    Task<List<(Guid UserId, int Score, int Rank)>> GetRoomRankingAsync(Guid roomId, CancellationToken ct = default);

    Task AddAsync(Tournament tournament, CancellationToken ct = default);

    /// <summary>
    /// Explicit adds (not `tournament.Rounds.Add(...)`/`round.RoundRooms.Add(...)`) for a new
    /// round created on an *already-tracked, existing* Tournament — EF Core's change tracker can
    /// end up marking an entity with an explicitly-set client-generated key Modified instead of
    /// Added when it's only ever discovered via a navigation-collection Add() on a
    /// previously-loaded parent, generating an UPDATE that matches 0 rows instead of an INSERT.
    /// Mirrors MatchOrchestrator's db.MatchQuestions.AddRange(...) pattern for the same reason.
    /// </summary>
    Task AddRoundAsync(TournamentRound round, CancellationToken ct = default);
    Task AddRoundRoomAsync(TournamentRoundRoom roundRoom, CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);
}
