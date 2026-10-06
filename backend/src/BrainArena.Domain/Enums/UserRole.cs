namespace BrainArena.Domain.Enums;

public enum UserRole
{
    Player = 0,
    Admin = 1,
    /// <summary>Throwaway account auto-created for anonymous Solitary practice — practice-only, purged after its token expires.</summary>
    Guest = 2
}
