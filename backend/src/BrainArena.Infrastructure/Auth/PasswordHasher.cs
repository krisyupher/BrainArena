using BrainArena.Application.Abstractions;
using BrainArena.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace BrainArena.Infrastructure.Auth;

/// <summary>Thin wrapper over ASP.NET Core Identity's PBKDF2 hasher, used standalone (no full Identity system).</summary>
public class PasswordHasher : IPasswordHasher
{
    private readonly PasswordHasher<User> _inner = new();

    public string Hash(User user, string password) => _inner.HashPassword(user, password);

    public bool Verify(User user, string hash, string password) =>
        _inner.VerifyHashedPassword(user, hash, password) != PasswordVerificationResult.Failed;
}
