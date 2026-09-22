using BrainArena.Application.Common;
using BrainArena.Application.Matches;
using BrainArena.Application.Rooms;
using BrainArena.Domain.Enums;

namespace BrainArena.Application.Tests.Rooms;

public class RoomValidationTests
{
    private static readonly string[] ValidGameModeKeys = [MultipleChoiceGameMode.Key, CalculationGameMode.Key];

    private static CreateRoomRequest ValidRequest() => new(
        Name: "Math Duel",
        Topic: RoomTopic.Math,
        MaxPlayers: 6,
        MinPlayersToStart: 2,
        QuestionCount: 10,
        SecondsPerQuestion: 20,
        IsPrivate: false);

    [Fact]
    public void Validate_AcceptsARequestWithinAllLimits()
    {
        var exception = Record.Exception(() => RoomValidation.Validate(ValidRequest(), ValidGameModeKeys));

        Assert.Null(exception);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(11)]
    public void Validate_RejectsMaxPlayersOutsideRange(int maxPlayers)
    {
        var request = ValidRequest() with { MaxPlayers = maxPlayers };

        Assert.Throws<AppException>(() => RoomValidation.Validate(request, ValidGameModeKeys));
    }

    [Fact]
    public void Validate_RejectsMinPlayersBelowTwo()
    {
        var request = ValidRequest() with { MinPlayersToStart = 1 };

        Assert.Throws<AppException>(() => RoomValidation.Validate(request, ValidGameModeKeys));
    }

    [Fact]
    public void Validate_RejectsMinPlayersGreaterThanMaxPlayers()
    {
        var request = ValidRequest() with { MaxPlayers = 4, MinPlayersToStart = 5 };

        Assert.Throws<AppException>(() => RoomValidation.Validate(request, ValidGameModeKeys));
    }

    [Theory]
    [InlineData(4)]
    [InlineData(21)]
    public void Validate_RejectsQuestionCountOutsideRange(int questionCount)
    {
        var request = ValidRequest() with { QuestionCount = questionCount };

        Assert.Throws<AppException>(() => RoomValidation.Validate(request, ValidGameModeKeys));
    }

    [Theory]
    [InlineData(9)]
    [InlineData(61)]
    public void Validate_RejectsSecondsPerQuestionOutsideRange(int seconds)
    {
        var request = ValidRequest() with { SecondsPerQuestion = seconds };

        Assert.Throws<AppException>(() => RoomValidation.Validate(request, ValidGameModeKeys));
    }

    [Theory]
    [InlineData("")]
    [InlineData("ab")]
    public void Validate_RejectsRoomNameThatIsTooShort(string name)
    {
        var request = ValidRequest() with { Name = name };

        Assert.Throws<AppException>(() => RoomValidation.Validate(request, ValidGameModeKeys));
    }

    [Fact]
    public void Validate_RejectsAnUnknownGameMode()
    {
        var request = ValidRequest() with { GameMode = "memory" };

        Assert.Throws<AppException>(() => RoomValidation.Validate(request, ValidGameModeKeys));
    }

    [Theory]
    [InlineData(MultipleChoiceGameMode.Key)]
    [InlineData(CalculationGameMode.Key)]
    public void Validate_AcceptsEveryRegisteredGameMode(string gameMode)
    {
        var request = ValidRequest() with { GameMode = gameMode };

        var exception = Record.Exception(() => RoomValidation.Validate(request, ValidGameModeKeys));

        Assert.Null(exception);
    }
}
