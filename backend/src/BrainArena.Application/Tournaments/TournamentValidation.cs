using BrainArena.Application.Common;
using BrainArena.Application.Rooms;

namespace BrainArena.Application.Tournaments;

/// <summary>
/// Pure validation rules for tournament creation, kept separate from TournamentService so they can
/// be unit tested without a database. Room-size-related bounds intentionally reuse RoomValidation's
/// constants — a tournament round's room is a normal Room under the hood.
/// </summary>
public static class TournamentValidation
{
    public const int MinTournamentSize = 4;
    public const int MaxTournamentSize = 64;

    public static void Validate(CreateTournamentRequest request, IReadOnlyCollection<string> validGameModeKeys)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length is < 3 or > 40)
            throw new AppException("Tournament name must be between 3 and 40 characters.");

        if (!validGameModeKeys.Contains(request.GameMode))
            throw new AppException("Unknown game mode.");

        if (request.TournamentSize is < MinTournamentSize or > MaxTournamentSize)
            throw new AppException($"Tournament size must be between {MinTournamentSize} and {MaxTournamentSize}.");

        if (request.RoomSize is < RoomValidation.MinPlayersLimit or > RoomValidation.MaxPlayersLimit)
            throw new AppException($"Room size must be between {RoomValidation.MinPlayersLimit} and {RoomValidation.MaxPlayersLimit}.");

        if (request.TournamentSize < request.RoomSize)
            throw new AppException("Tournament size must be at least the room size.");

        if (request.AdvancesPerRoom < 1 || request.AdvancesPerRoom >= request.RoomSize)
            throw new AppException("Advances per room must be at least 1 and less than the room size (someone has to be eliminated each round).");

        if (request.MinPlayersToStart < RoomValidation.MinPlayersLimit)
            throw new AppException($"Minimum players to start must be at least {RoomValidation.MinPlayersLimit}.");

        if (request.MinPlayersToStart > request.TournamentSize)
            throw new AppException("Minimum players to start cannot exceed the tournament size.");

        if (request.QuestionCount is < RoomValidation.MinQuestionCount or > RoomValidation.MaxQuestionCount)
            throw new AppException($"Question count must be between {RoomValidation.MinQuestionCount} and {RoomValidation.MaxQuestionCount}.");

        if (request.SecondsPerQuestion is < RoomValidation.MinSecondsPerQuestion or > RoomValidation.MaxSecondsPerQuestion)
            throw new AppException(
                $"Seconds per question must be between {RoomValidation.MinSecondsPerQuestion} and {RoomValidation.MaxSecondsPerQuestion}.");
    }
}
