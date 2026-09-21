using BrainArena.Application.Common;
using BrainArena.Application.Questions;
using BrainArena.Domain.Enums;

namespace BrainArena.Application.Tests.Questions;

public class QuestionValidationTests
{
    private static QuestionUpsertRequest ValidRequest() => new(
        Topic: RoomTopic.Math,
        Difficulty: 2,
        Text: "What is 2 + 2?",
        Options: ["3", "4", "5", "6"],
        CorrectOptionIndex: 1,
        Explanation: "2 + 2 equals 4.",
        Language: "es");

    [Fact]
    public void Validate_AcceptsAWellFormedQuestion()
    {
        var exception = Record.Exception(() => QuestionValidation.Validate(ValidRequest()));

        Assert.Null(exception);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Hi")]
    public void Validate_RejectsTextThatIsTooShort(string text)
    {
        var request = ValidRequest() with { Text = text };

        Assert.Throws<AppException>(() => QuestionValidation.Validate(request));
    }

    [Fact]
    public void Validate_RejectsFewerThanFourOptions()
    {
        var request = ValidRequest() with { Options = ["a", "b", "c"] };

        Assert.Throws<AppException>(() => QuestionValidation.Validate(request));
    }

    [Fact]
    public void Validate_RejectsAnEmptyOption()
    {
        var request = ValidRequest() with { Options = ["a", "", "c", "d"] };

        Assert.Throws<AppException>(() => QuestionValidation.Validate(request));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    public void Validate_RejectsCorrectOptionIndexOutOfRange(int index)
    {
        var request = ValidRequest() with { CorrectOptionIndex = index };

        Assert.Throws<AppException>(() => QuestionValidation.Validate(request));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    public void Validate_RejectsDifficultyOutOfRange(int difficulty)
    {
        var request = ValidRequest() with { Difficulty = difficulty };

        Assert.Throws<AppException>(() => QuestionValidation.Validate(request));
    }

    [Fact]
    public void Validate_RejectsAnUnsupportedLanguage()
    {
        var request = ValidRequest() with { Language = "fr" };

        Assert.Throws<AppException>(() => QuestionValidation.Validate(request));
    }
}
