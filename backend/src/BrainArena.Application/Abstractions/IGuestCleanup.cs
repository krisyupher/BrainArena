namespace BrainArena.Application.Abstractions;

public interface IGuestCleanup
{
    /// <summary>
    /// Deletes up to <paramref name="batchSize"/> Guest-role users created before
    /// <paramref name="cutoff"/>, together with the rooms they host and everything under them.
    /// Returns how many guests were deleted.
    /// </summary>
    Task<int> DeleteGuestsCreatedBeforeAsync(DateTimeOffset cutoff, int batchSize, CancellationToken ct = default);
}
