using BrainArena.Application.Common;

namespace BrainArena.Application.Chat;

public static class ChatMessageValidation
{
    public const int MaxLength = 200;

    public static void Validate(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Trim().Length > MaxLength)
        {
            throw new AppException($"Messages must be between 1 and {MaxLength} characters.");
        }
    }
}
