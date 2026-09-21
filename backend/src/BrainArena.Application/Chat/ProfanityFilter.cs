using System.Text.RegularExpressions;

namespace BrainArena.Application.Chat;

/// <summary>
/// A basic wordlist-based profanity filter: blocked words are censored with asterisks
/// (same length) rather than rejecting the whole message outright.
/// </summary>
public static partial class ProfanityFilter
{
    private static readonly HashSet<string> BlockedWords = new(StringComparer.OrdinalIgnoreCase)
    {
        // Spanish
        "mierda", "puta", "puto", "pendejo", "cabron", "cabrón", "idiota", "estupido", "estúpido",
        "imbecil", "imbécil", "gilipollas", "joder", "cono", "coño",
        // English
        "fuck", "shit", "bitch", "asshole", "bastard", "idiot", "stupid", "damn", "crap"
    };

    public static string Sanitize(string text) =>
        WordPattern().Replace(text, match =>
            BlockedWords.Contains(match.Value) ? new string('*', match.Value.Length) : match.Value);

    [GeneratedRegex(@"\b\w+\b", RegexOptions.CultureInvariant)]
    private static partial Regex WordPattern();
}
