using BrainArena.Domain.Enums;
using Microsoft.AspNetCore.Authorization;

namespace BrainArena.Api.Auth;

public static class AuthPolicies
{
    /// <summary>A real account (Player/Admin) — excludes Guest sessions, which are Solitary-practice-only.</summary>
    public const string RegisteredUser = "RegisteredUser";

    public static void Configure(AuthorizationOptions options) =>
        options.AddPolicy(RegisteredUser, policy => policy.RequireRole(nameof(UserRole.Player), nameof(UserRole.Admin)));
}
