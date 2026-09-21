using BrainArena.Domain.Entities;

namespace BrainArena.Application.Abstractions;

public interface IJwtTokenService
{
    string GenerateToken(User user);
}
