using BrainArena.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BrainArena.Infrastructure.Data;

public class BrainArenaDbContext(DbContextOptions<BrainArenaDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<RoomPlayer> RoomPlayers => Set<RoomPlayer>();
    public DbSet<Question> Questions => Set<Question>();
    public DbSet<Match> Matches => Set<Match>();
    public DbSet<MatchQuestion> MatchQuestions => Set<MatchQuestion>();
    public DbSet<MatchPlayer> MatchPlayers => Set<MatchPlayer>();
    public DbSet<MatchAnswer> MatchAnswers => Set<MatchAnswer>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BrainArenaDbContext).Assembly);
    }
}
