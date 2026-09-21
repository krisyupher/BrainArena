using System.Text.RegularExpressions;
using BrainArena.Application.Abstractions;
using BrainArena.Application.Common;
using BrainArena.Domain.Entities;

namespace BrainArena.Application.Auth;

public partial class AuthService(
    IUserRepository users,
    IPasswordHasher passwordHasher,
    IJwtTokenService jwtTokenService) : IAuthService
{
    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var displayName = request.DisplayName.Trim();

        if (!EmailRegex().IsMatch(email))
            throw new AppException("Enter a valid email address.");

        if (displayName.Length is < 2 or > 30)
            throw new AppException("Display name must be between 2 and 30 characters.");

        if (request.Password.Length < 8)
            throw new AppException("Password must be at least 8 characters.");

        if (await users.GetByEmailAsync(email, ct) is not null)
            throw new AppException("This email is already registered.", 409);

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            DisplayName = displayName,
            PasswordHash = string.Empty,
            CreatedAt = DateTimeOffset.UtcNow
        };
        user.PasswordHash = passwordHasher.Hash(user, request.Password);

        await users.AddAsync(user, ct);
        await users.SaveChangesAsync(ct);

        return BuildResponse(user);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await users.GetByEmailAsync(email, ct);

        if (user is null || !passwordHasher.Verify(user, user.PasswordHash, request.Password))
            throw new AppException("Invalid email or password.", 401);

        return BuildResponse(user);
    }

    private AuthResponse BuildResponse(User user) =>
        new(jwtTokenService.GenerateToken(user), user.Id, user.DisplayName, user.Role.ToString());

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex EmailRegex();
}
