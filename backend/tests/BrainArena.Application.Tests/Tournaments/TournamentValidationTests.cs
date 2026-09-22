using BrainArena.Application.Common;
using BrainArena.Application.Matches;
using BrainArena.Application.Tournaments;
using BrainArena.Domain.Enums;

namespace BrainArena.Application.Tests.Tournaments;

public class TournamentValidationTests
{
    private static readonly string[] ValidGameModeKeys = [MultipleChoiceGameMode.Key, CalculationGameMode.Key];

    private static CreateTournamentRequest ValidRequest() => new(
        Name: "Spring Cup",
        Topic: RoomTopic.Math,
        GameMode: MultipleChoiceGameMode.Key,
        QuestionCount: 10,
        SecondsPerQuestion: 20,
        TournamentSize: 8,
        RoomSize: 4,
        AdvancesPerRoom: 2,
        MinPlayersToStart: 4);

    [Fact]
    public void Validate_AcceptsARequestWithinAllLimits()
    {
        var exception = Record.Exception(() => TournamentValidation.Validate(ValidRequest(), ValidGameModeKeys));

        Assert.Null(exception);
    }

    [Fact]
    public void Validate_RejectsAnUnknownGameMode()
    {
        var request = ValidRequest() with { GameMode = "memory" };

        Assert.Throws<AppException>(() => TournamentValidation.Validate(request, ValidGameModeKeys));
    }

    [Theory]
    [InlineData(3)]
    [InlineData(65)]
    public void Validate_RejectsTournamentSizeOutsideRange(int size)
    {
        var request = ValidRequest() with { TournamentSize = size };

        Assert.Throws<AppException>(() => TournamentValidation.Validate(request, ValidGameModeKeys));
    }

    [Fact]
    public void Validate_RejectsTournamentSizeSmallerThanRoomSize()
    {
        var request = ValidRequest() with { TournamentSize = 4, RoomSize = 6 };

        Assert.Throws<AppException>(() => TournamentValidation.Validate(request, ValidGameModeKeys));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    public void Validate_RejectsAdvancesPerRoomOutOfRange(int advancesPerRoom)
    {
        // RoomSize is 4 in ValidRequest — 0 advances is nonsensical, and 4 (== RoomSize) means
        // nobody is ever eliminated, which would make the bracket never converge.
        var request = ValidRequest() with { AdvancesPerRoom = advancesPerRoom };

        Assert.Throws<AppException>(() => TournamentValidation.Validate(request, ValidGameModeKeys));
    }

    [Fact]
    public void Validate_RejectsMinPlayersToStartAboveTournamentSize()
    {
        var request = ValidRequest() with { MinPlayersToStart = 9 };

        Assert.Throws<AppException>(() => TournamentValidation.Validate(request, ValidGameModeKeys));
    }

    [Theory]
    [InlineData(4)]
    [InlineData(21)]
    public void Validate_RejectsQuestionCountOutsideRange(int questionCount)
    {
        var request = ValidRequest() with { QuestionCount = questionCount };

        Assert.Throws<AppException>(() => TournamentValidation.Validate(request, ValidGameModeKeys));
    }
}
