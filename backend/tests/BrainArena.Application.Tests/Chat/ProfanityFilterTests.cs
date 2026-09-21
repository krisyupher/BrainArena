using BrainArena.Application.Chat;

namespace BrainArena.Application.Tests.Chat;

public class ProfanityFilterTests
{
    [Fact]
    public void Sanitize_LeavesCleanTextUnchanged()
    {
        var result = ProfanityFilter.Sanitize("Good game everyone, gg!");

        Assert.Equal("Good game everyone, gg!", result);
    }

    [Theory]
    [InlineData("fuck")]
    [InlineData("shit")]
    [InlineData("idiot")]
    public void Sanitize_CensorsEnglishBlockedWords(string word)
    {
        var result = ProfanityFilter.Sanitize($"You are such an {word}!");

        Assert.DoesNotContain(word, result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(new string('*', word.Length), result);
    }

    [Theory]
    [InlineData("idiota")]
    [InlineData("estupido")]
    public void Sanitize_CensorsSpanishBlockedWords(string word)
    {
        var result = ProfanityFilter.Sanitize($"Eres un {word}.");

        Assert.DoesNotContain(word, result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Sanitize_IsCaseInsensitive()
    {
        var result = ProfanityFilter.Sanitize("SHUT UP YOU IDIOT");

        Assert.DoesNotContain("IDIOT", result);
    }

    [Fact]
    public void Sanitize_DoesNotCensorSubstringsInsideInnocentWords()
    {
        // "classic" contains no blocked word as a whole word; guards against overly broad matching.
        var result = ProfanityFilter.Sanitize("That was a classic move.");

        Assert.Equal("That was a classic move.", result);
    }
}
