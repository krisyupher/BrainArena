using BrainArena.Application.Common;

namespace BrainArena.Application.Rooms;

/// <summary>
/// Pure validation rules for room creation, kept separate from RoomService so they can be
/// unit tested without a database.
/// </summary>
public static class RoomValidation
{
    public const int MinPlayersLimit = 2;
    public const int MaxPlayersLimit = 10;
    public const int MinQuestionCount = 5;
    public const int MaxQuestionCount = 20;
    public const int MinSecondsPerQuestion = 10;
    public const int MaxSecondsPerQuestion = 60;

    public static void Validate(CreateRoomRequest request, IReadOnlyCollection<string> validGameModeKeys)
    {
        if (!validGameModeKeys.Contains(request.GameMode))
            throw new AppException("Unknown game mode.");


        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length is < 3 or > 40)
            throw new AppException("Room name must be between 3 and 40 characters.");

        if (request.MaxPlayers is < MinPlayersLimit or > MaxPlayersLimit)
            throw new AppException($"Max players must be between {MinPlayersLimit} and {MaxPlayersLimit}.");

        if (request.MinPlayersToStart < MinPlayersLimit)
            throw new AppException($"Minimum players to start must be at least {MinPlayersLimit}.");

        if (request.MinPlayersToStart > request.MaxPlayers)
            throw new AppException("Minimum players to start cannot exceed max players.");

        if (request.QuestionCount is < MinQuestionCount or > MaxQuestionCount)
            throw new AppException($"Question count must be between {MinQuestionCount} and {MaxQuestionCount}.");

        if (request.SecondsPerQuestion is < MinSecondsPerQuestion or > MaxSecondsPerQuestion)
            throw new AppException(
                $"Seconds per question must be between {MinSecondsPerQuestion} and {MaxSecondsPerQuestion}.");
    }
}
