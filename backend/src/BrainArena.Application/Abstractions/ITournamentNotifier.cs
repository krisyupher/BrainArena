namespace BrainArena.Application.Abstractions;

/// <summary>Mirrors IRoomNotifier — lets Application announce tournament changes without depending on SignalR directly.</summary>
public interface ITournamentNotifier
{
    Task NotifyTournamentListChangedAsync(CancellationToken ct = default);

    /// <summary>Signals clients in a specific tournament's group to refetch its detail (players, rounds, standings).</summary>
    Task NotifyTournamentUpdatedAsync(Guid tournamentId, CancellationToken ct = default);
}
