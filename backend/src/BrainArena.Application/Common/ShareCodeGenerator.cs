using System.Security.Cryptography;

namespace BrainArena.Application.Common;

public static class ShareCodeGenerator
{
    // Avoids ambiguous characters (0/O, 1/I).
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private const int Length = 6;

    public static string Generate()
    {
        Span<char> code = stackalloc char[Length];
        for (var i = 0; i < Length; i++)
        {
            code[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        }
        return new string(code);
    }
}
