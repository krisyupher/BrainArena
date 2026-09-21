using BrainArena.Application.Chat;
using BrainArena.Application.Common;

namespace BrainArena.Application.Tests.Chat;

public class ChatMessageValidationTests
{
    [Fact]
    public void Validate_AcceptsAnOrdinaryMessage()
    {
        var exception = Record.Exception(() => ChatMessageValidation.Validate("Good luck everyone!"));

        Assert.Null(exception);
    }

    [Fact]
    public void Validate_RejectsAnEmptyMessage()
    {
        Assert.Throws<AppException>(() => ChatMessageValidation.Validate("   "));
    }

    [Fact]
    public void Validate_RejectsAMessageOverTwoHundredCharacters()
    {
        var tooLong = new string('a', 201);

        Assert.Throws<AppException>(() => ChatMessageValidation.Validate(tooLong));
    }

    [Fact]
    public void Validate_AcceptsExactlyTwoHundredCharacters()
    {
        var exactly200 = new string('a', 200);

        var exception = Record.Exception(() => ChatMessageValidation.Validate(exactly200));

        Assert.Null(exception);
    }
}
