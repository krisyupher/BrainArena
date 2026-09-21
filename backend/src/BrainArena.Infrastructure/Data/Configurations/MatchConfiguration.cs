using BrainArena.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BrainArena.Infrastructure.Data.Configurations;

public class MatchConfiguration : IEntityTypeConfiguration<Match>
{
    public void Configure(EntityTypeBuilder<Match> builder)
    {
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Status).HasConversion<string>().HasMaxLength(20);

        // One match per room in this phase — Room:Match is effectively 1:1 (a finished room isn't reused).
        builder.HasIndex(m => m.RoomId).IsUnique();

        builder.HasOne(m => m.Room)
            .WithMany()
            .HasForeignKey(m => m.RoomId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class MatchQuestionConfiguration : IEntityTypeConfiguration<MatchQuestion>
{
    public void Configure(EntityTypeBuilder<MatchQuestion> builder)
    {
        builder.HasKey(mq => mq.Id);

        builder.HasOne(mq => mq.Match)
            .WithMany(m => m.Questions)
            .HasForeignKey(mq => mq.MatchId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(mq => mq.Question)
            .WithMany()
            .HasForeignKey(mq => mq.QuestionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class MatchPlayerConfiguration : IEntityTypeConfiguration<MatchPlayer>
{
    public void Configure(EntityTypeBuilder<MatchPlayer> builder)
    {
        builder.HasKey(mp => new { mp.MatchId, mp.UserId });

        builder.HasOne(mp => mp.Match)
            .WithMany(m => m.Players)
            .HasForeignKey(mp => mp.MatchId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(mp => mp.User)
            .WithMany()
            .HasForeignKey(mp => mp.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class MatchAnswerConfiguration : IEntityTypeConfiguration<MatchAnswer>
{
    public void Configure(EntityTypeBuilder<MatchAnswer> builder)
    {
        builder.HasKey(a => new { a.MatchQuestionId, a.UserId });

        builder.HasOne(a => a.MatchQuestion)
            .WithMany(mq => mq.Answers)
            .HasForeignKey(a => a.MatchQuestionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.User)
            .WithMany()
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
