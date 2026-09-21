using BrainArena.Domain.Entities;

namespace BrainArena.Application.Abstractions;

public interface IPasswordHasher
{
    string Hash(User user, string password);
    bool Verify(User user, string hash, string password);
}
