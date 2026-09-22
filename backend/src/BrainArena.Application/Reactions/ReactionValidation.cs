using BrainArena.Application.Common;

namespace BrainArena.Application.Reactions;

/// <summary>
/// Pure validation, kept separate from the hub so it's unit testable without a connection.
/// Reactions are a small fixed emoji set (not free text) — no profanity filter or persistence
/// needed, unlike chat.
/// </summary>
public static class ReactionValidation
{
    public static readonly IReadOnlyList<string> AllowedEmojis = ["👏", "🎉", "😂", "😮", "❤️", "🔥"];

    public static void Validate(string emoji)
    {
        if (!AllowedEmojis.Contains(emoji))
        {
            throw new AppException("Unknown reaction.", 400);
        }
    }
}
