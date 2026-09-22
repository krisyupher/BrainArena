using System.Collections.Concurrent;

namespace BrainArena.Application.Tournaments;

/// <summary>
/// Serializes round-advancement per tournament. Two rooms in the same round routinely finish
/// within moments of each other (they were created with identical timing), so
/// TournamentService.HandleRoomMatchFinishedAsync can genuinely be invoked concurrently for the
/// same tournament — without this, both calls could independently observe "all rooms finished"
/// and each create the next round, corrupting the bracket. Matches the rest of the app's
/// single-process, in-memory coordination approach (see MatchOrchestrator) rather than adding
/// DB-level optimistic concurrency.
/// </summary>
public class TournamentAdvancementLock
{
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _locks = new();

    public async Task<IDisposable> AcquireAsync(Guid tournamentId, CancellationToken ct = default)
    {
        var semaphore = _locks.GetOrAdd(tournamentId, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(ct);
        return new Releaser(semaphore);
    }

    private sealed class Releaser(SemaphoreSlim semaphore) : IDisposable
    {
        public void Dispose() => semaphore.Release();
    }
}
