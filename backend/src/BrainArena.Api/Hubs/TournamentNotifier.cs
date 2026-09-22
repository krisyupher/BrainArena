using BrainArena.Application.Abstractions;
using Microsoft.AspNetCore.SignalR;

namespace BrainArena.Api.Hubs;

/// <summary>Mirrors RoomNotifier — deliberately payload-free, clients re-fetch GET /api/tournaments/{id}.</summary>
public class TournamentNotifier(IHubContext<RoomHub> hub) : ITournamentNotifier
{
    public Task NotifyTournamentListChangedAsync(CancellationToken ct = default) =>
        hub.Clients.Group("tournament-lobby").SendAsync("TournamentListChanged", cancellationToken: ct);

    public Task NotifyTournamentUpdatedAsync(Guid tournamentId, CancellationToken ct = default) =>
        hub.Clients.Group(RoomHub.TournamentGroupName(tournamentId)).SendAsync("TournamentUpdated", cancellationToken: ct);
}
