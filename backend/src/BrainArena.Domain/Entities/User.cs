using BrainArena.Domain.Enums;

namespace BrainArena.Domain.Entities;

public class User
{
    public Guid Id { get; set; }
    public required string Email { get; set; }
    public required string PasswordHash { get; set; }
    public required string DisplayName { get; set; }
    public UserRole Role { get; set; } = UserRole.Player;
    public DateTimeOffset CreatedAt { get; set; }
}
