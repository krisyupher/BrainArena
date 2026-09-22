using BrainArena.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BrainArena.Infrastructure.Data.Configurations;

public class TournamentConfiguration : IEntityTypeConfiguration<Tournament>
{
    public void Configure(EntityTypeBuilder<Tournament> builder)
    {
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Name).IsRequired().HasMaxLength(40);
        builder.Property(t => t.Topic).HasConversion<string>().HasMaxLength(20);
        builder.Property(t => t.GameMode).IsRequired().HasMaxLength(30);
        builder.Property(t => t.Status).HasConversion<string>().HasMaxLength(20);

        builder.HasOne(t => t.CreatorUser)
            .WithMany()
            .HasForeignKey(t => t.CreatorUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class TournamentPlayerConfiguration : IEntityTypeConfiguration<TournamentPlayer>
{
    public void Configure(EntityTypeBuilder<TournamentPlayer> builder)
    {
        builder.HasKey(p => new { p.TournamentId, p.UserId });
        builder.Property(p => p.Status).HasConversion<string>().HasMaxLength(20);

        builder.HasOne(p => p.Tournament)
            .WithMany(t => t.Players)
            .HasForeignKey(p => p.TournamentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(p => p.User)
            .WithMany()
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class TournamentRoundConfiguration : IEntityTypeConfiguration<TournamentRound>
{
    public void Configure(EntityTypeBuilder<TournamentRound> builder)
    {
        builder.HasKey(r => r.Id);

        builder.HasOne(r => r.Tournament)
            .WithMany(t => t.Rounds)
            .HasForeignKey(r => r.TournamentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class TournamentRoundRoomConfiguration : IEntityTypeConfiguration<TournamentRoundRoom>
{
    public void Configure(EntityTypeBuilder<TournamentRoundRoom> builder)
    {
        builder.HasKey(rr => rr.Id);

        // The reverse lookup MatchOrchestrator uses (given only a RoomId) to check "is this room
        // part of a tournament round" — see TournamentService.HandleRoomMatchFinishedAsync.
        builder.HasIndex(rr => rr.RoomId).IsUnique();

        builder.HasOne(rr => rr.TournamentRound)
            .WithMany(r => r.RoundRooms)
            .HasForeignKey(rr => rr.TournamentRoundId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(rr => rr.Room)
            .WithMany()
            .HasForeignKey(rr => rr.RoomId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
