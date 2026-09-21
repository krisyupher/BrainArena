using BrainArena.Application.Common;

namespace BrainArena.Application.Questions;

/// <summary>
/// Pure validation rules for a question, kept separate from QuestionService so they can be unit
/// tested without a database. Shared by both the single create/edit path and the JSON import path.
/// </summary>
public static class QuestionValidation
{
    public const int MinDifficulty = 1;
    public const int MaxDifficulty = 3;
    public const int OptionCount = 4;

    private static readonly string[] SupportedLanguages = ["es", "en"];

    public static void Validate(QuestionUpsertRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Text) || request.Text.Trim().Length is < 5 or > 500)
        {
            throw new AppException("Question text must be between 5 and 500 characters.");
        }

        if (request.Options.Count != OptionCount)
        {
            throw new AppException($"Exactly {OptionCount} options are required.");
        }

        if (request.Options.Any(o => string.IsNullOrWhiteSpace(o) || o.Trim().Length > 200))
        {
            throw new AppException("Each option must be non-empty and at most 200 characters.");
        }

        if (request.CorrectOptionIndex < 0 || request.CorrectOptionIndex >= OptionCount)
        {
            throw new AppException("Correct option index must reference one of the options.");
        }

        if (string.IsNullOrWhiteSpace(request.Explanation) || request.Explanation.Trim().Length is < 5 or > 1000)
        {
            throw new AppException("Explanation must be between 5 and 1000 characters.");
        }

        if (request.Difficulty is < MinDifficulty or > MaxDifficulty)
        {
            throw new AppException($"Difficulty must be between {MinDifficulty} and {MaxDifficulty}.");
        }

        if (!SupportedLanguages.Contains(request.Language))
        {
            throw new AppException("Language must be 'es' or 'en'.");
        }
    }
}
