using BrainArena.Application.Common;
using BrainArena.Application.Reactions;

namespace BrainArena.Application.Tests.Reactions;

public class ReactionValidationTests
{
    [Theory]
    [MemberData(nameof(AllowedEmojis))]
    public void Validate_AcceptsEveryAllowedEmoji(string emoji)
    {
        var exception = Record.Exception(() => ReactionValidation.Validate(emoji));

        Assert.Null(exception);
    }

    public static IEnumerable<object[]> AllowedEmojis() =>
        ReactionValidation.AllowedEmojis.Select(emoji => new object[] { emoji });

    [Theory]
    [InlineData("")]
    [InlineData("🙂")]
    [InlineData("not an emoji")]
    public void Validate_RejectsAnythingNotInTheAllowedSet(string emoji)
    {
        var exception = Assert.Throws<AppException>(() => ReactionValidation.Validate(emoji));

        Assert.Equal(400, exception.StatusCode);
    }
}
